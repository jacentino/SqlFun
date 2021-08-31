namespace SqlFun.Performance.Tests

module Preparation = 

    open SqlFun
    open SqlFun.Queries
    open Data
    open System

    let run f = AsyncDb.run createConnection f

    let sql commandText = sql generatorConfig commandText

    let testDataExist: AsyncDb<bool> = 
        sql "select count(*) from post2" |> AsyncDb.map((<=) 500)
        

    let insertPost: Post -> AsyncDb<unit> = 
        sql "insert into post2 (id, blogId, name, title, content, author, createdAt, status)
             values (@id, @blogId, @name, @title, @content, @author, @createdAt, @status)"

    let createTestData() = 
        asyncdb {
            let! td = testDataExist
            if not td then
                for i in 1..500 do
                    do! insertPost {
                        id = i
                        blogId = 1
                        name = sprintf "Post %d" i
                        title = sprintf "Post %d title" i 
                        content = sprintf "Post %d contents" i
                        author = "jacenty"
                        createdAt = DateTime.Now
                        modifiedAt = None
                        modifiedBy = None
                        status = "N"
                        comments = []
                    }
        } |> run |> Async.RunSynchronously
        
            
