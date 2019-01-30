namespace SqlFun.Performance.Tests

module DapperSuite = 
    open Data
    open Dapper
    open System
    open System.Linq

    type IdKey = { id: int }

    let readMany() =
        use connection = createConnection()
        connection.Open()
        let result = connection.Query<Post>("select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post2 where blogId = @id", { id = 1 })
        result.First() |> ignore

    let readOneManyTimes() = 
        use connection = createConnection()
        connection.Open()
        let rnd = Random()
        for i in 1..500 do
            let id = rnd.Next(1, 500)
            let result = connection.Query<Post>("select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post2 where id = @id", { id = id})
            result.First() |> ignore
        

    module Async = 

        let readMany() =
            async {
                use connection = createConnection()
                connection.Open()
                let! result = 
                    connection.QueryAsync<Post>("select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post2 where blogId = @id", { id = 1 })
                    |> Async.AwaitTask
                let b = result.First()
                ()
            }

        let readOneManyTimes() = 
            async {
                use connection = createConnection()
                connection.Open()
                let rnd = Random()
                for i in 1..500 do
                    let id = rnd.Next(1, 500)
                    let! result = 
                        connection.QueryAsync<Post>("select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post2 where id = @id", { id = id})
                        |> Async.AwaitTask
                    let p = result.First()
                    ()
            }
        

