# Basic concepts

How to represent query in functional code?

Of course, as a function. Query parameters could be reflected as function parameters, its result as function return type. 

E.g. the query:
```sql
select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy 
from Blog 
where id = @id
```
could be implemented as a function of type:
```fsharp 
val getBlog: int -> IDbConnection-> Blog
```
Actually, instead of `IDbConnection`, SqlFun uses `DataContext` type, encapsulating connection and transaction:
```fsharp 
val getBlog: int -> DataContext -> Blog
```
SqlFun alows to generate such a function:
```fsharp 
let getBlog: int -> DataContext -> Blog = 
    sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy 
         from Blog 
         where id = @id"
```
where `sql` is a function responsible for building query functions using runtime code generation.
It generates:
* mappings between function parameters and query parameters
* mappings between query result and function result
* all needed type conversions
* command execution

We don't need any cache. Assigning generated functions to variables is perfectly enough.

A side-effect of code generation is a validation of sql commands and all necessary type checking (i.e. whether function parameter types match query parameter types and whether function and query results are compatible).

So, how to represent whole database API?

Functions described above don't relay on any state, so they don't need a class, carrying state.
That means, that the most natural way is set of modules. Couple of functions, that are cohesive from some point of view, can be grouped in a module:
```fsharp 
module Blogging =     
    let getBlog: int -> DataContext -> Blog = ...
    let getPosts: int -> DataContext -> Post list = ...
    let getComments: int -> DataContext -> Comment list = ...
```

And, since all variables are evaluated and assigned during the first access to the module contents, we obtain some level of type safety almost for free - it's enough to access one of them, without even calling it, to validate whole module.
So, writing one unit test per module give us type safety:

```fsharp
    [<Test>]
    member this.``Blogging module is valid``() = 
        Blogging.getBlog |> ignore
```
