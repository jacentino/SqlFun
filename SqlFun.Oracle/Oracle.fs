namespace SqlFun.Oracle

open System
open System.Data
open System.Linq.Expressions
open Oracle.ManagedDataAccess.Client
open SqlFun
open SqlFun.Types
open SqlFun.ParamBuilder
open SqlFun.GeneratorConfig

[<AutoOpen>]
module Config = 

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
    /// Parameter builder supporting Oracle array parameters.
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

    let private getDbTypeEnum name = 
        if name = "REF CURSOR" then
            OracleDbType.RefCursor :> obj
        else
            match name with
            | "INT"
            | "INTEGER" -> DbType.Int32
            | "SMALLINT" -> DbType.Int16
            | "NUMBER" -> DbType.Decimal
            | "VARCHAR" 
            | "VARCHAR2"
            | "NVARCHAR"
            | "NVARCHAR2" -> DbType.String
            | "DATE" -> DbType.Date
            | "TIMESTAMP" -> DbType.DateTime
            | "BIT" -> DbType.Boolean
            | _ -> DbType.String
            :> obj

    /// <summary>
    /// Reads parameter names and directions from information schema.
    /// </summary>
    /// <param name="createConnection">
    /// Function creating database connection.
    /// </param>
    /// <param name="createCommand">
    /// Function creating database command.
    /// </param>
    /// <param name="procedureName">
    /// The name of the procedure.
    /// </param>
    let extractProcParamNames (createConnection: unit -> IDbConnection) (createCommand: IDbConnection -> IDbCommand) (procedureName: string) = 
        use connection = createConnection()
        connection.Open()
        use command = createCommand(connection)
        command.CommandText <- "select argument_name, in_out, data_type
                                from user_arguments 
                                where upper(object_name) = :procedure_name
                                order by position"
        let param = command.CreateParameter()
        param.ParameterName <- ":procedure_name"
        param.Value <- procedureName.ToUpper().Split('.') |> Seq.last
        command.Parameters.Add(param) |> ignore
        use reader = command.ExecuteReader()
        [ while reader.Read() do 
            yield reader.GetString(0), reader.GetString(1) <> "IN", getDbTypeEnum(reader.GetString(2))
        ]



    /// <summary>
    /// Adds support for Oracle array parameters.
    /// </summary>
    /// <param name="config">
    /// The initial config.
    /// </param>
    let useArrayParameters config =
        { config with
            paramBuilder = arrayParamBuilder <+> config.paramBuilder
        }

    /// <summary>
    /// Adds settings needed by generator to work with Oracle databases.
    /// </summary>
    /// <remarks>
    /// * : as a parameter prefix
    /// * Oracle specific information schema reader
    /// * turned off return parameter adding
    /// * turned off execution of schema only commands 
    ///   for queries, that doesn't create results
    /// <remarks>
    /// <param name="config">
    /// The initial config.
    /// </param>
    let addOraclePrerequisites config = 
        let createCommand (con: IDbConnection) = 
            let cmd = con.CreateCommand()
            (cmd :?> OracleCommand).BindByName <- true
            cmd
        { config with 
            createCommand = createCommand
            paramNameFinder = extractParameterNames ":"
            procParamFinder = extractProcParamNames config.createConnection config.createCommand
            makeDiagnosticCalls = false
            addReturnParameter = false
        }
        

    /// <summary>
    /// Adds support for collections of basic types as query parameters.
    /// Subsequent collection items are injected as comma separated parameters in a command text.
    /// </summary>
    /// <param name="config">
    /// The initial config.
    /// </param>
    let useCollectionParameters config = 
        { config with 
            paramBuilder = (listParamBuilder Types.isSimpleType ":") <+> config.paramBuilder
        }

    /// <summary>
    /// Creates default config for Oracle database.
    /// Activates parameter binding by name and sets Oracle parameter naming rule (':' as a prefix).
    /// </summary>
    /// <param name="createConnection">
    /// Function creating a database connection.
    /// </param>
    let createDefaultConfig createConnection = 
        createDefaultConfig createConnection
        |> addOraclePrerequisites
        |> useArrayParameters

