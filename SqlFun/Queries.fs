namespace SqlFun

open System
open System.Data
open System.Data.Common
open System.Reflection
open System.Linq
open System.Linq.Expressions
open System.Text.RegularExpressions

open ExpressionExtensions
open Types
open SqlFun.Exceptions
open SqlFun.ParamBuilder
open SqlFun.ResultBuilder

module Queries =

    let private rmap (f: IDataReader -> 't) (r: IDataReader): 't list =
        [ while r.Read() do yield f r ]

    type Toolbox() = 
        inherit ExpressionExtensions.Toolbox()

        static member ExecuteSql 
                (createCommand: IDbConnection -> IDbCommand) 
                (connection: IDbConnection) 
                (transaction: IDbTransaction option) 
                (commandText: string) 
                (commandTimeout: int option) 
                (assignParams: Func<IDbCommand, int>) 
                (buildResult: Func<IDataReader, 't>) =
            use command = createCommand(connection)
            match transaction with
            | Some t -> command.Transaction <- t
            | None -> ()
            command.CommandText <- commandText
            match commandTimeout with
            | Some ct -> command.CommandTimeout <- ct
            | None -> ()
            assignParams.Invoke(command) |> ignore
            use reader = command.ExecuteReader()
            buildResult.Invoke(reader)

        static member ExecuteProcedure 
                (createCommand: IDbConnection -> IDbCommand) 
                (connection: IDbConnection) 
                (transaction: IDbTransaction option) 
                (procName: string) 
                (commandTimeout: int option) 
                (assignParams: Func<IDbCommand, int>) 
                (buildResult: Func<IDataReader, 't>) 
                (buildOutParams: Func<IDbCommand, 'u>) =
            use command = createCommand(connection)
            command.CommandText <- procName
            command.CommandType <- CommandType.StoredProcedure
            match transaction with
            | Some t -> command.Transaction <- t
            | None -> ()
            match commandTimeout with
            | Some ct -> command.CommandTimeout <- ct
            | None -> ()
            assignParams.Invoke(command) |> ignore
            let retValParam = command.CreateParameter()
            retValParam.ParameterName <- "@RETURN_VALUE"
            retValParam.DbType <- DbType.Int32
            retValParam.Direction <- ParameterDirection.ReturnValue
            command.Parameters.Add(retValParam) |> ignore
            use reader = command.ExecuteReader()
            let retVal = if retValParam.Value = null then 0 else unbox (retValParam.Value)
            let outParamVals = buildOutParams.Invoke(command)
            let result = buildResult.Invoke(reader)
            retVal, outParamVals, result

        static member ExecuteSqlAsync 
                (createCommand: IDbConnection -> IDbCommand) 
                (connection: IDbConnection) 
                (transaction: IDbTransaction option) 
                (commandText: string) 
                (commandTimeout: int option) 
                (assignParams: Func<IDbCommand, int>) 
                (buildResult: Func<DbDataReader, Async<'t>>) =
            async {
                use command = createCommand(connection) :?> DbCommand
                command.CommandText <- commandText
                match transaction with
                | Some t -> command.Transaction <- t :?> DbTransaction
                | None -> ()
                match commandTimeout with
                | Some ct -> command.CommandTimeout <- ct
                | None -> ()
                assignParams.Invoke(command) |> ignore
                use! reader = Async.AwaitTask(command.ExecuteReaderAsync())
                return! buildResult.Invoke(reader)
            }

        static member ExecuteProcedureAsync 
                (createCommand: IDbConnection -> IDbCommand) 
                (connection: IDbConnection) 
                (transaction: IDbTransaction option) 
                (procName: string) 
                (commandTimeout: int option) 
                (assignParams: Func<IDbCommand, int>) 
                (buildResult: Func<DbDataReader, Async<'t>>) 
                (buildOutParams: Func<IDbCommand, 'u>) =
            async {
                use command = createCommand(connection) :?> DbCommand
                command.CommandText <- procName
                command.CommandType <- CommandType.StoredProcedure
                match transaction with
                | Some t -> command.Transaction <- t :?> DbTransaction
                | None -> ()
                match commandTimeout with
                | Some ct -> command.CommandTimeout <- ct
                | None -> ()
                assignParams.Invoke(command) |> ignore
                let retValParam = command.CreateParameter()
                retValParam.ParameterName <- "@RETURN_VALUE"
                retValParam.DbType <- DbType.Int32
                retValParam.Direction <- ParameterDirection.ReturnValue
                command.Parameters.Add(retValParam) |> ignore
                use! reader = Async.AwaitTask(command.ExecuteReaderAsync())
                let! result = buildResult.Invoke(reader)
                return (if retValParam.Value = null then 0 else retValParam.Value :?> int), buildOutParams.Invoke(command), result                   
            }

        static member UnpackOption (value: 't option) = 
            match value with
            | Some v -> v :> obj
            | None -> DBNull.Value :> obj

        static member CompileCaller<'t> (parameters: ParameterExpression list, caller: Expression) =
            Expression.Lambda< Action<'t> >(caller.Reduce(), parameters).Compile()

        static member CompileCaller<'t1, 't2> (parameters: ParameterExpression list, caller: Expression) =      
            let compiled = Expression.Lambda< Func<'t1, 't2> >(caller.Reduce(), parameters).Compile()
            fun a -> compiled.Invoke(a)

        static member CompileCaller<'t1, 't2, 't3> (parameters: ParameterExpression list, caller: Expression) =
            let compiled = Expression.Lambda< Func<'t1, 't2, 't3> >(caller.Reduce(), parameters).Compile()
            fun a1 a2 -> compiled.Invoke(a1, a2)

        static member CompileCaller<'t1, 't2, 't3, 't4> (parameters: ParameterExpression list, caller: Expression) =
            let compiled = Expression.Lambda< Func<'t1, 't2, 't3, 't4> >(caller.Reduce(), parameters).Compile()
            fun a1 a2 a3 -> compiled.Invoke(a1, a2, a3)

        static member CompileCaller<'t1, 't2, 't3, 't4, 't5> (parameters: ParameterExpression list, caller: Expression) =
            let compiled = Expression.Lambda< Func<'t1, 't2, 't3, 't4, 't5> >(caller.Reduce(), parameters).Compile()
            fun a1 a2 a3 a4 -> compiled.Invoke(a1, a2, a3, a4)

        static member CompileCaller<'t1, 't2, 't3, 't4, 't5, 't6> (parameters: ParameterExpression list, caller: Expression) =
            let compiled = Expression.Lambda< Func<'t1, 't2, 't3, 't4, 't5, 't6> >(caller.Reduce(), parameters).Compile()
            fun a1 a2 a3 a4 a5 -> compiled.Invoke(a1, a2, a3, a4, a5)

        static member CompileCaller<'t1, 't2, 't3, 't4, 't5, 't6, 't7> (parameters: ParameterExpression list, caller: Expression) =
            let compiled = Expression.Lambda< Func<'t1, 't2, 't3, 't4, 't5, 't6, 't7> >(caller.Reduce(), parameters).Compile()
            fun a1 a2 a3 a4 a5 a6 -> compiled.Invoke(a1, a2, a3, a4, a5, a6)

        static member CompileCaller<'t1, 't2, 't3, 't4, 't5, 't6, 't7, 't8> (parameters: ParameterExpression list, caller: Expression) =
            let compiled = Expression.Lambda< Func<'t1, 't2, 't3, 't4, 't5, 't6, 't7, 't8> >(caller.Reduce(), parameters).Compile()
            fun a1 a2 a3 a4 a5 a6 a7 -> compiled.Invoke(a1, a2, a3, a4, a5, a6, a7)

        static member CompileCaller<'t1, 't2, 't3, 't4, 't5, 't6, 't7, 't8, 't9> (parameters: ParameterExpression list, caller: Expression) =
            let compiled = Expression.Lambda< Func<'t1, 't2, 't3, 't4, 't5, 't6, 't7, 't8, 't9> >(caller.Reduce(), parameters).Compile()
            fun a1 a2 a3 a4 a5 a6 a7 a8 -> compiled.Invoke(a1, a2, a3, a4, a5, a6, a7, a8)

        static member CompileCaller<'t1, 't2, 't3, 't4, 't5, 't6, 't7, 't8, 't9, 't10> (parameters: ParameterExpression list, caller: Expression) =
            let compiled = Expression.Lambda< Func<'t1, 't2, 't3, 't4, 't5, 't6, 't7, 't8, 't9, 't10> >(caller.Reduce(), parameters).Compile()
            fun a1 a2 a3 a4 a5 a6 a7 a8 a9 -> compiled.Invoke(a1, a2, a3, a4, a5, a6, a7, a8, a9)

        static member CompileCaller<'t1, 't2, 't3, 't4, 't5, 't6, 't7, 't8, 't9, 't10, 't11> (parameters: ParameterExpression list, caller: Expression) =
            let compiled = Expression.Lambda< Func<'t1, 't2, 't3, 't4, 't5, 't6, 't7, 't8, 't9, 't10, 't11> >(caller.Reduce(), parameters).Compile()
            fun a1 a2 a3 a4 a5 a6 a7 a8 a9 a10 -> compiled.Invoke(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10)

        static member CompileCaller<'t1, 't2, 't3, 't4, 't5, 't6, 't7, 't8, 't9, 't10, 't11, 't12> (parameters: ParameterExpression list, caller: Expression) =
            let compiled = Expression.Lambda< Func<'t1, 't2, 't3, 't4, 't5, 't6, 't7, 't8, 't9, 't10, 't11, 't12> >(caller.Reduce(), parameters).Compile()
            fun a1 a2 a3 a4 a5 a6 a7 a8 a9 a10 a11 -> compiled.Invoke(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11)

        static member CompileCaller<'t1, 't2, 't3, 't4, 't5, 't6, 't7, 't8, 't9, 't10, 't11, 't12, 't13> (parameters: ParameterExpression list, caller: Expression) =
            let compiled = Expression.Lambda< Func<'t1, 't2, 't3, 't4, 't5, 't6, 't7, 't8, 't9, 't10, 't11, 't12, 't13> >(caller.Reduce(), parameters).Compile()
            fun a1 a2 a3 a4 a5 a6 a7 a8 a9 a10 a11 a12 -> compiled.Invoke(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12)

        static member CompileCaller<'t1, 't2, 't3, 't4, 't5, 't6, 't7, 't8, 't9, 't10, 't11, 't12, 't13, 't14> (parameters: ParameterExpression list, caller: Expression) =
            let compiled = Expression.Lambda< Func<'t1, 't2, 't3, 't4, 't5, 't6, 't7, 't8, 't9, 't10, 't11, 't12, 't13, 't14> >(caller.Reduce(), parameters).Compile()
            fun a1 a2 a3 a4 a5 a6 a7 a8 a9 a10 a11 a12 a13 -> compiled.Invoke(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13)

        static member CompileCaller<'t1, 't2, 't3, 't4, 't5, 't6, 't7, 't8, 't9, 't10, 't11, 't12, 't13, 't14, 't15> (parameters: ParameterExpression list, caller: Expression) =
            let compiled = Expression.Lambda< Func<'t1, 't2, 't3, 't4, 't5, 't6, 't7, 't8, 't9, 't10, 't11, 't12, 't13, 't14, 't15> >(caller.Reduce(), parameters).Compile()
            fun a1 a2 a3 a4 a5 a6 a7 a8 a9 a10 a11 a12 a13 a14 -> compiled.Invoke(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14)

        static member CompileCaller<'t1, 't2, 't3, 't4, 't5, 't6, 't7, 't8, 't9, 't10, 't11, 't12, 't13, 't14, 't15, 't16> (parameters: ParameterExpression list, caller: Expression) =
            let compiled = Expression.Lambda< Func<'t1, 't2, 't3, 't4, 't5, 't6, 't7, 't8, 't9, 't10, 't11, 't12, 't13, 't14, 't15, 't16> >(caller.Reduce(), parameters).Compile()
            fun a1 a2 a3 a4 a5 a6 a7 a8 a9 a10 a11 a12 a13 a14 a15-> compiled.Invoke(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14, a15)

    type Expression with
    
        static member Call (genericParams: Type array, methodName: string, [<ParamArray>]arguments: Expression array ) =
            Expression.Call(typeof<Toolbox>, genericParams, methodName, arguments)


    let private convertAsNeeded (expr: Expression) =
        match expr.Type with
        | OptionOf optType ->
            Expression.Call([| optType |], "UnpackOption", expr) :> Expression
        | _ ->
            Expression.Convert(expr, typeof<obj>) :> Expression

    let private createInParam (command: Expression) (name: string, getter: Expression, assigner: (obj -> IDbCommand -> int), fakeVal: obj) =
        let adapter = Expression.Constant(Func<obj, Func<IDbCommand, int>>(fun value -> Func<IDbCommand, int>(assigner value)))    
        let valueBound = Expression.Invoke(adapter, convertAsNeeded getter)
        Expression.Invoke(valueBound, command) :> Expression

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

    let private createOutParam (command: Expression) (name: string, dbtype: string) =
        let param = Expression.Variable(typeof<IDataParameter>)
        Expression.Block(
            [| param |],
            Expression.Assign(param, Expression.Call(command, "CreateParameter")),
            Expression.Assign(Expression.Property(param, "ParameterName"), Expression.Constant("@" + name)),
            Expression.Assign(Expression.Property(param, "DbType"), Expression.Constant(getDbTypeEnum dbtype)),
            Expression.Assign(Expression.Property(param, "Direction"), Expression.Constant(ParameterDirection.Output)),
            Expression.Call(Expression.Property(command, "Parameters"), "Add", param)) 
        :> Expression

    let private genParamAssigner (paramDefs: (string * Expression * (obj -> IDbCommand -> int) * obj) list) (outParams: (string * string) list)= 
        let command = Expression.Parameter(typeof<IDbCommand>, "command")
        if paramDefs.Length > 0 then
            let inAssignments = paramDefs |> List.map (createInParam command)  
            let outAssignments = outParams |> List.map (createOutParam command)                                      
            Expression.Lambda(Expression.Block(Seq.append inAssignments outAssignments), command) :> Expression
        else 
            Expression.Lambda(Expression.Constant(0), command) :> Expression

    let private extractParameterNames commandText = 
        let cmd = Regex.Matches(commandText, "(declare\s+\@[a-zA-Z0-9_]+)|(\@\@[a-zA-Z0-9_]+)", RegexOptions.IgnoreCase).Cast<Match>()
                    |> Seq.collect (fun m -> m.Captures.Cast<Capture>())
                    |> Seq.map (fun c -> c.Value.Split(' ').Last())
                    |> Seq.fold (fun (cmd: string) tr -> cmd.Replace(tr, "")) commandText
        Regex.Matches(cmd, "\@[a-zA-Z0-9_]+", RegexOptions.IgnoreCase).Cast<Match>() 
        |> Seq.collect (fun m -> m.Captures.Cast<Capture>()) 
        |> Seq.map (fun c -> c.Value.Substring(1))
        |> Seq.distinct
        |> List.ofSeq
    
    let private compileCaller (paramDefs: ParameterExpression list) (caller: Expression) = 
        let compiler = typeof<Toolbox>.GetMethods(BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic) 
                        |> Seq.find (fun m -> m.Name = "CompileCaller" && m.GetGenericArguments().Length = (List.length paramDefs) + 1)
        let compiler = compiler.MakeGenericMethod(Seq.append (paramDefs |> Seq.map (fun p -> p.Type)) [caller.Type] |> Seq.toArray)
        compiler.Invoke(null, [| paramDefs; caller |])

    let private withoutConnectionAndTransaction (paramDefs: (string * Expression * (obj -> IDbCommand -> int) * obj) list) = 
        paramDefs |> List.filter (fun (name, _, _, _) -> not (["<connection>"; "<transaction>"] |> Seq.exists ((=) name)))

    let private getConnectionExpr (paramDefs: (string * Expression * (obj -> IDbCommand -> int) * obj) list) =
        match paramDefs |> List.tryFind (fun (name, _, _, _) -> name = "<connection>") with
        | Some (_, cexpr, _, _) -> cexpr
        | None -> failwith "Connection parameter required."

    let private getTransactionExpr (paramDefs: (string * Expression * (obj -> IDbCommand -> int) * obj) list) =
        match paramDefs |> List.tryFind (fun (name, _, _, _) -> name = "<transaction>") with
        | Some (_, texpr, _, _) -> texpr
        | None -> Expression.GetNoneUnionCase typeof<IDbTransaction option> :> Expression

    let private generateSqlCommandCaller (createConnection: unit -> IDbConnection) (createCommand: IDbConnection -> IDbCommand) (commandTimeout: int option) (paramBuilder: ParamBuilder -> ParamBuilder) (rowBuilder: RowBuilder -> RowBuilder) (commandText: string)  (t: Type): obj = 

        let makeDiagnosticCall (paramDefs: (string * Expression * (obj -> IDbCommand -> int) * obj) list) = 
            use connection = createConnection()
            connection.Open()
            use command = createCommand(connection)
            command.CommandText <- commandText
            for _, _, buildParam, fakeVal in paramDefs do
                buildParam fakeVal command |> ignore
            command.ExecuteReader(CommandBehavior.SchemaOnly).Dispose()

        let getResultMetadata (paramDefs: (string * Expression * (obj -> IDbCommand -> int) * obj) list) = 
            use connection = createConnection()
            connection.Open()
            use command = createCommand(connection)
            command.CommandText <- commandText
            for _, expr, buildParam, fakeVal in paramDefs do
                buildParam fakeVal command |> ignore
            use schemaOnlyReader = command.ExecuteReader(CommandBehavior.SchemaOnly)
            let getOneResultMetadata () = [0.. schemaOnlyReader.FieldCount - 1] 
                                            |> Seq.map (fun i -> schemaOnlyReader.GetName(i).ToLower(), (i, schemaOnlyReader.GetFieldType(i))) 
                                            |> Map.ofSeq
            let initial = getOneResultMetadata ()
            initial :: List.unfold (fun _ -> if schemaOnlyReader.NextResult() then Some (getOneResultMetadata(), ()) else None) ()

        let genExecutor createCommand paramDefs returnType = 
            let queryParamDefs = paramDefs |> withoutConnectionAndTransaction
            let assignParams = genParamAssigner queryParamDefs []    
            let metadata = if returnType <> typeof<unit> && not (Types.isAsyncOf returnType typeof<unit>) // Npgsql hangs on NextResult if no first result exists
                            then 
                                getResultMetadata queryParamDefs                                
                            else 
                                makeDiagnosticCall queryParamDefs
                                [Map.empty]
            let connection = getConnectionExpr paramDefs
            let transaction = getTransactionExpr paramDefs
            let sql = Expression.Constant commandText :> Expression
            let createCmd = Expression.Constant(createCommand) :> Expression
            let timeout = Expression.Constant(commandTimeout, typeof<int option>) :> Expression
            match returnType with
            | AsyncOf t ->
                let buildResult = generateResultBuilder rowBuilder metadata t true
                Expression.Call([| t |], "ExecuteSqlAsync", createCmd, connection, transaction, sql, timeout, assignParams, buildResult) 
            | _ ->
                let buildResult = generateResultBuilder rowBuilder metadata returnType false
                Expression.Call([| returnType |], "ExecuteSql", createCmd, connection, transaction, sql, timeout, assignParams, buildResult) 

        try
            let parameterNames = extractParameterNames commandText
            let (paramExprs, paramDefs, retType) = buildParamDefs paramBuilder t (List.append parameterNames [""])
            let caller = genExecutor createCommand paramDefs retType
            compileCaller paramExprs caller 
        with ex ->
            raise <| CompileTimeException(t, "sql command", commandText, ex)
                
    /// <summary>
    /// Values and functions allowing to customize generation of query execution functions.
    /// </summary>
    type GeneratorConfig =
        {
            /// The function providing a database connection used in generation.
            createConnection: unit -> IDbConnection 
            /// The function creating commands.
            createCommand: IDbConnection -> IDbCommand
            /// The command timeout.
            commandTimeout: int option
            /// Function generating code creating query parameters from function parameters.
            paramBuilder: ParamBuilder -> ParamBuilder
            /// Function generating code creating typed result from data reader.
            rowBuilder: RowBuilder -> RowBuilder
        }
        
    /// <summary>
    /// Provides default configuration.
    /// </summary>
    /// <param name="connectionBuilder">
    /// Database connection - can not be left unspecified.
    /// </param>
    let createDefaultConfig (connectionBuilder: unit -> #IDbConnection) =
        {
            createConnection = (connectionBuilder >> unbox<IDbConnection>)
            createCommand = fun c -> c.CreateCommand()
            commandTimeout = None
            paramBuilder = ParamBuilder.getParamExpressions
            rowBuilder = ResultBuilder.getRowBuilderExpression
        }

    /// <summary>
    /// Generates function executing a sql command.
    /// </summary>
    /// <typeparam name="'t">
    /// The function type.
    /// </typeparam>
    /// <param name="config">
    /// The query generation configuration data.
    /// </param>
    /// <param name="commandText">
    /// The sql statement to be executed.
    /// </param>
    /// <returns>
    /// A function of type 't executing command given by commandText parameter.
    /// </returns>
    let sql<'t> (config: GeneratorConfig) (commandText: string): 't = 
        generateSqlCommandCaller config.createConnection config.createCommand config.commandTimeout config.paramBuilder config.rowBuilder commandText typeof<'t> :?> 't

    let getStoredProcElementTypes returnType =
        match returnType with
        | Tuple elts when elts.Length = 3 ->
            elts.[1], elts.[2]
        | _ -> 
            failwith "StoredProcedure return type must be 3-element tuple."

    let genOutParamsBuilder (outParams: (string * string) list) (outParamsType: Type) = 
        let command = Expression.Parameter(typeof<IDbCommand>)
        let getParamExpr name = Expression.Property(
                                    Expression.Convert(
                                        Expression.Property(
                                            Expression.Property(command, "Parameters"), "Item", Expression.Constant("@" + name)), 
                                            typeof<IDataParameter>), "Value")
        let wrapInOption = if isOption outParamsType
                           then (fun (expr: Expression) (coerce: Expression -> Expression) -> 
                                    Expression.Condition(
                                        Expression.Call(expr, "Equals", Expression.Constant(DBNull.Value) :> Expression), 
                                        Expression.GetNoneUnionCase outParamsType, 
                                        Expression.GetSomeUnionCase outParamsType (coerce expr)) 
                                        :> Expression)
                           else (fun expr coerce -> coerce expr)
        let outParamsType = if isOption outParamsType 
                            then getUnderlyingType outParamsType 
                            else outParamsType       
        let expr = 
            match outParamsType with
            | SimpleType ->
                wrapInOption (getParamExpr (outParams |> Seq.map fst |> Seq.head)) (coerce typeof<obj> outParamsType)
            | Tuple elts -> 
                Expression.NewTuple 
                    (elts |> Seq.mapi (fun i t -> wrapInOption (getParamExpr (fst outParams.[i])) (coerce typeof<obj> t)) 
                          |> Seq.toList)
            | Record fields ->
                Expression.NewRecord
                    (outParamsType, 
                     fields |> Seq.map (fun p -> wrapInOption (getParamExpr p.Name) (coerce typeof<obj> p.PropertyType)) 
                            |> Seq.toList)
            | _ -> Expression.UnitConstant :> Expression
        Expression.Lambda(expr, command) :> Expression
        

    let private generateStoredProcCaller (createConnection: unit -> IDbConnection) (createCommand: IDbConnection-> IDbCommand) (commandTimeout: int option) (paramBuilder: ParamBuilder -> ParamBuilder) (rowBuilder: RowBuilder -> RowBuilder) (procedureName: string) (t: Type): obj =

        let extractProcParamNames (procedureName: string) = 
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
            reader |> rmap (fun r -> (if r.GetString(0).StartsWith("@") then r.GetString(0).Substring(1) else r.GetString(0)), r.GetString(1) <> "IN", r.GetString(2))

        let makeDiagnosticCall (paramDefs: (string * Expression * (obj -> IDbCommand -> int) * obj) list) (outParams: (string * string) list) = 
            use connection = createConnection()
            connection.Open()
            use command = connection.CreateCommand()
            command.CommandText <- procedureName
            command.CommandType <- CommandType.StoredProcedure
            for _, _, buildParam, fakeVal in paramDefs do
                buildParam fakeVal command |> ignore
            for name, dbtype in outParams do
                let param = command.CreateParameter()
                param.ParameterName <- "@" + name
                param.DbType <- getDbTypeEnum dbtype
                param.Direction <- ParameterDirection.Output
                command.Parameters.Add param |> ignore
            command.ExecuteReader(CommandBehavior.SchemaOnly).Dispose()

        let getResultMetadata (paramDefs: (string * Expression * (obj -> IDbCommand -> int) * obj) list) (outParams: (string * string) list) = 
            use connection = createConnection()
            connection.Open()
            use command = connection.CreateCommand()
            command.CommandText <- procedureName
            command.CommandType <- CommandType.StoredProcedure
            for _, _, buildParam, fakeVal in paramDefs do
                buildParam fakeVal command |> ignore
            for name, dbtype in outParams do
                let param = command.CreateParameter()
                param.ParameterName <- "@" + name
                param.DbType <- getDbTypeEnum dbtype
                param.Direction <- ParameterDirection.Output
                command.Parameters.Add param |> ignore

            use schemaOnlyReader = command.ExecuteReader(CommandBehavior.SchemaOnly)
            let getOneResultMetadata () = [0.. schemaOnlyReader.FieldCount - 1] 
                                            |> Seq.map (fun i -> schemaOnlyReader.GetName(i).ToLower(), (i, schemaOnlyReader.GetFieldType(i))) 
                                            |> Map.ofSeq
            let initial = getOneResultMetadata ()
            initial :: List.unfold (fun _ -> if schemaOnlyReader.NextResult() then Some (getOneResultMetadata(), ()) else None) ()

        let genExecutor createCommand paramDefs outParams returnType = 
            let queryParamDefs = paramDefs |> withoutConnectionAndTransaction
            let assignParams = genParamAssigner queryParamDefs outParams      
            let metadata = if returnType <> typeof<unit> && not (Types.isAsyncOf returnType typeof<unit>) // Npgsql hangs on NextResult if no first result exists
                            then 
                                getResultMetadata queryParamDefs outParams
                            else 
                                makeDiagnosticCall queryParamDefs outParams
                                [Map.empty]
            let connection = getConnectionExpr paramDefs
            let transaction = getTransactionExpr paramDefs
            let sql = Expression.Constant(procedureName) :> Expression
            let createCmd = Expression.Constant(createCommand) :> Expression
            let timeout = Expression.Constant(commandTimeout, typeof<int option>) :> Expression
            match returnType with
            | AsyncOf underlyingType ->
                let (outParamsType, resultType) = getStoredProcElementTypes underlyingType
                let buildResult = generateResultBuilder rowBuilder metadata resultType true
                let outParamBuilder = genOutParamsBuilder outParams outParamsType
                Expression.Call([| resultType; outParamsType |], "ExecuteProcedureAsync", createCmd, connection, transaction, sql, timeout, assignParams, buildResult, outParamBuilder)
            | _ -> 
                let (outParamsType, resultType) = getStoredProcElementTypes returnType
                let buildResult = generateResultBuilder rowBuilder metadata resultType false
                let outParamBuilder = genOutParamsBuilder outParams outParamsType
                Expression.Call([| resultType; outParamsType |], "ExecuteProcedure", createCmd, connection, transaction, sql, timeout, assignParams, buildResult, outParamBuilder)

        try
            let parameters = extractProcParamNames procedureName 
            let inParams = parameters 
                            |> Seq.filter (fun (_, isOut, _) -> not isOut) 
                            |> Seq.map (fun (name, _, _) -> name) 
                            |> List.ofSeq
            let outParams = parameters 
                            |> Seq.filter (fun (_, isOut, _) -> isOut)
                            |> Seq.map (fun (name, _, dbtype) -> name, dbtype)
                            |> List.ofSeq
            let (paramExprs, paramDefs, retType) = buildParamDefs paramBuilder t (List.append inParams [""]) 
            let caller = genExecutor createCommand paramDefs outParams retType
            compileCaller paramExprs caller
        with ex ->
            raise <| CompileTimeException(t, "stored procedure", procedureName, ex) 

    /// <summary>
    /// Generates a function executing stored procedure.
    /// </summary>
    /// <typeparam name="'t">
    /// The function type.
    /// </typeparam>
    /// <param name="config">
    /// The query generation configuration data.
    /// </param>
    /// <param name="procedureName">
    /// The stored procedure to be executed.
    /// </param>
    /// <returns>
    /// A function of type 't executing stored procedure given by procedureName parameter.
    /// </returns>
    let proc<'t> (config: GeneratorConfig) (procedureName: string): 't =
        generateStoredProcCaller config.createConnection config.createCommand config.commandTimeout config.paramBuilder config.rowBuilder procedureName typeof<'t> :?> 't

    let test () = ()
