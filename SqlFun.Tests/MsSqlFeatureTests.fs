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

    type Tag2 = {
        postId: int option
        name: string
    }

    type Tag3 = {
        tag: Tag
    }

    type Tag4 = {
        tagOpt: Tag option
    }

    let updateTags: Post -> DataContext -> unit = 
        sql "delete from tag where postId = @id;
             insert into tag (postId, name) select @id, name from @tags"

    let updateTags2: int -> Tag2 list -> DataContext -> unit = 
        sql "delete from tag where postId = @id;
             insert into tag (postId, name) select @id, name from @tags"

    let updateTags3: int -> Tag3 list -> DataContext -> unit = 
        sql "delete from tag where postId = @id;
             insert into tag (postId, name) select @id, name from @tags"

    let updateTags4: int -> Tag4 list -> DataContext -> unit = 
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
            { Tag.postId = p.id; name = "EntityFramework" } 
            { Tag.postId = p.id; name = "Dapper" }
            { Tag.postId = p.id; name = "FSharp.Data.SqlClient" }
        ]
        updateTags { p with tags = tags } |> run
        let result = Tooling.getTags p.id |> run
        Assert.AreEqual(tags |> List.sortBy (fun t -> t.name), result |> List.sortBy (fun t -> t.name))
        
    [<Test>]
    member this.``Queries utilizing TVP-s with optional fields are executed correctly.``() = 
        let tags = [
            { Tag2.postId = Some 2; name = "EntityFramework" } 
            { Tag2.postId = Some 2; name = "Dapper" }
            { Tag2.postId = None; name = "FSharp.Data.SqlClient" }
        ]
        updateTags2 2 tags |> run
        let result = Tooling.getTags 2 |> run |> List.map (fun t -> t.name)
        Assert.AreEqual(tags |> List.map (fun t -> t.name) |> List.sortBy id, result |> List.sortBy id)
        
    [<Test>]
    member this.``Queries utilizing TVP-s with non-optional substructures are executed correctly.``() = 
        let tags = [
            { tag = { Tag.postId = 2; name = "EntityFramework" } }
            { tag = { Tag.postId = 2; name = "Dapper" } }
            { tag = { Tag.postId = 2; name = "FSharp.Data.SqlClient" } }
        ]
        updateTags3 2 tags |> run
        let result = Tooling.getTags 2 |> run |> List.map (fun t -> { tag = t })
        Assert.AreEqual(tags |> List.sortBy (fun t -> t.tag.name), result |> List.sortBy (fun t -> t.tag.name))
        
    [<Test>]
    member this.``Queries utilizing TVP-s with optional substructures are executed correctly.``() = 
        let tags = [
            { tagOpt = Some { Tag.postId = 2; name = "EntityFramework" } }
            { tagOpt = Some { Tag.postId = 2; name = "Dapper" } }
            { tagOpt = Some { Tag.postId = 2; name = "FSharp.Data.SqlClient" } }
        ]
        updateTags4 2 tags |> run
        let result = Tooling.getTags 2 |> run |> List.map (fun t -> { tagOpt = Some t })
        Assert.AreEqual(tags |> List.sortBy (fun t -> t.tagOpt.Value.name), result |> List.sortBy (fun t -> t.tagOpt.Value.name))
        
    


    


