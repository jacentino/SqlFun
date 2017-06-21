namespace SqlFun.Tests

open NUnit.Framework
open SqlFun
open Data
open SqlFun.Exceptions
open SqlFun.Transforms
open Common
open System

type CrudQueries() =
    
    static member insertPost: Post -> DataContext -> unit = 
        sql <| Crud.Insert<Post> ["id"; "comments"; "tags"]

    static member updatePost: Post -> DataContext -> unit = 
        sql <| Crud.Update<Post> (["id"], ["comments"; "tags"])

    static member deletePost: int -> DataContext -> unit =
        sql <| Crud.DeleteByKey<Post> ["id"]

    static member getPost: int -> DataContext -> Post = 
        sql <| Crud.SelectByKey<Post> ["id"]


[<TestFixture>]
type CrudQueryTests() = 
    
    let mutable postId = 0

    [<SetUp>]
    member this.Setup() =
        Tooling.cleanup |> run
        let post = {
            id = 0
            blogId = 1
            name = "fake-post"
            title = "Fake post"
            content = "Does not matter"
            author = "jacenty"
            createdAt = DateTime.Now
            modifiedBy = None
            modifiedAt = None
            status = PostStatus.Archived
            comments = []
            tags = []
        }
        postId <- Tooling.insertPost post |> run
     
    [<OneTimeTearDown>]
    member this.TearDown() =    
        Tooling.cleanup |> run

    [<Test>]
    member this.``Crud.Insert generates valid sql command``() =
        let post = {
            id = 0
            blogId = 1
            name = "another fake-post"
            title = "Another fake post"
            content = "Doesn't matter"
            author = "jacenty"
            createdAt = DateTime.Now
            modifiedBy = None
            modifiedAt = None
            status = PostStatus.Archived
            comments = []
            tags = []
        }
        CrudQueries.insertPost post |> run
        let n = Tooling.getNumberOfPosts |> run
        Assert.AreEqual(4, n)
        
    [<Test>]
    member this.``Crud.Update generates valid sql command``() = 
        let post = Tooling.getPostByName "fake-post" |> run
        let changed = { post with title = "Changed fake post" }
        CrudQueries.updatePost changed |> run
        let p = Tooling.getPostByName "fake-post" |> run
        Assert.AreEqual("Changed fake post", p.title)
        
    [<Test>]
    member this.``Crud.DeleteByKey generates valid sql command``() =
        CrudQueries.deletePost postId |> run
        let n = Tooling.getNumberOfPosts |> run
        Assert.AreEqual(2, n)

    [<Test>]
    member this.``Crud.SelectByKey generates valid sql command``() =
        let p = CrudQueries.getPost postId |> run
        Assert.AreEqual("fake-post", p.name)

