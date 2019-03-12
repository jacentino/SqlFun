namespace SqlFun.Oracle.Tests

module Common =
    open Oracle.ManagedDataAccess.Client
    open System.Configuration
    open SqlFun
    open SqlFun.Queries
    open SqlFun.Oracle
    open System.Data


    let createConnection () = new OracleConnection(ConfigurationManager.ConnectionStrings.["SqlFunTests"].ConnectionString)

    let generatorConfig = createDefaultConfig createConnection

    let run f = DbAction.run createConnection f

    let runAsync f = AsyncDb.run createConnection f

    let sql commandText = sql generatorConfig commandText

    let proc name = proc generatorConfig name

