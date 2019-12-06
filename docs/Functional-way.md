## Functional way

Accessing a database is producing side effects. It's unavoidable. But they can be controlled.

In Haskell, every impure function must return IO. Its closest equivalent in F# is Async.

Defining all query functions asynchronous, makes the code more functional:
```fsharp 
let getBlog: int -> DataContext -> Blog Async = 
    sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy 
         from Blog 
         where id = @id"
```
Unfortunately, it's not enough. There is another problem - connection management. Code like this:
```fsharp 
async {
    use ctx = createDataContext()
    let! blog = getBlog id ctx
    ...
}
```
is definitely not functional, since it relies on stateful resource. The solution is to encapsulate connection lifecycle management in some function:
```fsharp 
let blog = getBlog id |> run
```
It's pretty easy to implement - actually `run` function contains similar code as the criticized one, and the ugly part is hidden. 

But what about running more than one query on one open connection?

Defining some additional function allows it:
```fsharp 
(fun ctx ->
    let postId = insertPost post ctx
    insertTags postId tags ctx)
|> run
```
but doesn't look nice.

The solution lies in category theory. Functions of type `DataContext -> 't` are examples of Reader monad, and it's possible to define computation expression for composing them, like this:
```fsharp 
dbaction {
    let! postId = insertPost id
    do! insertTags postId tags
} |> run
```
The preferrable way is to use some mix of async and reader:
```fsharp 
asyncdb {
    let! postId = insertPost id
    do! insertTags postId tags
} |> runAsync
```
For convenience, functions having one `DataContext` parameter are aliased

* `DbAction<'t>` for synchronous results (i.e. `DataContext -> 't`)
* `AsyncDb<'t>` for asynchronous ones (i.e. `DataContext -> 't Async`)
    
