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

    let rec getFakeValue (dataType: Type) = 
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


    let private getDbTypeEnum name = 
        match name with
        | "int" -> DbType.Int32
        | "varchar" -> DbType.String
        | "nvarchar" -> DbType.String
        | "text" -> DbType.String
        | "date" -> DbType.Date
        | "datetime" -> DbType.DateTime
        | "bit" -> DbType.Boolean
        | _ -> DbType.String 
        :> obj

    /// <summary>
    /// Reads parameter names and directions from information schema.
    /// </summary>
    /// <param name="createConnection">
    /// Function creating database connection.
    /// </param>
    /// <param name="createCommand">
    /// Function creating database command.
    /// </param>
    /// <param name="procedureName">
    /// The name of the procedure.
    /// </param>
    let extractProcParamNames (createConnection: unit -> IDbConnection) (createCommand: IDbConnection -> IDbCommand) (procedureName: string) = 
        use connection = createConnection()
        connection.Open()
        use command = createCommand(connection)
        command.CommandText <- "select p.parameter_name, p.parameter_mode, p.data_type
                                from information_schema.parameters p
                                        join information_schema.routines r on r.specific_name = p.specific_name
                                where r.routine_name = @procedure_name
                                order by p.ordinal_position"
        let param = command.CreateParameter()
        param.ParameterName <- "@procedure_name"
        param.Value <- procedureName.Split('.') |> Seq.last
        command.Parameters.Add(param) |> ignore
        use reader = command.ExecuteReader()
        [ while reader.Read() do 
            yield 
                (if reader.GetString(0).StartsWith("@") then reader.GetString(0).Substring(1) else reader.GetString(0)), 
                reader.GetString(1) <> "IN", getDbTypeEnum(reader.GetString(2))
        ]


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
            let exprs = 
                fields
                |> Seq.collect (fun p -> customPB (prefix + getFieldPrefix p) p.Name (Expression.Property(expr, p)) paramNames)
                |> Seq.filter (fun (name, _, _, _) -> ("<connection>" :: "<transaction>" :: paramNames) |> Seq.exists ((=) name))
                |> List.ofSeq
            exprs
        | Tuple _ ->
            getTupleParamExpressions customPB expr 0 paramNames
        | OptionOf optType when optType.IsEnum ->
            let expr = convertOption optType expr
            getParamExpressions customPB prefix name expr paramNames
        | EnumOf (eType, values) ->
            let expr = Expression.Convert(convertEnum eType values expr, typeof<obj>) :> Expression
            getParamExpressions customPB prefix name expr paramNames
        | Unit ->
            []
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

 