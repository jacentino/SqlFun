namespace SqlFun

open System.Data

[<AutoOpen>]
module ComputationBuilder = 
    open System

    type DbAction<'t> = IDataContext -> 't

    module DbAction = 

        /// <summary>
        /// Function transforming value inside a monad.
        /// </summary>
        /// <param name="f">Function transforming a value.</param>
        /// <param name="v">Value wrapped in a monad.</param>
        let map (f: 't1 -> 't2) (v: DbAction<'t1>): DbAction<'t2> = 
            v >> f

        /// <summary>
        /// Wraps a database operation in a transaction.
        /// </summary>
        /// <param name="f">
        /// A function performing some database operation.
        /// </param>
        /// <param name="dc">
        /// The data context object.
        /// </param>
        let inTransaction (f: IDataContext -> 't) (dc: IDataContext) = 
            match dc.Transaction with
            | Some _ -> f dc
            | None ->
                use t = dc.BeginTransaction None
                let r = f t
                t.Commit()
                r

        /// <summary>
        /// Wraps a database operation in a transaction.
        /// </summary>
        /// <param name="isolationLevel">
        /// Transaction isolation level.
        /// </param>
        /// <param name="f">
        /// A function performing some database operation.
        /// </param>
        /// <param name="dc">
        /// The data context object.
        /// </param>
        let inTransactionWith (isolationLevel: IsolationLevel) (f: IDataContext -> 't) (dc: IDataContext) = 
            match dc.Transaction with
            | Some _ -> f dc
            | None ->
                use t = dc.BeginTransaction (Some isolationLevel)  
                let r = f t
                t.Commit()
                r

        /// <summary>
        /// Prepares data context and runs a function on it.
        /// </summary>
        /// <param name="createConnection">
        /// The function responsible for creating a database connection.
        /// </param>
        /// <param name="f">
        /// A function performing some database operation.
        /// </param>
        let run (createConnection: unit -> #IDbConnection) f = 
            let connection = createConnection()
            connection.Open()
            use dc = DataContext.create connection
            f (dc :> IDataContext)



    type AsyncDb<'t> = DbAction<Async<'t>>

    module AsyncDb = 

        /// <summary>
        /// Function transforming value inside a monad.
        /// </summary>
        /// <param name="f">Function transforming a value.</param>
        /// <param name="v">Value wrapped in a monad.</param>
        let map (f: 't1 -> 't2) (v: AsyncDb<'t1>): AsyncDb<'t2> = 
            fun ctx -> async {
                let! x = v ctx 
                return f x
            }

        /// <summary>
        /// Wraps a database operation in a transaction asynchronously.
        /// </summary>
        /// <param name="f">
        /// A function performing some database operation asynchronously.
        /// </param>
        /// <param name="dc">
        /// The data context object.
        /// </param>
        let inTransaction (f: IDataContext -> 't Async) (dc: IDataContext) = async {
            match dc.Transaction with
            | Some _ -> return! f dc
            | None -> 
                use t = dc.BeginTransaction None  
                let! r = f t
                t.Commit()
                return r
        }

        /// <summary>
        /// Wraps a database operation in a transaction asynchronously.
        /// </summary>
        /// <param name="isolationLevel">
        /// Transaction isolation level.
        /// </param>
        /// <param name="f">
        /// A function performing some database operation asynchronously.
        /// </param>
        /// <param name="dc">
        /// The data context object.
        /// </param>
        let inTransactionWith (isolationLevel: IsolationLevel) (f: IDataContext -> 't Async) (dc: IDataContext) = async {
            match dc.Transaction with
            | Some _ -> return! f dc
            | None -> 
                use t = dc.BeginTransaction (Some isolationLevel) 
                let! r = f t
                t.Commit()
                return r
        }        

        /// <summary>
        /// Prepares data context and runs a function on it asynchronously.
        /// </summary>
        /// <param name="createConnection">
        /// The function responsible for creating a database connection.
        /// </param>
        /// <param name="f">
        /// A function performing some database operation asynchronously.
        /// </param>
        let run (createConnection: unit -> #IDbConnection) f = 
            async {
                let connection = createConnection()
                connection.Open()
                use dc = DataContext.create connection
                return! f (dc :> IDataContext)
            }



    type DbActionBuilder() = 
            member this.Return(x: 't): DbAction<'t> = fun ctx -> x
            member this.ReturnFrom(x: DbAction<'t>): DbAction<'t> = x
            member this.Bind(x: DbAction<'t1>, f: 't1 -> DbAction<'t2>): DbAction<'t2> = fun ctx -> (f <| x ctx) ctx                   
            member this.Zero(x) = fun ctx -> ()
            member this.Combine(x: DbAction<'t1>, y: DbAction<'t2>) = this.Bind(x, fun x' -> y)
            member this.Delay(f) = f() 
            member this.For (items: seq<'t>,  f: 't -> DbAction<unit>): DbAction<unit> = 
                fun ctx ->
                    for x in items do 
                        f x ctx
            member this.Using(x: 't, f: 't -> DbAction<'u> when 't :> IDisposable) =
                fun ctx ->
                    try
                        f x ctx
                    finally
                        x.Dispose()




    let dbaction = DbActionBuilder()

    type AsyncDbBuilder() = 
            member this.Return(x: 't): AsyncDb<'t> = fun ctx -> async { return x }
            member this.ReturnFrom(x: AsyncDb<'t>): AsyncDb<'t> = x
            member this.Bind(x: AsyncDb<'t1>, f: 't1 -> AsyncDb<'t2>): AsyncDb<'t2> = 
                fun ctx -> async {
                        let! v = x ctx
                        return! (f v) ctx
                    }                    
            member this.Zero(x) = fun ctx -> async { return () }
            member this.Combine(x: AsyncDb<'t1>, y: AsyncDb<'t2>): AsyncDb<'t2> = this.Bind(x, fun x' -> y)
            member this.Delay(f: unit-> IDataContext -> 't Async) = fun ctx -> async { return! f () ctx }
            member this.For (items: seq<'t>,  f: 't -> AsyncDb<unit>): AsyncDb<unit> = 
                fun ctx -> async {
                    for x in items do 
                        do! f x ctx
                }
            member this.Using(x: 't, f: 't -> AsyncDb<'u> when 't :> IDisposable) =
                fun ctx -> async {
                    try
                        return! f x ctx
                    finally
                        x.Dispose()
                }


    let asyncdb = AsyncDbBuilder()
        
