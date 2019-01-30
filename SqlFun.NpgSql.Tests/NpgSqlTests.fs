namespace SqlFun.NpgSql.Tests

open SqlFun
open Data
open Common
open NUnit.Framework
open SqlFun.Transforms

type TestQueries() =    
 
    static member getBlog: int -> DataContext -> Blog = 
        sql "select blogid, name, title, description, owner, createdAt, modifiedAt, modifiedBy from blog where blogid = @id"

    static member spGetBlog: int -> DataContext -> Blog = 
        proc "getblog"
        >> DbAction.map (resultOnly id)
        
    static member getPosts: int array -> DataContext -> Post list = 
        sql "select p.postid, p.blogId, p.name, p.title, p.content, p.author, p.createdAt, p.modifiedAt, p.modifiedBy, p.status
             from post p join unnest(@ids) ids on p.postid = ids"

[<TestFixture>]
type NpgSqlTests() = 
    
    [<Test>]
    member this.``Simple queries to PostgreSQL return valid results``() =
        let b = TestQueries.getBlog 1 |> run
        Assert.AreEqual(1, b.blogId)

    [<Test>]
    member this.``Stored procedure calls to PostgreSQL return valid results``() =
        let b = TestQueries.spGetBlog 1 |> run
        Assert.AreEqual(1, b.blogId)

    [<Test>]
    member this.``Queries to PostgreSQL using array parameters return valid results``() = 
        let l = TestQueries.getPosts [| 1; 2 |] |> run
        Assert.AreEqual(2, l |> List.length)

