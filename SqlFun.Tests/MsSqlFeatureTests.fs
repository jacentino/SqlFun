namespace SqlFun.Tests

open NUnit.Framework
open SqlFun
open Data
open SqlFun.Exceptions
open SqlFun.Transforms
open Common
open System

module MsSqlTestQueries = 
    
    let sql command = MsSql.sql createConnection defaultParamBuilder command

    let updateTags: Post -> DataContext -> unit = 
        sql "delete from tag where postId = @id;
             insert into tag (postId, name) select @id, name from @tags"

open MsSqlTestQueries

[<TestFixture>]
type MsSqlTests() = 

    [<SetUp>]
    member this.SetUp() = 
        Tooling.cleanup |> run

    [<Test>]
    member this.``Queries utilizing TVP-s are executed correctly.``() = 
        let p = Tooling.getPost 2 |> run |> Option.get
        let tags = [
            { postId = p.id; name = "EntityFramework" } 
            { postId = p.id; name = "Dapper" }
            { postId = p.id; name = "FSharp.Data.SqlClient" }
        ]
        updateTags { p with tags = tags } |> run
        let tags = Tooling.getTags p.id |> run
        Assert.AreEqual(3, tags |> List.length)
        
    


