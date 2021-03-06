﻿namespace SqlFun.Oracle.Tests

open NUnit.Framework
open Oracle.ManagedDataAccess.Client
open SqlFun
open Common
open Data
open System
open System.Configuration
open System.Data
open Oracle.ManagedDataAccess.Types
open System.Diagnostics

type TestQueries() =    

    static member getBlog: int -> DbAction<Blog> = 
        sql "select blogid, name, title, description, owner, createdAt, modifiedAt, modifiedBy from blog where blogid = :blogid"

    static member insertBlog: Blog -> DbAction<unit> =
        sql "insert into blog (blogid, name, title, description, owner, createdAt, modifiedAt, modifiedBy) values (:blogId, :name, :title, :description, :owner, :createdAt, :modifiedAt, :modifiedBy)"

    static member insertBlogsWithArrays: int[] -> string[] -> string[] -> string[] -> string[] -> DateTime[] -> DbAction<unit> =
        sql "insert into blog (blogid, name, title, description, owner, createdAt) values (:blogid, :name, :title, :description, :owner, :createdAt)"

    static member insertBlogProc: (int * string * string * string * string * DateTime) -> DbAction<unit> =
        proc "sp_add_blog"
        >> DbAction.map Transforms.resultOnly
        

    static member getBlogProc: int -> DbAction<Blog> =
        proc "sp_get_blog"
        >> DbAction.map Transforms.resultOnly

[<TestFixture>]
type OracleTests() = 
    
    [<Test>]
    member this.``Simple queries to Oracle return valid results``() = 
        let blog = TestQueries.getBlog 1 |> run
        Assert.AreEqual("functional-data-access-with-sqlfun", blog.name)        
    
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
    
    [<Test>]
    member this.``Oracle array parameters are faster in bulk operations``() =    

        Tooling.deleteAllButFirstBlog |> run

        let sw = Stopwatch()
        sw.Start()

        for i in 0..9 do

            let ids = Array.init 200 ((+) (i * 200 + 2))
            let names = ids |> Array.map (sprintf "test-blog-%d")
            let titles = ids |> Array.map (sprintf "Testing array parameters %d")
            let descriptions = ids |> Array.map (sprintf "Add to check if VARRAY parameters work as expected (%d).")
            let owners = ids |> Array.map (fun _ -> "jacentino")
            let creationDates = ids |> Array.map (fun _ -> DateTime.Now)

            TestQueries.insertBlogsWithArrays  
                ids
                names
                titles
                descriptions
                owners
                creationDates
                |> run

        sw.Stop()
        printfn "Elapsed time %O" sw.Elapsed

    [<Test>]
    member this.``Insert to Oracle with stored procedures works as expected``() =    

        Tooling.deleteAllButFirstBlog |> run

        TestQueries.insertBlogProc  
            ( 5
            , "test-blog-5"
            , "Testing simple insert 5"
            , "Added to check if inserts work properly."
            , "jacentino"
            ,  DateTime.Now )
        |> run
        |> ignore

    
    [<Test>]
    member this.``Oracle stored procedures return valid results``() = 
        let blog = TestQueries.getBlogProc 1 |> run
        Assert.AreEqual("functional-data-access-with-sqlfun", blog.name)        
