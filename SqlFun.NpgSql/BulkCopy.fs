namespace SqlFun.NpgSql

open SqlFun
open SqlFun.Types
open SqlFun.ExpressionExtensions
open Npgsql
open System
open Microsoft.FSharp.Reflection
open System.Linq.Expressions
open System.Reflection

/// <summary>
/// Strongly typed bulk copy implementation.
/// </summary>
type BulkCopy<'Rec>() = 

    static let rec getFieldNames (t: Type) = 
        match t with
        | Tuple elts -> [ for e in elts do yield! getFieldNames e]
        | _ ->
            [ for p in FSharpType.GetRecordFields t do
                if not (isCollectionType p.PropertyType) then
                    yield!
                        match p.PropertyType with
                        | SimpleType -> [ p.Name.ToLower() ]
                        | SimpleTypeOption _ -> [ p.Name.ToLower() ]
                        | pt -> getFieldNames pt
            ]

    static let copyCommand = sprintf "COPY %s (%s) FROM STDIN (FORMAT BINARY)" 
                                (typeof<'Rec>.Name.ToLower())
                                (getFieldNames typeof<'Rec> |> String.concat ", ")

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


    static let writeMethod = typeof<NpgsqlBinaryImporter>.GetMethods() |> Array.find (fun m -> m.GetParameters().Length = 2);
    static let writeNullMethod = typeof<NpgsqlBinaryImporter>.GetMethod("WriteNull");
    
    static let getIsSome (t: Type) = t.GetMethod("get_IsSome", BindingFlags.Public ||| BindingFlags.Static)

    static let rec getWriteExpr (writer: Expression) (enclosingOptionIsSome: Expression) (root: Expression) = 
        match root.Type with
        | Tuple elts -> 
            elts
            |> Seq.mapi (fun i t -> Expression.PropertyOrField(root, "Item" + (i + 1).ToString()) |> getWriteExpr enclosingOptionIsSome writer)
            |> Expression.Block :> Expression
        | _ ->
            let exprs = 
                [ for p in FSharpType.GetRecordFields root.Type do
                    if not (isCollectionType p.PropertyType) then
                        yield
                            match p.PropertyType with
                            | SimpleType -> 
                                let valueExpr = convertIfEnum (Expression.Property(root, p))
                                let npgType = Expression.Constant(getNpgSqlDbType valueExpr.Type)
                                let write = writeMethod.MakeGenericMethod(valueExpr.Type)
                                Expression.IfThenElse(
                                    enclosingOptionIsSome,
                                    Expression.Call (writer, write, valueExpr, npgType),
                                    Expression.Call (writer, writeNullMethod))
                                :> Expression
                            | SimpleTypeOption ->
                                let valueExpr = Expression.Property(root, p)
                                let optValueExpr = convertIfEnum (Expression.Property (valueExpr, "Value"))
                                let npgType = Expression.Constant(getNpgSqlDbType optValueExpr.Type)
                                let write = writeMethod.MakeGenericMethod(optValueExpr.Type)
                                Expression.IfThenElse(
                                    Expression.And(enclosingOptionIsSome, Expression.Call(getIsSome valueExpr.Type, valueExpr)),
                                    Expression.Call (writer, write, optValueExpr, npgType),
                                    Expression.Call(writer, writeNullMethod))
                                :> Expression
                            | OptionOf _ ->
                                let valueExpr = Expression.Property(root, p)
                                let optValueExpr = Expression.Property (valueExpr, "Value")
                                let isSome = Expression.And(enclosingOptionIsSome, Expression.Call(getIsSome valueExpr.Type, valueExpr))
                                getWriteExpr writer isSome optValueExpr
                            | _ ->  
                                Expression.Property(root, p) |> getWriteExpr enclosingOptionIsSome writer
                ]
            if exprs.IsEmpty 
            then Expression.UnitConstant :> Expression
            else exprs |> Expression.Block :> Expression

    static let recordWriter = 
        let writerParam = Expression.Parameter(typeof<NpgsqlBinaryImporter>)
        let recordParam = Expression.Parameter(typeof<'Rec>)
        let writeExpr = getWriteExpr writerParam (Expression.Constant(true)) recordParam
        Expression.Lambda<Action<NpgsqlBinaryImporter, 'Rec>>(writeExpr, writerParam, recordParam).Compile()

    /// <summary>
    /// Sends collection of records to the server.
    /// </summary>
    /// <param name="records">
    /// The data to be sent.
    /// </param>
    /// <param name="ctx">
    /// SqlFun data context.
    /// </param>
    static member WriteToServer (records: 'Rec seq) (ctx: IDataContext): unit Async =
        let npgcon = ctx.Connection :?> NpgsqlConnection
        async {
            use writer = npgcon.BeginBinaryImport(copyCommand)
            for r in records do
                writer.StartRow()
                recordWriter.Invoke(writer, r)
            writer.Complete()
        }
