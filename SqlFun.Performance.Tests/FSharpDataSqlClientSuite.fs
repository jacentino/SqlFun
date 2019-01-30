namespace SqlFun.Performance.Tests

module FSharpDataSqlClientSuite = 
    open FSharp.Data
    open System.Linq
    open System
    open System.Configuration

    let connectionString = ConfigurationManager.ConnectionStrings.["SqlFunTests"].ConnectionString

    type GetPostsByBlogId = SqlCommandProvider<"select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post2 where blogId = @blogId", "name=SqlFunTests">

    type GetPostById = SqlCommandProvider<"select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post2 where id = @postId", "name=SqlFunTests">

    let readMany() = 
        use cmd = new GetPostsByBlogId(connectionString)
        cmd.Execute(1).ToList() |> ignore

    let readOneManyTimes() = 
        let rnd = Random()
        for i in 1..500 do
            let id = rnd.Next(1, 500)
            use cmd = new GetPostById(connectionString) 
            cmd.Execute(id) |> ignore
    

    module Async = 

        let readMany() = 
            async {
                use cmd = new GetPostsByBlogId(connectionString)
                let! r1 = cmd.AsyncExecute(1)
                let r2 = r1.ToList()
                return ()
            }

        let readOneManyTimes() = 
            let rnd = Random()
            async {
                for i in 1..500 do
                    let id = rnd.Next(1, 500)
                    use cmd = new GetPostById(connectionString) 
                    let! r = cmd.AsyncExecute(id)
                    ()
            }


