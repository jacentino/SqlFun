namespace SqlFun.NpgSql

[<AutoOpen>]
module NpgSql = 

    open SqlFun.Queries
    open SqlFun.Types
    open System.Linq.Expressions
    open System.Data
    open NpgsqlTypes
    open Npgsql
    open System

    let defaultParamBuilder = defaultParamBuilder

    let private getNpgSqlDbType t = 
        if t = typeof<int> then NpgsqlDbType.Integer
        elif t = typeof<Int64> then NpgsqlDbType.Bigint
        elif t = typeof<Int16> then NpgsqlDbType.Smallint
        elif t = typeof<bool> then NpgsqlDbType.Boolean
        elif t = typeof<decimal> then NpgsqlDbType.Numeric
        elif t = typeof<DateTime> then NpgsqlDbType.Timestamp
        elif t = typeof<Guid> then NpgsqlDbType.Uuid
        elif t = typeof<string> then NpgsqlDbType.Varchar
        elif t = typeof<double> then NpgsqlDbType.Double
        else failwith <| sprintf "Unknown array element type: %O" t
        

    let private NpgsqlParamBuilder defaultPB prefix name (expr: Expression) names = 
        if expr.Type.IsArray && isSimpleType (expr.Type.GetElementType()) then
            [
                prefix + name,
                expr,
                fun value (command: IDbCommand) ->
                    let param = new NpgsqlParameter()
                    param.ParameterName <- "@" + name
                    if value <> null then
                        param.Value <- value
                    param.NpgsqlDbType <- NpgsqlDbType.Array ||| (getNpgSqlDbType (expr.Type.GetElementType()))
                    command.Parameters.Add(param)
                ,
                Array.CreateInstance(expr.Type.GetElementType(), 0) :> obj
            ]
        else
            defaultPB prefix name expr names

    let sql connectionBuilder paramBuilder commandText = 
        sql connectionBuilder (fun defaultPB -> paramBuilder <| NpgsqlParamBuilder defaultPB) commandText

    let storedproc connectionBuilder paramBuilder procName = 
        storedproc connectionBuilder (fun defaultPB -> paramBuilder <| NpgsqlParamBuilder defaultPB) procName


