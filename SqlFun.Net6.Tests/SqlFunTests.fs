namespace SqlFun.Net6.Tests

open NUnit.Framework

open System
open SqlFun
open Common
open Data
open FSharp.Control

module TestQueries = 

    let insertPost: Post -> AsyncDb<int> = 
        sql "insert into post 
                    (blogId, name, title, content, author, createdAt, status)
             values (@blogId, @name, @title, @content, @author, @createdAt, @status);
             select scope_identity()"

    let getPosts: AsyncDb<Post list> = 
        sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post"

    let insertComment: Comment2 -> AsyncDb<unit> = 
        sql "insert into comment (postId, parentId, content, author, createdAt) values (@postId, @parentId, @content, @author, @createdAt)"

    let insertCommentsTV: Comment list -> AsyncDb<unit> = 
        sql "insert into comment select postId, parentId, content, author, createdAt from @comments"

    let insertCommentsTV2: Comment2 list -> AsyncDb<unit> = 
        sql "insert into comment select postId, parentId, content, author, createdAt from @comments"

    let insertCommentsTV3: Comment3 list -> AsyncDb<unit> = 
        sql "insert into comment select postId, parentId, content, author, createdAt from @comments"

    let insertCommentsTV4: Comment4 list -> AsyncDb<unit> = 
        sql "insert into comment select postId, parentId, content, author, createdAt from @comments"

    let getComments: int -> AsyncDb<Comment2 list> = 
        sql "select id, postId, parentId, content, author, createdAt from Comment where id = @id"

    let getSomePostsByTagsAsyncWithAsyncStream: string list -> IDataContext -> Post AsyncResultStream Async = 
        sql "select id, blogId, p.name, title, content, author, createdAt, modifiedAt, modifiedBy, status from 
             post p join tag t on t.postId = p.id
             where t.name in (@tagName)
             group by id, blogId, p.name, title, content, author, createdAt, modifiedAt, modifiedBy, status"


module Net6Tests = 

    [<SetUp>]
    let Setup () =
        Tooling.cleanup |> run |> Async.RunSynchronously

    [<Test>]
    let ``DateOnly values can be used as parameters`` () =
            let post = { 
                id = 0 
                blogId = 1
                name = "my-expectations"
                title = "My expectations" 
                content = "What I expect from an sql framework?"
                author = "jacenty"
                createdAt = DateOnly.FromDateTime(DateTime.Today)
                modifiedAt = None
                modifiedBy = None
                status = PostStatus.New
            }
            TestQueries.insertPost post
            |> run |> Async.RunSynchronously |> ignore
        
    [<Test>]
    let ``DateOnly values can be used as columns`` () =
        TestQueries.getPosts
        |> run |> Async.RunSynchronously |> ignore
            
    [<Test>]
    let ``DateOnly values can be used as TV parameters`` () =
        let comment = { 
            Comment.id = 0
            postId = 1
            parentId = None
            content = ""
            author = "jacenty"
            createdAt = DateOnly.FromDateTime(DateTime.Now)
        }
        TestQueries.insertCommentsTV [comment]
        |> run |> Async.RunSynchronously |> ignore
            
    [<Test>]
    let ``DateOnly option values can be used as TV parameters`` () =
        let comment = { 
            Comment3.id = 0
            postId = 1
            parentId = None
            content = ""
            author = "jacenty"
            createdAt = Some (DateOnly.FromDateTime(DateTime.Now))
        }
        TestQueries.insertCommentsTV3 [comment]
        |> run |> Async.RunSynchronously |> ignore


    [<Test>]
    let ``TimeOnly values can be used as parameters`` () =
        let comment = { 
            Comment2.id = 0
            postId = 1
            parentId = None
            content = ""
            author = "jacenty"
            createdAt = TimeOnly.FromDateTime(DateTime.Now)
        }
        TestQueries.insertComment comment
        |> run |> Async.RunSynchronously |> ignore
        
    [<Test>]
    let ``TimeOnly values can be used as TV parameters`` () =
        let comment = { 
            Comment2.id = 0
            postId = 1
            parentId = None
            content = ""
            author = "jacenty"
            createdAt = TimeOnly.FromDateTime(DateTime.Now)
        }
        TestQueries.insertCommentsTV2 [comment]
        |> run |> Async.RunSynchronously |> ignore
        
    [<Test>]
    let ``TimeOnly option values can be used as TV parameters`` () =
        let comment = { 
            Comment4.id = 0
            postId = 1
            parentId = None
            content = ""
            author = "jacenty"
            createdAt = Some(TimeOnly.FromDateTime(DateTime.Now))
        }
        TestQueries.insertCommentsTV4 [comment]
        |> run |> Async.RunSynchronously |> ignore

    [<Test>]
    let ``TimeOnly values can be used as columns`` () =
        TestQueries.getComments 1
        |> run |> Async.RunSynchronously |> ignore
            
    [<Test>]
    let ``Query results can be async sequences``() =
        asyncdb {
            use! ps = TestQueries.getSomePostsByTagsAsyncWithAsyncStream ["framework"; "options"]
            let! p = ps |> AsyncSeq.toListAsync |> AsyncDb.fromAsync
            Assert.IsNotEmpty(p)
        } |> run |> Async.RunSynchronously
           
    [<Test>]
    let ``Async sequences can be passed to for loop in AsyncDb``() =
        asyncdb {
            use! ps = TestQueries.getSomePostsByTagsAsyncWithAsyncStream ["framework"; "options"]
            for p in ps do
                printf "%A" p
        } |> run |> Async.RunSynchronously
           
    [<Test>]
    let ``Async sequences can be iterated only once``() =
        Assert.Throws<AggregateException>(fun () ->
            asyncdb {
                use! ps = TestQueries.getSomePostsByTagsAsyncWithAsyncStream ["framework"; "options"]
                for p in ps do
                    printf "%A" p
                for p in ps do
                    printf "%A" p
            } |> run |> Async.RunSynchronously
        ) |> ignore
