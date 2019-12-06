# Executing queries

To execute query, open connection to a database is needed. To provide it, we can use one of query execution methods:
```fsharp 
    let blog = getBlog id |> run
```
for synchronous execution, or
```fsharp 
    async {
        let! blog = getBlog id |> runAsync
        ...
    }
```
for asynchronous one.

When more than one query should be executed in context of one open connection, the function with `DataContext` parameter can be defined:
```fsharp 
    let insertPostWithTags (p: Post) (ctx: DataContext) = 
        let postId = insertPost p ctx
        insertTags postId p.tags ctx

    insertPostWithTags p |> run
```
But there is even better approach. Functions of type `DataContext -> 't` are examples of Reader monad and it's possible to create computation expression for them. SqlFun provides one:
```fsharp 
    dbaction {
        let! postId = insertPost p
        do! insertTags postId p.tags 
    } |> run
```
There is also asynchronous version:
```fsharp 
    asyncdb {
        let! postId = insertPost p
        do! insertTags postId p.tags
    } |> runAsync |> Async.Start
```
Of course, nothing prevents you from creating connection manually, and executing query within a `use` block, but I strongly believe, that it's not a proper way of doing things in functional language.
