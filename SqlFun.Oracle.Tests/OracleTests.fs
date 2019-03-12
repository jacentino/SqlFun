namespace SqlFun.Oracle.Tests

open NUnit.Framework
open Oracle.ManagedDataAccess.Client
open SqlFun
open Common
open Data

type TestQueries() =    
 
    static member getBlog: int -> DataContext -> Blog = 
        sql "select blogid, name, title, description, owner, createdAt, modifiedAt, modifiedBy from blog where blogid = :blogid"


[<TestFixture>]
type OracleTests() = 
    
    [<Test>]
    member this.``Simple queries to Oracle return valid results``() = 
        let blog = TestQueries.getBlog 1 |> run
        Assert.AreEqual("functional-data-access-with-sqlfun", blog.name)
        
