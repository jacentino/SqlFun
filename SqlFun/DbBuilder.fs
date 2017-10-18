namespace SqlFun

[<AutoOpen>]
module ComputationBuilder = 

    type DbAction<'t> = DataContext -> 't
    type AsyncDbAction<'t> = DbAction<Async<'t>>

    type DbActionBuilder() = 
            member this.Return(x: 't): DbAction<'t> = fun ctx -> x
            member this.ReturnFrom(x: DbAction<'t>): DbAction<'t> = x
            member this.Bind(x: DbAction<'t1>, f: 't1 -> DbAction<'t2>): DbAction<'t2> = fun ctx -> (f <| x ctx) ctx                   
            member this.Zero(x: DbAction<'t>) = fun ctx -> ()
            member this.Combine(x: DbAction<'t1>, y: DbAction<'t2>) = this.Bind(x, fun x' -> y)
            member this.Delay(f) = f() 
            member this.For (items: seq<'t>,  f: 't -> DbAction<unit>): DbAction<unit> = 
                fun ctx ->
                    for x in items do 
                        f x ctx
                

    let dbaction = DbActionBuilder()

    type AsyncDbActionBuilder() = 
            member this.Return(x: 't): AsyncDbAction<'t> = fun ctx -> async { return x }
            member this.ReturnFrom(x: AsyncDbAction<'t>): AsyncDbAction<'t> = x
            member this.Bind(x: AsyncDbAction<'t1>, f: 't1 -> AsyncDbAction<'t2>): AsyncDbAction<'t2> = 
                fun ctx -> async {
                        let! v = x ctx
                        return! (f v) ctx
                    }                    
            member this.Zero(x) = fun ctx -> async { return () }
            member this.Combine(x: AsyncDbAction<'t1>, y: AsyncDbAction<'t2>): AsyncDbAction<'t2> = this.Bind(x, fun x' -> y)
            member this.Delay(f: unit-> 't) = fun ctx -> async { return! f () ctx }
            member this.For (items: seq<'t>,  f: 't -> AsyncDbAction<unit>): AsyncDbAction<unit> = 
                fun ctx -> async {
                    for x in items do 
                        do! f x ctx
                }


    let asyncdb = AsyncDbActionBuilder()
        
