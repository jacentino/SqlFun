namespace SqlFun.Tests

open NUnit.Framework
open SqlFun
open Data
open SqlFun.Exceptions
open SqlFun.Transforms
open SqlFun.Transforms.Conventions
open Common
open System

module ClassesAsModules = 

    type Result<'t> = Choice<'t, exn>

    let checkFailure f r = 
        match r with
        | Choice1Of2 _ -> ()
        | Choice2Of2 e -> f e

    type AsyncReader<'env, 't> = 'env -> Async<'t>

    type AsyncReaderBuilder() = 

            member this.Return(x: 't): AsyncReader<'env, 't> = fun env -> async { return x }

            member this.ReturnFrom(x: AsyncReader<'env, 't>): AsyncReader<'env, 't> = x

            member this.Bind(x: AsyncReader<'env, 't1>, f: 't1 -> AsyncReader<'env, 't2>): AsyncReader<'env, 't2> = 
                fun env -> async {
                        let! v = x env
                        return! (f v) env
                    }                    

            member this.Zero(x) = fun env -> async { return () }

            member this.Combine(x: AsyncReader<'env, 't1>, y: AsyncReader<'env, 't2>): AsyncReader<'env, 't2> = 
                this.Bind(x, fun x' -> y)

            member this.Delay(f: unit-> AsyncReader<'env, 't2>) = fun env -> async { return! f () env }

            member this.For (items: seq<'t>,  f: 't ->AsyncReader<'env, unit>): AsyncReader<'env, unit> = 
                fun env -> async {
                    for x in items do 
                        do! f x env 
                }


    let asyncenv = AsyncReaderBuilder()



    module Data = 

        let mapEnv m f p deps = f(p, m deps)

        type IBlogging<'env> = 
            abstract member getBlog: (int -> AsyncReader<'env, Blog>)
            abstract member getPosts: (int -> AsyncReader<'env, Post list>)

        type BloggingImpl<'env>(extract: 'env -> DataContext) =      
            let mapEnv f p env = mapEnv extract f p env

            interface IBlogging<'env> with
                member val getBlog = 
                    sql"select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy from Blog where id = @id"
                    |> mapEnv
                member val getPosts = 
                    sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post where blogId = @id;
                         select t.postId, t.name from tag t join post p on t.postId = p.id where p.blogId = @id;
                         select c.id, c.postId, c.parentId, c.content, c.author, c.createdAt from comment c join post p on c.postId = p.id where p.blogId = @id"
                    >> mapAsync (join<_, Tag> >-> (mapSnd Tooling.buildTree >> join<_, Comment>))
                    |> mapEnv

    module Service = 

        type Result<'t> = Choice<'t, exn>

        type IBlogging<'env> = 
            abstract member getBlog: int -> (AsyncReader<'env, Blog>)
            abstract member getPosts: int -> (AsyncReader<'env, Post list>)
        

        type BloggingImpl<'env>(data: Data.IBlogging<'env>) =

            interface IBlogging<'env> with
                member x.getBlog id = data.getBlog id
                member x.getPosts id = data.getPosts id 

    module CompositionRoot = 

        type Environment = { dataContext: DataContext }

        type EnvImpl() = 
            member x.Run f = async {
                return! runAsync (fun ctx -> f { dataContext = ctx })
            }
            member x.InTransaction f env = async {
                let f' txn = f { env with dataContext = txn }
                return! DataContext.inTransactionAsync f' env.dataContext
            }
                

        let Env = EnvImpl()

        let withLogging msg (f: 'env -> 't Async) env = async {
            let sw = System.Diagnostics.Stopwatch()
            try
                sw.Start()
                let! res = f env
                printf "[%O] %s" sw.Elapsed msg
                return res
            with e ->
                printf "[%O] %s: %O" sw.Elapsed msg e                
                raise <| Exception("Rethrow", e)
                return! f env
        } 

        let withLoggingF v = Printf.kprintf (fun msg -> withLogging msg) v

        module Data = 
            let Blogging = Data.BloggingImpl<Environment>(fun d -> d.dataContext) :> Data.IBlogging<Environment>

        module Service = 
            let private blogging = Service.BloggingImpl<Environment>(Data.Blogging) :> Service.IBlogging<Environment>
            
            let Blogging = {
                new Service.IBlogging<Environment> with
                    member x.getBlog id = 
                        blogging.getBlog id  
                        |> withLoggingF "Blogging.getBlog %d" id
                    member x.getPosts id = 
                        blogging.getPosts id
                        |> Env.InTransaction
                        |> withLoggingF "Blogging.getPosts %d" id
            }
             
    open CompositionRoot
    open CompositionRoot.Service
            
    [<TestFixture>]
    type ClassesAsModulesTest() =     

        [<Test>]
        member this.``Classes-as-modules technique works well with simple results``() = 
            asyncenv {
                let! blog  = Blogging.getBlog 1
                Assert.AreEqual (1, blog.id)
            }
            |> Env.Run 
            |> Async.RunSynchronously
            

        [<Test>]
        member this.``Classes-as-modules technique works well with compound results``() = 
            asyncenv {
                let! posts =  Blogging.getPosts 1 
                let p = posts |> List.head
                Assert.AreEqual(1, p.blogId)
                Assert.AreEqual(3, p.tags |> List.length)
                Assert.AreEqual(1, p.comments |> List.length)
            }
            |> Env.Run 
            |> Async.RunSynchronously


