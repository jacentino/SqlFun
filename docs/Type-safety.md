# Type safety

## Simple queries

SqlFun relies on hand-written SQL and runtime code generation. It's not type-safe in a usual meaning.
But there is quite nice equivalent. Since all query functions are generated during their module initialization, we need only one test for each data access module. E.g. to type-check the module:
```fsharp 
    module Blogging =     
        let getBlog: int -> Blog AsyncDb = ...
        let getNumberOfPosts: int -> int AsyncDb = ... 
        let getPostAndItsComments: int -> (Post * Comment list) AsyncDb = ...
```
the followin test is enough:
```fsharp 
    [<Test>]
    member this.``Blogging module passes type checks``() = 
        let x = Blogging.getBlog
```
Accessing one module member triggers initialization of remaining members. During code generation SqlFun executes query in `SchemaOnly` mode and tries to generate all needed type conversions. Typos in SQL, incorrect parameters or return types result in `TypeInitializationException`. 

Unfortunately, the information about failing function is somewhere in the stack trace of the inner exception. To make it easier to find, the code accessing module can be wrapped in the `testQueries` function:

```fsharp 
    [<Test>]
    member this.``Blogging module passes type checks``() = 
        Testing.testQueries <| fun () ->  Blogging.getBlog
```

The downside of this technique is, that null checks cannot be performed this way.

## Error reports

Another downside of this approach is, that we cannot obtain full error report, since code generation breaks after first error.
To improve it, SqlFun has `logCompilationErrors` function, that intercepts code generation exceptions and writes them to mutable variable.
We can use it by configuring `sql` function differently.

```fsharp
    let compilationErrors = ref []
    let sql command = Diagnostics.logCompilationErrors compilationErrors sql command
```
The test can use it to launch code generation as well, as to obtain error report:

```fsharp 
    [<Test>]
    member this.``Blogging module passes type checks``() = 
        let report = Diagnostics.buildReport Blogging.compilationErrors
        Assert.IsEmpty(report, report)
```

## Composite queries

Unfortunately, composite queries are not checked during module initialization, since they must be defined as functions, not variables. Each of them should have its own test, sometimes even more, than one. My recommendation is to use [FsCheck](https://fscheck.github.io/FsCheck/) for testing them:
```fsharp
    type Arbs = 
        static member strings() =
            Arb.filter ((<>) null) <| Arb.Default.String()


    [<Test>]
    member this.``Composie queries can be tested with FsCheck``() = 
        
        let property criteria ordering = 
            buildQuery
            |> filterPosts criteria
            |> sortPostsBy (ordering |> List.distinctBy fst) // FsCheck generates duplicates
            |> selectPosts
            |> runAsync
            |> Async.RunSynchronously
            |> ignore

        let cfg = { Config.QuickThrowOnFailure with Arbitrary = [ typeof<Arbs> ] }
        Check.One(cfg, property)
```
The example above uses custom generator, since FsCheck produces nulls by default. When testing with FsCheck, the best way to define criteria is record with optional fields:
```fsharp
    type PostCriteria = {
        TitleContains: string option
        ContentContains: string option
        AuthorIs: string option
        HasTag: string option
        HasOneOfTags: string list
        HasAllTags: string list
        CreatedAfter: DateTime option
        CreatedBefore: DateTime option
    }
```
It's good for application logic as well. You can, for example define criteria on the client and pass them through the network without any intermediate structures.
