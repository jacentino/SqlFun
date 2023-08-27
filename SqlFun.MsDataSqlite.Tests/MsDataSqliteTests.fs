namespace SqlFun.MsDataSqlite.Tests

open SqlFun
open Common
open Data
open NUnit.Framework

type TestQueries() =    

    static member getBlog: int -> DbAction<Blog> = 
        sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy from blog where id = @blogid"

    static member insertBlog: Blog -> DbAction<unit> =
        sql "insert into blog (id, name, title, description, owner, createdAt, modifiedAt, modifiedBy) values (@id, @name, @title, @description, @owner, @createdAt, @modifiedAt, @modifiedBy)"

    static member insertPost: Post -> DbAction<unit> = 
        sql "insert into post (id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status)
             values (@id, @blogId, @name, @title, @content, @author, @createdAt, @modifiedAt, @modifiedBy, @status)"

[<TestFixture>]
type MsDataSqliteTests() = 
    
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

    [<Test>]
    member this.``Inserts to tables with foregin keys work as expected``() = 
        
        Tooling.deleteAllPosts |> run

        TestQueries.insertPost {
            id = 1
            blogId = 1
            name = "test-post-1"
            title = "Checking inserts to tables with foreign key constraints"
            content = "Just checking"
            author = "jacentino"
            createdAt = System.DateTime.Now
            modifiedAt = None
            modifiedBy = None
            status = PostStatus.New
        } |> run


