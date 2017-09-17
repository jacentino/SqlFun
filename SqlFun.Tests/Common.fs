namespace SqlFun.Tests

module Common =
    open System.Data.SqlClient
    open System.Configuration
    open SqlFun
    open SqlFun.Queries
    open SqlFun.Composite


    let createConnection () = new SqlConnection(ConfigurationManager.ConnectionStrings.["SqlFunTests"].ConnectionString)

    let run f = DataContext.run createConnection f

    let runAsync f = DataContext.runAsync createConnection f

    let sql commandText = sql createConnection None  defaultParamBuilder commandText

    let storedproc name = storedproc createConnection None defaultParamBuilder name

