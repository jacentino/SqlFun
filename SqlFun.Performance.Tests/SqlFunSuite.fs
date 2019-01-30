namespace SqlFun.Performance.Tests

module SqlFunSuite = 
    open SqlFun
    open SqlFun.Queries
    open Data
    open System

    let run f = DbAction.run createConnection f

    let sql commandText = sql generatorConfig commandText

    let proc name = proc generatorConfig name
    
    let getPostsByBlogId: int -> Post list DbAction = 
        sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post2 where blogId = @blogId"

    let getPostById: int -> Post DbAction = 
        sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post2 where id = @postId"

    let readMany() =
        getPostsByBlogId 1 |> run |> ignore

    let readOneManyTimes() =
        let rnd = Random()
        dbaction {
            for i in 1..500 do
                let id = rnd.Next(1, 500)
                do! getPostById id |> DbAction.map (fun _ -> ())
        } |> run

    module Async = 

        let run f = AsyncDb.run createConnection f

        let getPostsByBlogId: int -> Post list AsyncDb = 
            sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post2 where blogId = @blogId"

        let getPostById: int -> Post AsyncDb = 
            sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post2 where id = @postId"

        let readMany() =
            getPostsByBlogId 1 |> run |> Async.Ignore

        let readOneManyTimes() =
            let rnd = Random()
            asyncdb {
                for i in 1..500 do
                    let id = rnd.Next(1, 500)
                    do! getPostById id |> AsyncDb.map (fun _ -> ())
            } |> run


        
