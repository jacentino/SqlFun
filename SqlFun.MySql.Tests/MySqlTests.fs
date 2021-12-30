namespace SqlFun.MySql.Tests

open SqlFun
open Data
open Common
open NUnit.Framework
open SqlFun.Transforms
open System

type TestQueries() =    
 
    static member getBlog: int -> DbAction<Blog> = 
        sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy from blog where id = @id"

    static member getBlogAsync: int -> AsyncDb<Blog> = 
        sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy from blog where id = @id"

    static member spInsertBlog: Blog -> DbAction<unit> = 
        proc "addblog"
        >> DbAction.map resultOnly
        
    static member spGetBlog: int -> DbAction<Blog> = 
        proc "getblog"
        >> DbAction.map resultOnly
        
    static member insertBlog: Blog -> DbAction<unit> =
        sql "insert into blog (id, name, title, description, owner, createdAt, modifiedAt, modifiedBy) values (@id, @name, @title, @description, @owner, @createdAt, @modifiedAt, @modifiedBy)"


[<TestFixture>]
type MySqlTests() = 


    [<Test>]
    member this.``Simple queries to MySql return valid results``() =
        let b = TestQueries.getBlog 1 |> run
        Assert.AreEqual(1, b.id)

    [<Test>]
    member this.``Async queries to MySql return valid results``() =
        let b = TestQueries.getBlogAsync 1 |> runAsync |> Async.RunSynchronously
        Assert.AreEqual(1, b.id)

    [<Test>]
    member this.``Stored procedure calls to MySql return valid results``() =
        let b = TestQueries.spGetBlog 1 |> run
        Assert.AreEqual(1, b.id)

    [<Test>]
    member this.``Stored procedure calls to MySql work as expected``() =

        Tooling.deleteAllButFirstBlog |> run

        TestQueries.spInsertBlog {
            id = 4
            name = "test-blog-4"
            title = "Testing simple insert 4"
            description = "Added to check if inserts work properly."
            owner = "jacentino"
            createdAt = DateTime.Now
            modifiedAt = None
            modifiedBy = None
            posts = []
        }  |> run

    
    [<Test>]
    member this.``Inserts to MySql work as expected``() =    

        Tooling.deleteAllButFirstBlog |> run

        TestQueries.insertBlog {
            id = 4
            name = "test-blog-4"
            title = "Testing simple insert 4"
            description = "Added to check if inserts work properly."
            owner = "jacentino"
            createdAt = DateTime.Now
            modifiedAt = None
            modifiedBy = None
            posts = []
        } |> run


    
