namespace SqlFun.MySql.Tests

module Common =
    open System.Configuration
    open SqlFun
    open SqlFun.Queries
    open SqlFun.GeneratorConfig
    open MySqlConnector

    let createConnection () = new MySqlConnection(ConfigurationManager.ConnectionStrings.["SqlFunTests"].ConnectionString)

    let generatorConfig = createDefaultConfig createConnection        

    let run f = DbAction.run createConnection f

    let runAsync f = AsyncDb.run createConnection f

    let sql commandText = sql generatorConfig commandText

    let proc name = proc generatorConfig name

