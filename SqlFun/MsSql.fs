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

    let defaultParamBuilder = defaultParamBuilder

    let private getColumnType propType = 
        if isOption propType
        then getUnderlyingType propType
        elif propType.IsEnum
        then 
            if propType.GetFields() |> Seq.exists (fun f -> f.IsStatic && not (f.GetCustomAttributes<EnumValueAttribute>() |> Seq.isEmpty))
            then typeof<string>
            else typeof<int>
        else propType

    let rec private getAllRecFields (recType: Type) = 
        if FSharpType.IsTuple recType
        then 
            FSharpType.GetTupleElements recType
            |> Seq.collect getAllRecFields
        else
            FSharpType.GetRecordFields recType
            |> Seq.collect (fun p -> if isCollectionType p.PropertyType
                                        then Seq.empty
                                        elif isSimpleType p.PropertyType || isSimpleTypeOption p.PropertyType
                                        then Seq.singleton (p.Name, getColumnType p.PropertyType)
                                        elif isOption p.PropertyType
                                        then getAllRecFields (getUnderlyingType p.PropertyType)
                                        else getAllRecFields p.PropertyType)

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

    let rec private getAllRecValues (recType: Type) (item: obj) = 
        if FSharpType.IsTuple recType
        then 
            FSharpType.GetTupleElements recType
            |> Seq.mapi (fun i t -> getAllRecValues t (recType.GetProperty("Item" + (i + 1).ToString()).GetValue item))
            |> Seq.collect id
        else
            FSharpType.GetRecordFields recType
            |> Seq.collect (fun p -> if isCollectionType p.PropertyType
                                        then Seq.empty
                                        elif isSimpleType p.PropertyType || isSimpleTypeOption p.PropertyType
                                        then Seq.singleton (convertToColumnValue (p.GetValue item))
                                        elif isOption p.PropertyType
                                        then 
                                            if item <> null 
                                            then getAllRecValues p.PropertyType (p.GetValue (item.GetType().GetProperty("Value").GetValue item))
                                            else Seq.singleton item                                        
                                        else getAllRecValues p.PropertyType (p.GetValue item))

    let private toDataTable (items: obj, itemType: Type): DataTable = 
        let itemSeq = items :?> obj seq
        let table = new DataTable()
        table.TableName <- itemType.Name
        getAllRecFields(itemType) |> Seq.iter (table.Columns.Add >> ignore)
        itemSeq |> Seq.iter (getAllRecValues itemType >> Array.ofSeq >> table.Rows.Add >> ignore)
        table

    let private MsSqlParamBuilder defaultPB prefix name (expr: Expression) names = 
        if isCollectionType expr.Type && isComplexType (getUnderlyingType expr.Type)
        then
            let itemType = getUnderlyingType expr.Type
            [
                prefix + name,
                expr,
                fun value (command: IDbCommand) ->
                    let param = new SqlClient.SqlParameter()
                    param.ParameterName <- "@" + name
                    if value <> null then
                        param.Value <- toDataTable (value, itemType)
                    param.SqlDbType <- SqlDbType.Structured
                    param.TypeName <- itemType.Name
                    command.Parameters.Add(param)
                ,
                [] :> obj
            ]       
        else
            defaultPB prefix name expr names

    let sql connectionBuilder paramBuilder commandText = 
        sql connectionBuilder (fun defaultPB -> paramBuilder <| MsSqlParamBuilder defaultPB) commandText

    let storedproc connectionBuilder paramBuilder procName = 
        storedproc connectionBuilder (fun defaultPB -> paramBuilder <| MsSqlParamBuilder defaultPB) procName



