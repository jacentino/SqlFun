namespace SqlFun.Sqlite.Tests

open SqlFun
open Common
open Data
open NUnit.Framework

type TestQueries() =    

    static member getBlog: int -> DbAction<Blog> = 
        sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy from blog where id = @blogid"

    static member insertBlog: Blog -> DbAction<unit> =
        sql "insert into blog (id, name, title, description, owner, createdAt, modifiedAt, modifiedBy) values (@id, @name, @title, @description, @owner, @createdAt, @modifiedAt, @modifiedBy)"

[<TestFixture>]
type SqliteTests() = 
    
    [<Test>]
    member this.``Simple queries to Sqlite return valid results``() = 
        let blog = TestQueries.getBlog 1 |> run
        Assert.AreEqual("functional-data-access-with-sqlfun", blog.name)        
    
    [<Test>]
    member this.``Inserts to sqlite work as expected``() =    

        Tooling.deleteAllButFirstBlog |> run

        TestQueries.insertBlog {
            id = 4
            name = "test-blog-4"
            title = "Testing simple insert 4"
            description = "Added to check if inserts work properly."
            owner = "jacentino"
            createdAt = System.DateTime.Now
            modifiedAt = None
            modifiedBy = None
        } |> run


