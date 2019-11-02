namespace SqlFun.Tests

open NUnit.Framework
open SqlFun
open Data
open Common
open SqlFun.MsSql

module MsSqlTestQueries =    

    let generatorConfig = createDefaultConfig createConnection

    let sqlTm tm commandText = Queries.sql { generatorConfig with commandTimeout = Some tm } commandText

    let sql command = Queries.sql generatorConfig command

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

    type TagStatusString = 
        | [<EnumValue("A")>] Active = 1
        | [<EnumValue("I")>] Inactive = 0

    type Tag5 = {
        postId: int 
        name: string
        status: TagStatusString
    }

    type TagStatusInt = 
        | Active = 1
        | Inactive = 0

    type Tag6 = {
        postId: int 
        name: string
        status: TagStatusInt
    }

    let insertBlogs: Blog list -> DataContext -> unit =
        sqlTm 60 "insert into blog ([name],[title],[description],[owner],[createdAt],[modifiedAt],[modifiedBy])
                  select [name],[title],[description],[owner],[createdAt],[modifiedAt],[modifiedBy] from @blogs"

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

    let updateTags5: int -> Tag5 list -> DataContext -> unit = 
        sql "delete from tag where postId = @id;
             insert into tag (postId, name) select @id, name from @tags"

    let updateTags6: int -> Tag6 list -> DataContext -> unit = 
        sql "delete from tag where postId = @id;
             insert into tag (postId, name) select @id, name from @tags"

open MsSqlTestQueries
open System.Linq.Expressions
open System.Diagnostics

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
                
    [<Test>]
    member this.``Queries utilizing TVP-s with enums represented as strings are executed correctly.``() = 
        let tags = [
            { Tag5.postId = 2; name = "EntityFramework"; status = TagStatusString.Active } 
            { Tag5.postId = 2; name = "Dapper"; status = TagStatusString.Active }
            { Tag5.postId = 2; name = "FSharp.Data.SqlClient"; status = TagStatusString.Active }
        ]
        updateTags5 2 tags |> run
        let result = Tooling.getTags 2 |> run |> List.map (fun t -> { Tag5.postId = t.postId; name = t.name; status = TagStatusString.Active })
        Assert.AreEqual(tags |> List.sortBy (fun t -> t.name), result |> List.sortBy (fun t -> t.name))
        
    [<Test>]
    member this.``Queries utilizing TVP-s with enums represented as ints are executed correctly.``() = 
        let tags = [
            { Tag6.postId = 2; name = "EntityFramework"; status = TagStatusInt.Active } 
            { Tag6.postId = 2; name = "Dapper"; status = TagStatusInt.Active }
            { Tag6.postId = 2; name = "FSharp.Data.SqlClient"; status = TagStatusInt.Active }
        ]
        updateTags6 2 tags |> run
        let result = Tooling.getTags 2 |> run |> List.map (fun t -> { Tag6.postId = t.postId; name = t.name; status = TagStatusInt.Active })
        Assert.AreEqual(tags |> List.sortBy (fun t -> t.name), result |> List.sortBy (fun t -> t.name))
        
    [<Test>]       
    member this.``TVP-based inserts are extremely fast``() = 

        Tooling.deleteAllButFirstBlog |> run

        let blogsToAdd = 
            [  for i in 2..200 do
                yield {
                    id = i
                    name = sprintf "blog-%d" i
                    title = sprintf "Blog no %d" i
                    description = sprintf "Just another blog, added for test - %d" i
                    owner = "jacenty"
                    createdAt = System.DateTime.Now
                    modifiedAt = None
                    modifiedBy = None
                    posts = []          
                }
            ]

        let sw = Stopwatch()
        sw.Start()
        insertBlogs blogsToAdd |> run
        sw.Stop()
        printfn "Elapsed time %O" sw.Elapsed
        
        let numOfBlogs = Tooling.getNumberOfBlogs |> run        
        Tooling.deleteAllButFirstBlog |> run
        Assert.AreEqual(200, numOfBlogs)
        


    


