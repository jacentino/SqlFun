# PostgreSQL array parameters

The NpgSql extension allows to use PostgreSQL array parameters. Just define function with an array parameter:
```fsharp 
let getPostsByIds: int array -> DataContext -> Post list = 
    sql "select p.postid, .blogId, p.name, p.title, p.content, 
                p.author, p.createdAt, p.modifiedAt, p.modifiedBy, p.status
         from post p join unnest(@ids) ids on p.postid = ids"
```
To make it work, you have to write initialization code another way:
```fsharp 
let createConnection () = new NpgsqlConnection(connectionString)
let generatorConfig = NpgSql.Config.createDefaultConfig createConnection

let sql commandText = sql generatorConfig commandText

let proc name = proc generatorConfig name
```
