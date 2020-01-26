namespace SqlFun


module MsSql = 

    open System
    open System.Data
    open System.Reflection
    open Microsoft.SqlServer.Server
    open Microsoft.FSharp.Reflection
    open SqlFun.Types
    open System.Linq.Expressions
    open SqlFun.ExpressionExtensions
    open SqlFun.GeneratorConfig

    let private convertIfEnum (expr: Expression) = 
        if expr.Type.IsEnum
        then
            let exprAsInt = Expression.Convert(expr, typeof<int>) :> Expression
            let values = getEnumValues expr.Type 
            if values |> Seq.exists (fun (e, v) -> e <> v)
            then
                let intValues = values |> List.map (fun (e, v) -> Convert.ChangeType(e, typeof<int>), v)    
                let (_, firstVal) = List.head values
                let firstValExpr = Expression.Constant (firstVal) :> Expression
                let comparer e = 
                    Expression.Call(Expression.Constant(e), "Equals", exprAsInt) :> Expression
                intValues 
                |> Seq.fold (fun cexpr (e, v) -> Expression.Condition(comparer e, Expression.Constant(v), cexpr) :> Expression) firstValExpr
            else
                exprAsInt
        else
            expr

    
    let private getIsSome (t: Type) = t.GetMethod("get_IsSome", BindingFlags.Public ||| BindingFlags.Static)

    let private getSetter (t: Type) = 
        typeof<SqlDataRecord>.GetMethod("Set" + t.Name)

    let private getOptValue expr = Expression.Property (expr, "Value")

    let rec private getUpdateExpr (positions: Map<string, int>) (record: Expression) (root: Expression): Expression = 
        match root.Type with
        | Tuple elts -> 
            elts
            |> Seq.mapi (fun i t -> Expression.PropertyOrField(root, "Item" + (i + 1).ToString()) |> getUpdateExpr positions record)                        
            |> Expression.Block :> Expression
        | _ ->
            let exprs = 
                [ for p in FSharpType.GetRecordFields root.Type do
                    if not (isCollectionType p.PropertyType) && 
                       not (isSimpleType p.PropertyType || isSimpleTypeOption p.PropertyType) || positions.ContainsKey p.Name 
                    then
                        yield match p.PropertyType with
                              | t when t = typeof<byte[]> ->
                                    let valueExpr = Expression.Property(root, p)
                                    let setter = typeof<SqlDataRecord>.GetMethod("SetBytes")
                                    Expression.Call (record, setter, 
                                        Expression.Constant(positions.[p.Name]), 
                                        Expression.Constant(int64(0)),
                                        valueExpr,
                                        Expression.Constant(0),
                                        Expression.ArrayLength(valueExpr)) 
                                    :> Expression
                              | SimpleType -> 
                                    let valueExpr = convertIfEnum (Expression.Property(root, p))
                                    let setter = getSetter valueExpr.Type
                                    Expression.Call (record, setter, Expression.Constant(positions.[p.Name]), valueExpr) :> Expression
                              | SimpleTypeOption ->
                                    let valueExpr = Expression.Property(root, p)
                                    let optValueExpr = convertIfEnum (getOptValue valueExpr)
                                    let setter = getSetter optValueExpr.Type
                                    Expression.IfThen(
                                        Expression.Call(getIsSome valueExpr.Type, valueExpr),
                                            Expression.Call (record, setter, Expression.Constant(positions.[p.Name]), optValueExpr))
                                    :> Expression
                              | OptionOf _ ->
                                    let valueExpr = Expression.Property(root, p)
                                    let optValueExpr = getOptValue valueExpr
                                    Expression.IfThen(
                                        Expression.Call(getIsSome valueExpr.Type, valueExpr), 
                                        getUpdateExpr positions record optValueExpr)
                                    :> Expression
                              | _ ->  
                                    Expression.Property(root, p) |> getUpdateExpr positions record
                ]
            if exprs.IsEmpty 
            then Expression.UnitConstant :> Expression
            else exprs |> Expression.Block :> Expression
                                                                                    
    let private createMetaData name typeName (maxLen: int64) (precision: byte) (scale: byte) = 
        let dbType = Enum.Parse(typeof<SqlDbType>, typeName, true) :?> SqlDbType
        match dbType with
        | SqlDbType.Char 
        | SqlDbType.NChar 
        | SqlDbType.VarChar 
        | SqlDbType.NVarChar -> SqlMetaData(name, dbType, Math.Min(maxLen, 4000L))
        | SqlDbType.Binary 
        | SqlDbType.VarBinary -> SqlMetaData(name, dbType, maxLen)
        | SqlDbType.Decimal 
        | SqlDbType.Money 
        | SqlDbType.SmallMoney -> SqlMetaData(name, dbType, precision, scale)
        | _ -> SqlMetaData(name, dbType)

    let private getMetaData (t: Type) (connection: IDbConnection) =
        use command = connection.CreateCommand()
        command.CommandText <- "select c.name, t.name as typeName, c.max_length, c.precision, c.scale, c.is_nullable
                                from sys.table_types tt 
	                                join sys.columns c on c.object_id = tt.type_table_object_id
	                                join sys.types t on t.system_type_id = c.system_type_id and t.user_type_id = c.user_type_id
                                where tt.name = @name"
        let param = command.CreateParameter()
        param.ParameterName <- "@name"
        param.Value <- t.Name.Split('`').[0] // Allows for use of resolved generic types.
        command.Parameters.Add(param) |> ignore
        use reader = command.ExecuteReader()
        [| 
            while reader.Read() do
                yield createMetaData (reader.GetString 0) (reader.GetString 1) (int64(reader.GetInt16 2)) (reader.GetByte 3) (reader.GetByte  4)
        |]

    let private getRecSequenceBuilder (connection: IDbConnection) (itemType: Type) = 
        let metadata = getMetaData itemType connection
        let itemParam = Expression.Parameter(typeof<obj>, "itemParam")
        let dataRecParam = Expression.Parameter(typeof<SqlDataRecord>, "dataRecParam")
        let itemVar = Expression.Variable(itemType, "itemVar")
        let convert = Expression.Assign(itemVar, Expression.Convert(itemParam, itemType)) :> Expression
        let positions = metadata |> Seq.mapi (fun i m -> m.Name, i) |> Map.ofSeq
        let updateBlock = Expression.Block([itemVar], [convert; getUpdateExpr positions dataRecParam itemVar])
        let updater = Expression.Lambda<Action<SqlDataRecord, obj> >(updateBlock, dataRecParam, itemParam).Compile()

        fun (items: obj) ->
            let record = SqlDataRecord(metadata)
            let itemSeq = items :?> obj seq
            if itemSeq |> Seq.length = 0 then null
            else
                seq {
                    for item in itemSeq do
                        for i in 0..record.FieldCount - 1 do
                            record.SetDBNull(i)
                        updater.Invoke (record, item)
                        yield record
                }

    /// <summary>
    /// Parameter builder supporting user defined table type parameters.
    /// Allows to combine bulk operations with usual queries.
    /// </summary>
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
    let tableValueParamBuilder (connectionBuilder: unit -> #IDbConnection) defaultPB prefix name (expr: Expression) names = 
        match expr.Type with 
        | CollectionOf itemType when isComplexType itemType ->
            let typeName = itemType.Name.Split('`').[0] // Allows for use of resolved generic types.
            use connection = connectionBuilder()
            connection.Open()
            let toSqlDataRecords = getRecSequenceBuilder connection itemType
            [
                prefix + name,
                expr,
                fun value (command: IDbCommand) ->
                    let param = new SqlClient.SqlParameter()
                    param.ParameterName <- name
                    if value <> null then
                        param.Value <- toSqlDataRecords value
                    param.SqlDbType <- SqlDbType.Structured
                    param.TypeName <- typeName
                    command.Parameters.Add(param)
                ,
                null
            ]       
        | _ ->
            defaultPB prefix name expr names

    /// <summary>
    /// Adds support for user defined table type parameters.
    /// Allows to combine bulk operations with usual queries.
    /// </summary>
    /// <param name="config">
    /// The initial config.
    /// </param>
    let useTableValuedParameters (config: SqlFun.GeneratorConfig) = 
        { config with
            paramBuilder = (tableValueParamBuilder config.createConnection) <+> config.paramBuilder 
        }

    /// <summary>
    /// Provides default configuration for MsSql with TVP support.
    /// </summary>
    /// <param name="connectionBuilder">
    /// Function creating database connection.
    /// </param>
    let createDefaultConfig connectionBuilder = 
        createDefaultConfig connectionBuilder
        |> useTableValuedParameters
