namespace SqlFun.Tests

open NUnit.Framework
open SqlFun
open Data
open SqlFun.Exceptions
open SqlFun.Transforms
open SqlFun.Transforms.Standard
open Common
open System

type StoredProcs() = 
        
        static member GetAllPosts: int -> DataContext -> Post list Async = 
            proc "GetAllPosts"
            >> AsyncDb.map (resultOnly
                            >> (combineTransforms 
                                    (join Post.Id Comment.PostId (Post.withComments Tooling.buildTree)) 
                                    (join Post.Id Tag.PostId (Post.withTags List.ofSeq)))
                            >> List.ofSeq)

        static member FindPostsFull: (PostSearchCriteria * SignatureSearchCriteria) -> DataContext -> (int * unit * Post list) =
            proc "FindPosts"

        static member FindPostsResultOnly: (PostSearchCriteria * SignatureSearchCriteria) -> DataContext -> Post list =
            proc "FindPosts"
            >> DbAction.map resultOnly

        static member FindPostsStream: (PostSearchCriteria * SignatureSearchCriteria) -> DataContext -> Post ResultStream =
            proc "FindPosts"
            >> DbAction.map resultOnly

        static member FindPostsStreamAsync: (PostSearchCriteria * SignatureSearchCriteria) -> DataContext -> Post ResultStream Async =
            proc "FindPosts"
            >> AsyncDb.map resultOnly


[<TestFixture>]
type StoredProcTests() = 
    
    [<Test>]
    member this.``Stored procedures with simple parameters are invoked correctly``() =
        let pl = StoredProcs.GetAllPosts 1 |> runAsync |> Async.RunSynchronously
        Assert.AreEqual(2, pl |> List.length)
        let p = pl |> List.head
        Assert.AreEqual(1, p.comments |> List.length)

    [<Test>]
    member this.``Stored procedures calls return retcode, output params and results``() = 
        let (_, _, pl) = StoredProcs.FindPostsFull
                            ({
                                blogId = Some 1
                                title = Some "another"
                                content = None
                            }, {
                                author = None
                                createdAtFrom = Some <| DateTime(2017, 04, 20)
                                createdAtTo = None
                                modifiedAtFrom = None
                                modifiedAtTo = None
                                modifiedBy = None
                                status = Some PostStatus.Published
                            }) 
                            |> run    
        let p = pl |> List.head                             
        Assert.AreEqual("Yet another sql framework", p.title)

    [<Test>]
    member this.``Stored procedures calls composed with resultOnly return only results``() = 
        let p = StoredProcs.FindPostsResultOnly
                    ({
                        blogId = Some 1
                        title = Some "another"
                        content = None
                    }, {
                        author = None
                        createdAtFrom = Some <| DateTime(2017, 04, 20)
                        createdAtTo = None
                        modifiedAtFrom = None
                        modifiedAtTo = None
                        modifiedBy = None
                        status = Some PostStatus.Published
                    }) 
                    |> run 
                    |> List.head  
        Assert.AreEqual("Yet another sql framework", p.title)

    [<Test>]
    member this.``Stored procedure results can be streamed``() = 
        let p = 
            dbaction {
                use! s = StoredProcs.FindPostsStream
                            ({
                                blogId = Some 1
                                title = Some "another"
                                content = None
                            }, {
                                author = None
                                createdAtFrom = Some <| DateTime(2017, 04, 20)
                                createdAtTo = None
                                modifiedAtFrom = None
                                modifiedAtTo = None
                                modifiedBy = None
                                status = Some PostStatus.Published
                            }) 
                return s |> Seq.head
            }
            |> run 
        Assert.AreEqual("Yet another sql framework", p.title)


    [<Test>]
    member this.``Stored procedure async results can be streamed``() = 
        let p = 
            asyncdb {
                use! s = StoredProcs.FindPostsStreamAsync
                            ({
                                blogId = Some 1
                                title = Some "another"
                                content = None
                            }, {
                                author = None
                                createdAtFrom = Some <| DateTime(2017, 04, 20)
                                createdAtTo = None
                                modifiedAtFrom = None
                                modifiedAtTo = None
                                modifiedBy = None
                                status = Some PostStatus.Published
                            }) 
                return s |> Seq.head
            }
            |> runAsync 
            |> Async.RunSynchronously
        Assert.AreEqual("Yet another sql framework", p.title)
