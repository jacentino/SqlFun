﻿namespace SqlFun.Net6.Tests

module Common =
    open Microsoft.Data.SqlClient
    open System.Configuration
    open SqlFun
    open SqlFun.Queries
    open SqlFun.MsSql
    

    let createConnection () = 
        let config = ConfigurationManager.OpenExeConfiguration(System.Reflection.Assembly.GetExecutingAssembly().Location)
        new SqlConnection(config.ConnectionStrings.ConnectionStrings.["SqlFunTests"].ConnectionString)

    let generatorConfig = createDefaultConfig createConnection
        
    let run f = AsyncDb.run createConnection f

    let sql commandText = sql generatorConfig commandText