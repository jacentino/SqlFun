namespace SqlFun.MySql.Connector

open SqlFun
open SqlFun.Types
open SqlFun.ExpressionExtensions
open System
open Microsoft.FSharp.Reflection
open System.Linq.Expressions
open System.Reflection
open System.Data
open MySqlConnector

type BulkCopy<'Rec>() = 

    static let rec getFields (t: Type) = 
        match t with
        | Tuple elts -> [ for e in elts do yield! getFields e]
        | _ ->
            [ for p in FSharpType.GetRecordFields t do
                if not (isCollectionType p.PropertyType) then
                    yield!
                        match p.PropertyType with
                        | SimpleType -> [ p.Name, p.PropertyType, false ]
                        | OptionOf t when isSimpleType t -> [ p.Name, t, true ]
                        | pt -> getFields pt
            ]

    static let dataTable =         
        let dt = new DataTable()
        for name, dataType, nullable in getFields typeof<'Rec> do
            let col = dt.Columns.Add()    
            col.ColumnName <- name
            col.DataType <- 
                if dataType.IsEnum then 
                    typeof<int> 
                else 
                    dataType
            col.AllowDBNull <- nullable
        dt

    static let convertIfEnum (expr: Expression) = 
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


    static let itemProperty = typeof<DataRow>.GetProperty("Item", Array.singleton typeof<int> )
    
    static let dataRowFieldAssigner (dataRow: Expression, name: string, value: Expression) = 
        let property = Expression.Property(dataRow, itemProperty, Expression.Constant(dataTable.Columns.[name].Ordinal))        
        Expression.Assign (property, Expression.Convert(value, typeof<obj>))

    static let getIsSome (t: Type) = t.GetMethod("get_IsSome", BindingFlags.Public ||| BindingFlags.Static)

    static let rec getWriteExpr (dataRow: Expression) (enclosingOptionIsSome: Expression) (root: Expression) = 
        match root.Type with
        | Tuple elts -> 
            elts
            |> Seq.mapi (fun i t -> Expression.PropertyOrField(root, "Item" + (i + 1).ToString()) |> getWriteExpr enclosingOptionIsSome dataRow)
            |> Expression.Block :> Expression
        | _ ->
            let exprs = 
                [ for p in FSharpType.GetRecordFields root.Type do
                    if not (isCollectionType p.PropertyType) then
                        yield
                            match p.PropertyType with
                            | SimpleType -> 
                                let valueExpr = convertIfEnum (Expression.Property(root, p))
                                Expression.IfThen(
                                    enclosingOptionIsSome,
                                    dataRowFieldAssigner (dataRow, p.Name, valueExpr))
                                :> Expression
                            | SimpleTypeOption ->
                                let valueExpr = Expression.Property(root, p)
                                let optValueExpr = convertIfEnum (Expression.Property (valueExpr, "Value"))
                                Expression.IfThen(
                                    Expression.And(enclosingOptionIsSome, Expression.Call(getIsSome valueExpr.Type, valueExpr)),
                                    dataRowFieldAssigner (dataRow, p.Name, optValueExpr))
                                :> Expression
                            | OptionOf _ ->
                                let valueExpr = Expression.Property(root, p)
                                let optValueExpr = Expression.Property (valueExpr, "Value")
                                let isSome = Expression.And(enclosingOptionIsSome, Expression.Call(getIsSome valueExpr.Type, valueExpr))
                                getWriteExpr dataRow isSome optValueExpr
                            | _ ->  
                                Expression.Property(root, p) |> getWriteExpr enclosingOptionIsSome dataRow
                ]
            if exprs.IsEmpty 
            then Expression.UnitConstant :> Expression
            else exprs |> Expression.Block :> Expression

    static let recordWriter = 
        let writerParam = Expression.Parameter(typeof<DataRow>)
        let recordParam = Expression.Parameter(typeof<'Rec>)
        let writeExpr = getWriteExpr writerParam (Expression.Constant(true)) recordParam
        Expression.Lambda<Action<DataRow, 'Rec>>(writeExpr, writerParam, recordParam).Compile()

    /// <summary>
    /// Sends collection of records to the server.
    /// </summary>
    /// <param name="records">
    /// The data to be sent.
    /// </param>
    /// <param name="ctx">
    /// SqlFun data context.
    /// </param>
    static member WriteToServer (records: 'Rec seq) (ctx: IDataContext): MySqlBulkCopyResult Async =
        let dataRow = dataTable.NewRow()
        async {
            let rows = 
                seq {                                
                    for r in records do
                        for i in 0..dataTable.Columns.Count - 1 do
                            dataRow.[i] <- DBNull.Value
                        recordWriter.Invoke(dataRow, r)
                        yield dataRow
                }
            let bulkCopy = new MySqlBulkCopy(ctx.Connection :?> MySqlConnection, ctx.Transaction |> Option.defaultValue null :?> MySqlTransaction)
            bulkCopy.DestinationTableName <- typeof<'Rec>.Name
            return bulkCopy.WriteToServer(rows, dataTable.Columns.Count)
        }


