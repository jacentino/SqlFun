namespace SqlFun.Oracle.Tests

open NUnit.Framework
open Oracle.ManagedDataAccess.Client
open SqlFun
open Common
open Data
open System
open System.Configuration
open System.Data

type TestQueries() =    
 
    static member getBlog: int -> DbAction<Blog> = 
        sql "select blogid, name, title, description, owner, createdAt, modifiedAt, modifiedBy from blog where blogid = :blogid"

    static member insertBlog: Blog -> DbAction<unit> =
        sql "insert into blog (blogid, name, title, description, owner, createdAt, modifiedAt, modifiedBy) values (:blogId, :name, :title, :description, :owner, :createdAt, :modifiedAt, :modifiedBy)"

    static member insertBlogsWithArrays: int[] -> string[] -> string[] -> string[] -> string[] -> DateTime[] -> DbAction<unit> =
        sql "insert into blog (blogid, name, title, description, owner, createdAt) values (:blogid, :name, :title, :description, :owner, :createdAt)"

[<TestFixture>]
type OracleTests() = 
    
    [<Test>]
    member this.``Simple queries to Oracle return valid results``() = 
        let blog = TestQueries.getBlog 1 |> run
        Assert.AreEqual("functional-data-access-with-sqlfun", blog.name)        
    
    [<Test>]
    member this.``Oracle SchemaOnly test``() =    
        use con = new OracleConnection(ConfigurationManager.ConnectionStrings.["SqlFunTests"].ConnectionString)
        con.Open()
        use cmd = con.CreateCommand()
        cmd.BindByName <- true
        cmd.CommandText <- "select * from blog where blogid = :blogid"
        cmd.Parameters.Add(":blogid", OracleDbType.Int32, 1 :> obj, ParameterDirection.Input) |> ignore
        use reader = cmd.ExecuteReader()
        while reader.Read() do
            Console.WriteLine(reader.GetString(1))

    
    [<Test>]
    member this.``Inserts to Oracle work as expected``() =    

        Tooling.deleteAllButFirstBlog |> run

        TestQueries.insertBlog {
            blogId = 4
            name = "test-blog-4"
            title = "Testing simple insert 4"
            description = "Added to check if inserts work properly."
            owner = "jacentino"
            createdAt = DateTime.Now
            modifiedAt = None
            modifiedBy = None
        } |> run
    
    [<Test>]
    member this.``Array parameters allow to add multiple records``() =    

        Tooling.deleteAllButFirstBlog |> run

        TestQueries.insertBlogsWithArrays  
            [| 2; 3 |]
            [| "test-blog-2"; "test-blog-3" |]
            [| "Testing array parameters 1"; "Testing array parameters 2" |]
            [| "Add to check if VARRAY parameters work as expected (1)."; "Added to check if VARRAY parameters work as expected (2)." |]
            [| "jacentino"; "placentino" |]
            [| DateTime.Now; DateTime.Now |]
            |> run
