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

    let defaultParamBuilder = defaultParamBuilder

    let private getEnumValue (value: obj) = 
        value.GetType().GetFields()
            |> Seq.filter (fun f -> f.IsStatic)
            |> Seq.map (fun f -> f.GetValue(null), f.GetCustomAttributes<EnumValueAttribute>() |> Seq.map (fun a -> a.Value) |> Seq.tryHead)
            |> Seq.map (fun (e, vopt) -> e, match vopt with Some x -> x | None -> e)
            |> Seq.filter (fun (e, v) -> e = value)
            |> Seq.map snd
            |> Seq.head

    let private convertToColumnValue value = 
        if value <> null && isOption (value.GetType())
        then value.GetType().GetProperty("Value").GetValue value
        elif value <> null && value.GetType().IsEnum
        then getEnumValue value
        else value

    let private composeAll (f: obj -> obj) (l: (string * (obj -> obj)) seq) = 
        l |> Seq.map (fun (n, f1) -> n, fun item -> let x = f item in if x = null then null else f1 x)

    let rec private getAccessors (recType: Type): (string * (obj -> obj)) seq = 
        if FSharpType.IsTuple recType
        then 
            FSharpType.GetTupleElements recType
            |> Seq.mapi (fun i t -> composeAll (fun item -> recType.GetProperty("Item" + (i + 1).ToString()).GetValue item) (getAccessors t))
            |> Seq.collect id
        else
            FSharpType.GetRecordFields recType
            |> Seq.collect (fun p -> if isCollectionType p.PropertyType
                                        then Seq.empty
                                        elif isSimpleType p.PropertyType || isSimpleTypeOption p.PropertyType
                                        then Seq.singleton (p.Name, fun item -> convertToColumnValue (p.GetValue item))
                                        elif isOption p.PropertyType
                                        then 
                                            let underlyingType = p.PropertyType.GetGenericArguments().[0]
                                            composeAll 
                                                (fun item -> 
                                                    let opt = p.GetValue item
                                                    let value = p.PropertyType.GetProperty("Value").GetValue opt
                                                    value)
                                                (getAccessors underlyingType)
                                        else 
                                            composeAll  (fun item -> p.GetValue item) (getAccessors p.PropertyType))


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
        let record = SqlDataRecord(metadata)
        let accessors = getAccessors itemType |> Map.ofSeq
        fun (items: obj) ->
            let itemSeq = items :?> obj seq
            if itemSeq |> Seq.length = 0 then null
            else
                seq {
                    for item in itemSeq do
                        for i in 0..metadata.Length - 1 do
                            let v = accessors.[metadata.[i].Name] item
                            record.SetValue(i, v) 
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



