namespace SqlFun.Tests

open NUnit.Framework
open SqlFun
open Data
open SqlFun.Transforms
open SqlFun.Transforms.Conventions
open Common
open System
open LazyExtensions

module ClassesAsModules = 

    type Result<'t> = Choice<'t, exn>

    let checkFailure f r = 
        match r with
        | Choice1Of2 _ -> ()
        | Choice2Of2 e -> f e

    type IEnv =
        abstract member GetService: unit -> 't
        abstract member WithService: 't Lazy -> IEnv

    type AsyncReader<'env, 't> = 'env -> Async<'t>
    type AsyncReader<'t> = IEnv -> Async<'t>

    type AsyncReaderBuilder() = 

            member this.Return(x: 't): AsyncReader<'env, 't> = fun env -> async { return x }

            member this.ReturnFrom<'envC, 't, 'env when 'env :> IEnv>(x: AsyncReader<'envC, 't>): AsyncReader<'env, 't> = fun env -> x (env.GetService())

            member this.Bind<'env, 't1, 'c1, 't2, 'c2 when 'env :> IEnv>(x: AsyncReader<'c1, 't1>, f: 't1 -> AsyncReader<'c2, 't2>): AsyncReader<'env, 't2> = 
                fun env -> async {
                        let! v = x (env.GetService())
                        return! (f v) (env.GetService())
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

        type IBlogging = 
            abstract member getBlog: (int -> AsyncDb<Blog>)
            abstract member getPosts: (int -> AsyncDb<Post list>)

        type BloggingImpl() =      

            interface IBlogging with
                member val getBlog = 
                    sql"select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy from Blog where id = @id"
                member val getPosts = 
                    sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post where blogId = @id;
                         select t.postId, t.name from tag t join post p on t.postId = p.id where p.blogId = @id;
                         select c.id, c.postId, c.parentId, c.content, c.author, c.createdAt from comment c join post p on c.postId = p.id where p.blogId = @id"
                    >> AsyncDb.map (join<_, Tag> >-> (mapSnd Tooling.buildTree >> join<_, Comment>) >> List.ofSeq)
                    

    module Service = 

        type IBlogging = 
            abstract member getBlog: int -> (AsyncReader<Blog>)
            abstract member getPosts: int -> (AsyncReader<Post list>)
        

        type BloggingImpl(data: Data.IBlogging) =

            interface IBlogging with
                member x.getBlog id = asyncenv { return! data.getBlog id }
                member x.getPosts id = asyncenv { return! data.getPosts id }

    module CompositionRoot = 

        type Environment(services: (Type * Lazy<obj>) list) = 
        
            new() = 
                let services = [ 
                    typeof<DataContext>, lazy let con = createConnection()
                                              con.Open()
                                              box <| DataContext.create con                                            
                ]
                new Environment(services)

            interface IEnv with
                member this.WithService (service: 't Lazy) =
                    new Environment((typeof<'t>, lazy box (service.Force())) :: services) :> IEnv

                member this.GetService (): 't = 
                    (typeof<Environment>, lazy box this) :: services 
                    |> List.tryFind (fst >> typeof<'t>.IsAssignableFrom)
                    |> Option.map (fun (_, srv) -> srv.Value :?> 't)
                    |> Option.defaultWith (fun () -> failwithf "Service of type %O is not available." typeof<'t>)
            
            interface IDisposable with
                member this.Dispose() = 
                    services
                    |> Seq.map snd
                    |> Seq.filter (fun srv -> srv.IsValueCreated && srv.Value :? IDisposable)
                    |> Seq.iter (fun srv -> (srv.Value :?> IDisposable).Dispose())


        type EnvRunner() = 
            member x.Run f = async {
                use env = new Environment()
                return! f env
            }
            member x.InTransaction f (env: IEnv) = async {
                let f' txn = f (env.WithService(lazy txn))
                return! AsyncDb.inTransaction f' (env.GetService())
            }
                

        let Env = EnvRunner()

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
            let Blogging = Data.BloggingImpl() :> Data.IBlogging

        module Service = 
            let private blogging = Service.BloggingImpl(Data.Blogging) :> Service.IBlogging
            
            let Blogging = {
                new Service.IBlogging with
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


