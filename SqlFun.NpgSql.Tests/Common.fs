namespace SqlFun.NpgSql.Tests

module Common =
    open Npgsql
    open System.Configuration
    open SqlFun
    open SqlFun.Queries
    open SqlFun.Composite
    open SqlFun.NpgSql


    let createConnection () = new NpgsqlConnection(ConfigurationManager.ConnectionStrings.["SqlFunTests"].ConnectionString)

    let run f = DataContext.run createConnection f

    let runAsync f = DataContext.runAsync createConnection f

    let sql commandText = sql createConnection defaultParamBuilder commandText

    let storedproc name = storedproc createConnection defaultParamBuilder name

