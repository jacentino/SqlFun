namespace SqlFun.MsDataSqlite.Tests

module Common =
    open Microsoft.Data.Sqlite
    open System.Configuration
    open SqlFun
    open SqlFun.Queries
    open SqlFun.GeneratorConfig
    open SqlFun.Sqlite
    open System.Reflection
    open System.IO

    let connectionString = ConfigurationManager
                                .ConnectionStrings.["SqlFunTests"]
                                .ConnectionString
                                .Replace("{dir}", Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))

    let createConnection () = new SqliteConnection(connectionString)

    let generatorConfig = 
        createDefaultConfig createConnection 
        |> useMicrosoftDataSqlite
        |> representDatesAsStrings

    let run f = DbAction.run createConnection f

    let sql commandText = sql generatorConfig commandText

