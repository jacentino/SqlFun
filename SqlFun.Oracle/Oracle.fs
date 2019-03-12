namespace SqlFun.Oracle

open SqlFun
open Oracle.ManagedDataAccess.Client

[<AutoOpen>]
module Oracle = 

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
        }

