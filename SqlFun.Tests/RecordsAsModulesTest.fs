namespace SqlFun.Tests

open NUnit.Framework
open SqlFun
open Data
open SqlFun.Exceptions
open SqlFun.Transforms
open SqlFun.Transforms.Standard
open Common
open System

module RecordsAsModules = 

    module Data = 

        let mapDeps m f p deps = f(p, m deps)

        type Blogging<'deps> = {
            getBlog: int -> 'deps -> Blog Async
            getPosts: int -> 'deps -> Post list Async
        }

        let ComposeBlogging(extract: 'deps -> DataContext) =      
            let mapDeps f p deps= mapDeps extract f p deps
            { 
                getBlog = 
                    sql"select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy from Blog where id = @id"
                    |> mapDeps
                getPosts = 
                    sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post where blogId = @id;
                         select t.postId, t.name from tag t join post p on t.postId = p.id where p.blogId = @id;
                         select c.id, c.postId, c.parentId, c.content, c.author, c.createdAt from comment c join post p on c.postId = p.id where p.blogId = @id"
                    >> mapAsync (Conventions.join<_, Tag> >-> (mapSnd Tooling.buildTree >> Conventions.join<_, Comment>) >> List.ofSeq)
                    |> mapDeps
            }

    module Service = 

        type Result<'t> = Choice<'t, exn>

        type Blogging<'deps> = {
            getBlog: int -> 'deps -> Blog Result Async
            getPosts: int -> 'deps -> Post list Result Async
        }

        let toResult f a deps = f a deps |> Async.Catch

        let ComposeBlogging (data: Data.Blogging<'deps>) =
            {
                getBlog = data.getBlog |> toResult
                getPosts = data.getPosts |> toResult
            }

    module CompositionRoot = 

        type Dependencies = { dctx: DataContext }

        let run f = async {
            return! runAsync (fun ctx -> f { dctx = ctx })
        }

        let withLogging msg f args deps = async {
            let sw = System.Diagnostics.Stopwatch()
            sw.Start()
            let! res = f args deps
            match res with
            | Choice1Of2 _ -> printf "[%O] %s %O" sw.Elapsed msg args
            | Choice2Of2 err -> printf "[%O] %s %O: %O" sw.Elapsed msg args err
            return res
        } 

        module Data = 
            let Blogging = Data.ComposeBlogging(fun d -> d.dctx)

        module Service = 
            let private blogging = Service.ComposeBlogging(Data.Blogging)
            let Blogging = {
                blogging with
                    getBlog = blogging.getBlog |> withLogging "Blogging.getBlog" 
                    getPosts = blogging.getPosts |> withLogging "Blogging.getPosts"
            }
             
    open CompositionRoot
            
    [<TestFixture>]
    type RecordsAsModulesTest() =     

        [<Test>]
        member this.``Records-as-modules technique works well with simple results``() = 
            let (Choice1Of2 blog) =  Service.Blogging.getBlog 1 
                                        |> run 
                                        |> Async.RunSynchronously
            Assert.AreEqual (1, blog.id)

        [<Test>]
        member this.``Records-as-modules technique works well with compound results``() = 
            let (Choice1Of2 posts) =  Service.Blogging.getPosts 1 
                                        |> run 
                                        |> Async.RunSynchronously
            let p = posts |> List.head
            Assert.AreEqual(1, p.blogId)
            Assert.AreEqual(3, p.tags |> List.length)
            Assert.AreEqual(1, p.comments |> List.length)


