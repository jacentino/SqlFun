# Processing large results

It's possible to iterate over large result without loading it as a whole into a memory.
The `ResultStream<'t>` class allows to read data sequentially. It keeps open reader until
the result is read to the end, or the stream is explicitly disposed.
To use the streaming functionality, it's enough to define query function with `ResultStream` as a result, e.g:

```fsharp
let getAllComments: int -> AsyncDb<ResultStream<Comment>> = 
    sql "select c.id, c.postId, c.parentId, c.content, c.author, c.createdAt 
         from comment c join post p on c.postId = p.id 
         where p.blogId = @id"
```
Since it implements `seq` interface, it can be iterated as any other collection:
```fsharp
asyncdb {
    for c in getAllComments blogId do
        ...
} |> runAsync 
```
When iterating whole result set, simple function call is enough, but if we intend to break iteration, 
the preferred way is to enclose the result stream in a use statement:

```fsharp
asyncdb {
    use! comments = getAllComments blogId 
    for c in comments do
        ...
} |> runAsync 
```
