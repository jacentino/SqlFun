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
    
    let (|Connection|_|) (t: Type) =
        if typeof<IDbConnection>.IsAssignableFrom(t) then Some () else None

    let (|TransactionOption|_|) (t: Type) =
        if typeof<IDbTransaction option>.IsAssignableFrom(t) then Some () else None

    let private buildInParam (name: string, expr: Expression) value (command: IDbCommand) =
        let param = command.CreateParameter()
        param.ParameterName <- "@" + name
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


    let (<+>) (pb1: ParamBuilder -> ParamBuilder) (pb2: ParamBuilder-> ParamBuilder) (next: ParamBuilder): ParamBuilder = 
        pb1 <| pb2 next