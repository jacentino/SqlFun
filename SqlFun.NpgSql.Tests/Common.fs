﻿namespace SqlFun.NpgSql.Tests

module Common =
    open Npgsql
    open System.Configuration
    open SqlFun
    open SqlFun.Queries
    open SqlFun.NpgSql

    let createConnection () = new NpgsqlConnection(ConfigurationManager.ConnectionStrings.["SqlFunTests"].ConnectionString)

    let generatorConfig = createDefaultConfig createConnection

    let run f = DbAction.run createConnection f

    let runAsync f = AsyncDb.run createConnection f

    let sql commandText = sql generatorConfig commandText

    let proc name = proc generatorConfig name

