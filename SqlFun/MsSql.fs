namespace SqlFun

module MsSql =
    
    open SqlFun.Queries
    open SqlFun.Types
    open System.Linq.Expressions
    open System.Data
    open System
    open Microsoft.FSharp.Reflection
    open System.Reflection
    open Future
    open Microsoft.SqlServer.Server
    open System.Collections.Concurrent
    open SqlFun.ExpressionExtensions

    let defaultParamBuilder = defaultParamBuilder







    let private getEnumValues (enumType: Type) = 
        enumType.GetFields()
            |> Seq.filter (fun f -> f.IsStatic)
            |> Seq.map (fun f -> f.GetValue(null), f.GetCustomAttributes<EnumValueAttribute>() |> Seq.map (fun a -> a.Value) |> Seq.tryHead)
            |> Seq.map (fun (e, vopt) -> e, match vopt with Some x -> x | None -> e)
            |> List.ofSeq

    let private getConcreteMethod concreteType methodName = 
        let m = typeof<Toolbox>.GetMethod(methodName, BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
        m.MakeGenericMethod([| concreteType |])


    let private convertIfEnum (expr: Expression) = 
        if expr.Type.IsEnum
        then
            let exprAsObj = Expression.Convert(expr, typeof<obj>) :> Expression
            let values = getEnumValues expr.Type
            if values |> Seq.exists (fun (e, v) -> e <> v)
            then            
                let comparer e = Expression.Call(Expression.Constant(e), "Equals", exprAsObj) :> Expression
                values 
                    |> Seq.fold (fun cexpr (e, v) -> Expression.Condition(comparer e, Expression.Constant(v, typeof<obj>), cexpr) :> Expression) exprAsObj
            else
                exprAsObj
        else
            expr

    let private convertEnumOption (expr: Expression) =
        let param = Expression.Parameter(getUnderlyingType expr.Type, "v")
        if param.Type.IsEnum
        then 
            Expression.Call(getConcreteMethod param.Type "mapOption", Expression.Lambda(convertIfEnum (param), param), expr) :> Expression
        else 
            expr

    let private convertAsNeeded (expr: Expression) =
        if isOption expr.Type 
        then 
            let converter = convertEnumOption expr
            Expression.Call(getConcreteMethod (getUnderlyingType converter.Type) "unpackOption", converter) :> Expression
        else
            Expression.Convert(convertIfEnum expr, typeof<obj>) :> Expression







    let rec private getAccessors (root: Expression): (string * Expression) seq = 
        if FSharpType.IsTuple root.Type
        then
            FSharpType.GetTupleElements root.Type
            |> Seq.mapi (fun i t -> Expression.PropertyOrField(root, "Item" + (i + 1).ToString()) |> getAccessors)
            |> Seq.collect id
        else
            FSharpType.GetRecordFields root.Type
            |> Seq.collect (fun p -> if isCollectionType p.PropertyType
                                        then Seq.empty
                                        elif isSimpleType p.PropertyType || isSimpleTypeOption p.PropertyType
                                        then Seq.singleton (p.Name, convertAsNeeded (Expression.Property(root, p)))
                                        elif isOption p.PropertyType
                                        then
                                            let propValue = Expression.Property(root, p)
                                            let unwrappedValue = Expression.PropertyOrField(propValue, "Value")     
                                            let nullConst = Expression.Constant(null, p.PropertyType)
                                            getAccessors unwrappedValue
                                            |> Seq.map (fun (name, expr) -> 
                                                            name, 
                                                            Expression.Condition(
                                                                Expression.NotEqual(propValue, nullConst), 
                                                                expr, 
                                                                Expression.Constant(null, expr.Type)) :> Expression)
                                        else
                                            Expression.Property(root, p) |> getAccessors)
                                            
                                                                                    
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
        param.Value <- t.Name
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
        let accessors = getAccessors itemVar |> Map.ofSeq
        let updates = metadata 
                        |> Seq.mapi (fun i m -> 
                                        let indexExpr = Expression.Constant i
                                        let valueExpr = accessors.[m.Name]
                                        let setter = typeof<SqlDataRecord>.GetMethod("SetValue" (*+ valueExpr.Type.Name*))
                                        Expression.Call(dataRecParam, setter, indexExpr, valueExpr) :> Expression)
                        |> List.ofSeq
        let updateBlock = Expression.Block([itemVar], convert :: updates)
        let updaterExpr = Expression.Lambda< Action<SqlDataRecord, obj> >(updateBlock, dataRecParam, itemParam)
        let updater = 
            try
                updaterExpr.Compile()
            with ex -> 
                Console.WriteLine(ex)
                raise (Exception("Compile failed", ex))

        fun (items: obj) ->
            let record = SqlDataRecord(metadata)
            let itemSeq = items :?> obj seq
            if itemSeq |> Seq.length = 0 then null
            else
                seq {
                    for item in itemSeq do
                        updater.Invoke (record, item)
                        yield record
                }



    let private MsSqlParamBuilder (connectionBuilder: unit -> #IDbConnection) defaultPB prefix name (expr: Expression) names = 
        if isCollectionType expr.Type && isComplexType (getUnderlyingType expr.Type)
        then
            let itemType = getUnderlyingType expr.Type
            let typeName = itemType.Name
            use connection = connectionBuilder()
            connection.Open()
            let toSqlDataRecords = getRecSequenceBuilder connection itemType
            [
                prefix + name,
                expr,
                fun value (command: IDbCommand) ->
                    let param = new SqlClient.SqlParameter()
                    param.ParameterName <- "@" + name
                    if value <> null then
                        param.Value <- toSqlDataRecords value
                    param.SqlDbType <- SqlDbType.Structured
                    param.TypeName <- typeName
                    command.Parameters.Add(param)
                ,
                null
            ]       
        else
            defaultPB prefix name expr names

    let sql connectionBuilder paramBuilder commandText = 
        sql connectionBuilder (fun defaultPB -> paramBuilder <| MsSqlParamBuilder connectionBuilder defaultPB) commandText

    let storedproc connectionBuilder paramBuilder procName = 
        storedproc connectionBuilder (fun defaultPB -> paramBuilder <| MsSqlParamBuilder connectionBuilder defaultPB) procName



