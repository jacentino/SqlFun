namespace SqlFun.NpgSql

open System
open System.Linq.Expressions
open System.Data

open Npgsql
open NpgsqlTypes

open SqlFun
open SqlFun.Types
open SqlFun.GeneratorConfig

[<AutoOpen>]
module Config = 

    /// <summary>
    /// Mapping between .NET types and PostgreSQL types.
    /// </summary>
    /// <param name="t">
    /// .NET type.
    /// </param>
    let getNpgSqlDbType t = 
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
        
    /// <summary>
    /// Parameter builder supporting PostgreSQL array parameters.
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
    let arrayParamBuilder (defaultPB: ParamBuilder) prefix name (expr: Expression) (names: string list) = 
        if expr.Type.IsArray && isSimpleType (expr.Type.GetElementType()) then
            [
                prefix + name,
                expr,
                fun value (command: IDbCommand) ->
                    let param = new NpgsqlParameter()
                    param.ParameterName <- name
                    if value <> null then
                        param.Value <- value
                    param.NpgsqlDbType <- NpgsqlDbType.Array ||| (getNpgSqlDbType (expr.Type.GetElementType()))
                    command.Parameters.Add(param)
                ,
                Array.CreateInstance(expr.Type.GetElementType(), 0) :> obj
            ]
        else
            defaultPB prefix name expr names

    let useArrayParameters config = 
        { config with 
            paramBuilder = arrayParamBuilder <+> config.paramBuilder 
        }

    /// <summary>
    /// Creates default config for PostgreSQL database.
    /// </summary>
    /// <param name="connectionBuilder">
    /// Function creating a database connection.
    /// </param>
    let createDefaultConfig (connectionBuilder: unit -> #IDbConnection) = 
        createDefaultConfig connectionBuilder
        |> useArrayParameters
