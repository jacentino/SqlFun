namespace SqlFun.NpgSql.Tests

module Common =
    open Npgsql
    open System.Configuration
    open SqlFun
    open SqlFun.Queries
    open SqlFun.Composite
    open SqlFun.NpgSql


    let createConnection () = new NpgsqlConnection(ConfigurationManager.ConnectionStrings.["SqlFunTests"].ConnectionString)

    let run f = DbAction.run createConnection f

    let runAsync f = AsyncDb.run createConnection f

    let sql commandText = sql createConnection None defaultParamBuilder defaultRowBuilder commandText

    let storedproc name = storedproc createConnection None defaultParamBuilder defaultRowBuilder name

