namespace SqlFun

open System
open System.Reflection
open System.Linq.Expressions
open System.Data

open FSharp.Reflection

open SqlFun.Types
open SqlFun.ExpressionExtensions

type ParamBuilder = string -> string -> Expression -> string list -> (string * Expression * (obj -> IDbCommand -> int) * obj) list

module ParamBuilder =        
    open System.Text.RegularExpressions
    open System.Linq

    let (|Connection|_|) (t: Type) =
        if typeof<IDbConnection>.IsAssignableFrom(t) then Some () else None

    let (|TransactionOption|_|) (t: Type) =
        if typeof<IDbTransaction option>.IsAssignableFrom(t) then Some () else None

    let private buildInParam (name: string, expr: Expression) value (command: IDbCommand) =
        let param = command.CreateParameter()
        param.ParameterName <- name
        param.Value <- value
        command.Parameters.Add(param)            

    let rec private getFakeValue (dataType: Type) = 
        if isOption dataType
        then getFakeValue (getUnderlyingType dataType)
        elif dataType = typeof<DateTime>
        then DateTime.Now :> obj
        elif dataType = typeof<string>
        then "" :> obj
        elif dataType.IsClass || dataType.IsInterface
        then null 
        else Activator.CreateInstance(dataType)

    let skipUsedParamNames paramExprs paramNames = 
        let usedNames = paramExprs 
                        |> Seq.map (fun (name, _, _, _) -> name) 
                        |> Seq.except ["<connection>"; "<transaction>"] 
                        |> List.ofSeq
        let length = List.length usedNames
        if paramNames |> Seq.take length |> Seq.except usedNames |> Seq.isEmpty
        then paramNames |> List.skip length
        else failwith "Inconsistent parameter list."

    let rec private getTupleParamExpressions (customPB: ParamBuilder) (expr: Expression) (index: int) (paramNames: string list) = 
        let tupleItemTypes = FSharpType.GetTupleElements expr.Type
        if index = tupleItemTypes.Length
        then
            []
        else
            let param = Expression.TupleGet(expr, index)
            let paramExprs = customPB "" (Seq.head paramNames) param paramNames
            List.append paramExprs (getTupleParamExpressions customPB expr (index + 1) (skipUsedParamNames paramExprs paramNames))


    let private getFieldPrefix (field: PropertyInfo) = 
        field.GetCustomAttributes<PrefixedAttribute>() 
        |> Seq.map (fun a -> if a.Name <> "" then a.Name else field.Name)
        |> Seq.fold (fun last next -> next) ""

    let private convertEnum (enumType: Type) (values: (obj * obj) list) (expr: Expression) = 
        if values |> Seq.exists (fun (e, v) -> e <> v) then            
            let comparer e = Expression.Equal(Expression.Constant(e), expr) :> Expression
            values |> Seq.fold 
                        (fun cexpr (e, v) -> Expression.Condition(comparer e, Expression.Constant(v, enumType), cexpr) :> Expression) 
                        (Expression.Constant(null, enumType) :> Expression)
        else
            expr

    let private convertOption (optType) (expr: Expression) =
        let param = Expression.Parameter(optType, "v")
        match param.Type with
        | EnumOf (eType, values) -> 
            Expression.Call([| optType; eType |], "MapOption", Expression.Lambda(convertEnum eType values param, param), expr) :> Expression
        | _ ->
            expr

    /// <summary>
    /// Searches for parameter names in a command.
    /// </summary>
    /// <param name="commandText">
    /// The SQL command.
    /// </param>
    let extractParameterNames prefix commandText = 
        let var = sprintf "(declare\s+\%s[a-zA-Z0-9_]+)|(\%s\%s[a-zA-Z0-9_]+)" prefix prefix prefix
        let cmd = Regex.Matches(commandText, var, RegexOptions.IgnoreCase).Cast<Match>()
                    |> Seq.collect (fun m -> m.Captures.Cast<Capture>())
                    |> Seq.map (fun c -> c.Value.Split(' ').Last())
                    |> Seq.fold (fun (cmd: string) tr -> cmd.Replace(tr, "")) commandText
        let param = sprintf "\%s[a-zA-Z0-9_]+" prefix
        Regex.Matches(cmd, param, RegexOptions.IgnoreCase).Cast<Match>() 
        |> Seq.collect (fun m -> m.Captures.Cast<Capture>()) 
        |> Seq.map (fun c -> c.Value.Substring(1))
        |> Seq.distinct
        |> List.ofSeq

    /// <summary>
    /// Most default parameter building functionality.
    /// </summary>
    /// <param name="customPB">
    /// Another parameter builder implementing customizations.
    /// </param>
    /// <param name="prefix">
    /// Parameter name prefix.
    /// </param>
    /// <param name="name">
    /// Parameter name.
    /// </param>
    /// <param name="expr">
    /// Expression calculating parameter value from function parameter.
    /// </param>
    /// <param name="paramNames">
    /// List of available parameter names extracted from SQL command.
    /// </param>
    let rec getParamExpressions (customPB: ParamBuilder) (prefix: string) (name: string) (expr: Expression) (paramNames: string list) =         
        match expr.Type with
        | Connection ->
            ["<connection>", expr, (fun _ _ -> 0), null :> obj]
        | TransactionOption ->
            ["<transaction>", expr, (fun _ _ -> 0), null :> obj] 
        | Record fields ->
            fields
            |> Seq.collect (fun p -> customPB (prefix + getFieldPrefix p) p.Name (Expression.Property(expr, p)) paramNames)
            |> Seq.filter (fun (name, _, _, _) -> ("<connection>" :: "<transaction>" :: paramNames) |> Seq.exists ((=) name))
            |> List.ofSeq
        | Tuple _ ->
            getTupleParamExpressions customPB expr 0 paramNames
        | OptionOf optType when optType.IsEnum ->
            let expr = convertOption optType expr
            getParamExpressions customPB prefix name expr paramNames
        | EnumOf (eType, values) ->
            let expr = Expression.Convert(convertEnum eType values expr, typeof<obj>) :> Expression
            getParamExpressions customPB prefix name expr paramNames
        | _ ->
            [prefix + name, expr, buildInParam (prefix + name, expr), getFakeValue expr.Type]

    let rec private buildParamDefsInternal customPB t paramNames paramDefs = 
        match t with
        | Function (firstParamType, remainingParams) ->
            let param = Expression.Parameter(firstParamType, Seq.head paramNames)
            let paramGetters = customPB "" param.Name param paramNames
            let (paramExprs, paramDefs, retType) = buildParamDefsInternal customPB remainingParams (skipUsedParamNames paramGetters paramNames) (List.append paramDefs paramGetters)
            (param :: paramExprs), paramDefs, retType
        | _ ->
            [], paramDefs, t
    
    let private cyclePB (pb: ParamBuilder -> ParamBuilder): ParamBuilder = 
        let next: Ref<ParamBuilder> = ref (fun _ _ _ _ -> [])
        let first = (fun prefix name expr names -> pb !next prefix name expr names)
        next := first
        first


    let buildParamDefs pb t paramNames = 
        buildParamDefsInternal (cyclePB pb) t paramNames []

    /// <summary>
    /// Composes two parameter builders.
    /// </summary>
    /// <param name="pb1">
    /// First parameter builder.
    /// </param>
    /// <param name="pb2">
    /// Second parameter builder.
    /// </param>
    /// <param name="next">
    /// Next item in parameter building cycle.
    /// </param>
    let (<+>) (pb1: ParamBuilder -> ParamBuilder) (pb2: ParamBuilder-> ParamBuilder) (next: ParamBuilder): ParamBuilder = 
        pb1 <| pb2 next

    /// <summary>
    /// Parameter builder transforming list of values (intentionally of simple type)
    /// by adding SQL parameters for all elements.
    /// </summary>
    /// <param name="isAllowed">
    /// Function determining if list elements have valid type.
    /// </param>
    /// <param name="toString">
    /// Converts element to string representing SQL literal of element.
    /// </param>
    /// <param name="defaultPB">
    /// Next item in parameter building cycle.
    /// </param>
    /// <param name="prefix">
    /// Parameter name prefix.
    /// </param>
    /// <param name="name">
    /// Parameter name.
    /// </param>
    /// <param name="expr">
    /// Expression calculating parameter value from function parameter.
    /// </param>
    /// <param name="names">
    /// List of available parameter names extracted from SQL command.
    /// </param>
    let listParamBuilder isAllowed paramPrefix defaultPB prefix name (expr: Expression) names = 
        match expr.Type with 
        | CollectionOf itemType when isAllowed itemType ->
            [
                prefix + name,
                expr,
                fun (value: obj) (command: IDbCommand) ->
                    let first = command.Parameters.Count
                    for v in value :?> System.Collections.IEnumerable do
                        let param = command.CreateParameter()
                        param.ParameterName <- name + string(command.Parameters.Count - first)
                        param.Value <- v
                        command.Parameters.Add(param) |> ignore
                    let names = [| for i in 0..command.Parameters.Count - first - 1 -> paramPrefix + name + string(i) |] 
                    let newCommandText = command.CommandText.Replace(paramPrefix + name, names |> String.concat ",")
                    command.CommandText <- newCommandText
                    command.Parameters.Count
                ,
                [ getFakeValue itemType ] :> obj
            ]       
        | _ ->
            defaultPB prefix name expr names

    /// <summary>
    /// Parameter builder handling list of values (intentionally of simple type)
    /// by injecting them directly into SQL command.
    /// </summary>
    /// <param name="isAllowed">
    /// Function determining if list elements have valid type.
    /// </param>
    /// <param name="toString">
    /// Converts element to string representing SQL literal of element.
    /// </param>
    /// <param name="defaultPB">
    /// Next item in parameter building cycle.
    /// </param>
    /// <param name="prefix">
    /// Parameter name prefix.
    /// </param>
    /// <param name="name">
    /// Parameter name.
    /// </param>
    /// <param name="expr">
    /// Expression calculating parameter value from function parameter.
    /// </param>
    /// <param name="names">
    /// List of available parameter names extracted from SQL command.
    /// </param>
    let listDirectParamBuilder isAllowed toString defaultPB prefix name (expr: Expression) names = 
        match expr.Type with 
        | CollectionOf itemType when isAllowed itemType ->
            [
                prefix + name,
                expr,
                fun (value: obj) (command: IDbCommand) ->
                    let values = [| for v in value :?> System.Collections.IEnumerable do yield toString v |] 
                    let newCommandText = command.CommandText.Replace("@" + name, values |> String.concat ",")
                    command.CommandText <- newCommandText
                    command.Parameters.Count
                ,
                [ getFakeValue itemType ] :> obj
            ]       
        | _ ->
            defaultPB prefix name expr names

    let simpleConversionParamBuilder (convert: 't -> 'c) defaultPB prefix name (expr: Expression) names =
        if expr.Type = typeof<'t> then
            let convertExpr = Expression.Invoke(Expression.Constant(Func<'t, 'c>(convert)), expr) 
            defaultPB prefix name (convertExpr :> Expression) names
        else
            defaultPB prefix name expr names        