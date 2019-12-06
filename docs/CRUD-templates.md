# CRUD templates

SqlFun allows to generate CRUD commands from a record structure:
```fsharp 
    let insertPost: Post -> AsyncDb<unit> = 
        sql <| Crud.Insert<Post> ["id"; "comments"; "tags"]

    let updatePost: Post -> AsyncDb<unit> = 
        sql <| Crud.Update<Post> (["id"], ["comments"; "tags"])

    let deletePost: int -> AsyncDb<unit> =
        sql <| Crud.DeleteByKey<Post> ["id"]

    let getPost: int -> AsyncDb<Post> = 
        sql <| Crud.SelectByKey<Post> ["id"]
```
By default, `Insert` and `Update` functions use all record fields, but it's possible to exclude some of them. All functions except `Insert` require parameters specifying key columns.
