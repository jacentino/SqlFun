namespace SqlFun.Oracle

open System
open System.Data
open System.Linq.Expressions
open Oracle.ManagedDataAccess.Client
open SqlFun
open SqlFun.Types
open SqlFun.ParamBuilder

[<AutoOpen>]
module Oracle = 

    /// <summary>
    /// Mapping between .NET types and Oracle types.
    /// </summary>
    /// <param name="t">
    /// .NET type.
    /// </param>
    let getOracleDbType t = 
        if t = typeof<int> then OracleDbType.Int32
        elif t = typeof<Int64> then OracleDbType.Int64
        elif t = typeof<Int32> then OracleDbType.Int32
        elif t = typeof<Int16> then OracleDbType.Int16
        elif t = typeof<bool> then OracleDbType.Boolean
        elif t = typeof<decimal> then OracleDbType.Decimal
        elif t = typeof<DateTime> then OracleDbType.TimeStamp
        elif t = typeof<string> then OracleDbType.Varchar2
        elif t = typeof<double> then OracleDbType.Double
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
                fun (value: obj) (command: IDbCommand) ->
                    let param = new OracleParameter()
                    param.ParameterName <- name
                    if value <> null then
                        param.Value <- value
                    param.OracleDbType <- getOracleDbType (expr.Type.GetElementType())
                    param.Direction <- ParameterDirection.Input
                    let arr = value :?> Array
                    (command :?> OracleCommand).ArrayBindCount <- arr.Length
                    command.Parameters.Add(param)
                ,                
                let array = Array.CreateInstance(expr.Type.GetElementType(), 1)
                array.SetValue(ParamBuilder.getFakeValue <| expr.Type.GetElementType(), 0)
                array :> obj
            ]
        else
            defaultPB prefix name expr names


    /// <summary>
    /// Creates default config for Oracle database.
    /// Activates parameter binding by name and sets Oracle parameter naming rule (':' as a prefix).
    /// </summary>
    /// <param name="createConnection">
    /// Function creating a database connection.
    /// </param>
    let createDefaultConfig createConnection = 
        let lastDefault = Queries.createDefaultConfig createConnection
        { lastDefault with 
            createCommand = fun con -> 
                let cmd = con.CreateCommand()
                (cmd :?> OracleCommand).BindByName <- true
                cmd
            paramNameFinder = ParamBuilder.extractParameterNames ":"
            paramBuilder = arrayParamBuilder <+> lastDefault.paramBuilder
            makeDiagnosticCalls = false
        }

