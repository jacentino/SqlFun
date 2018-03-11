namespace SqlFun.Tests

module Common =
    open System.Data.SqlClient
    open System.Configuration
    open SqlFun
    open SqlFun.Queries
    open SqlFun.Composite


    let createConnection () = new SqlConnection(ConfigurationManager.ConnectionStrings.["SqlFunTests"].ConnectionString)

    let run f = DataContext.run createConnection f

    let createDC() = DataContext.create <| createConnection()

    let runAsync f = DataContext.runAsync createConnection f

    let sql commandText = sql createConnection None  defaultParamBuilder commandText

    let storedproc name = storedproc createConnection None defaultParamBuilder name

    let mapFst f (x, y) = f x, y
    let mapSnd f (x, y) = x, f y
