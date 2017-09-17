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

    let rec private getUpdateExpr (positions: Map<string, int>) (record: Expression) (root: Expression): Expression = 
        if FSharpType.IsTuple root.Type
        then
            FSharpType.GetTupleElements root.Type
            |> Seq.mapi (fun i t -> Expression.PropertyOrField(root, "Item" + (i + 1).ToString()) |> getUpdateExpr positions record)                        
            |> Expression.Block :> Expression
        else
            let exprs = FSharpType.GetRecordFields root.Type
                        |> Seq.filter (fun p -> not (isCollectionType p.PropertyType))
                        |> Seq.filter (fun p -> not (isSimpleType p.PropertyType || isSimpleTypeOption p.PropertyType) || positions.ContainsKey p.Name)
                        |> Seq.map (fun p -> if isSimpleType p.PropertyType 
                                                then 
                                                    let valueExpr = convertIfEnum (Expression.Property(root, p))
                                                    let setter = typeof<SqlDataRecord>.GetMethod("Set" + valueExpr.Type.Name)
                                                    Expression.Call (record, setter, Expression.Constant(positions.[p.Name]), valueExpr)
                                                    :> Expression
                                                elif isSimpleTypeOption p.PropertyType
                                                then
                                                    let valueExpr = Expression.Property(root, p)
                                                    let optValueExpr = convertIfEnum (Expression.Property (valueExpr, "Value"))
                                                    let setter = typeof<SqlDataRecord>.GetMethod("Set" + optValueExpr.Type.Name)
                                                    Expression.IfThen(
                                                        Expression.Call(valueExpr.Type.GetMethod("get_IsSome", BindingFlags.Public ||| BindingFlags.Static), valueExpr),
                                                        Expression.Call (record, setter, Expression.Constant(positions.[p.Name]), optValueExpr))
                                                    :> Expression
                                                elif isOption p.PropertyType
                                                then
                                                    let valueExpr = Expression.Property(root, p)
                                                    let optValueExpr = Expression.Property (valueExpr, "Value")
                                                    Expression.IfThen(
                                                        Expression.Call(valueExpr.Type.GetMethod("get_IsSome", BindingFlags.Public ||| BindingFlags.Static), valueExpr),
                                                        getUpdateExpr positions record optValueExpr)
                                                    :> Expression
                                                else  
                                                    Expression.Property(root, p) |> getUpdateExpr positions record)
                        |> List.ofSeq
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
        let positions = metadata |> Seq.mapi (fun i m -> m.Name, i) |> Map.ofSeq
        let updateBlock = Expression.Block([itemVar], [convert; getUpdateExpr positions dataRecParam itemVar])
        let updater = Expression.Lambda< Action<SqlDataRecord, obj> >(updateBlock, dataRecParam, itemParam).Compile()

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

    let sql connectionBuilder commandTimeout paramBuilder commandText = 
        sql connectionBuilder commandTimeout (fun defaultPB -> paramBuilder <| MsSqlParamBuilder connectionBuilder defaultPB) commandText

    let storedproc connectionBuilder commandTimeout paramBuilder procName = 
        storedproc connectionBuilder commandTimeout  (fun defaultPB -> paramBuilder <| MsSqlParamBuilder connectionBuilder defaultPB) procName



