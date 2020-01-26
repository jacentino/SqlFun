# Oracle array parameters

Array parameters is a mechanism allowing fast updates. It can be used with commands, that don't return results, i.e. INSERT, UPDATE and DELETE.

To make it work, each query parameter must be provided as an array:

```fsharp
let insertBlog: int[] -> string[] -> string[] -> string[] -> string[] -> DateTime[] -> DbAsync<unit> =
    sql "insert into blog (blogid, name, title, description, owner, createdAt) 
         values (:blogid, :name, :title, :description, :owner, :createdAt)"
```
All arrays must have the same length.

The command will be executed multiple times with array elements of subsequent indexes as parameters.

Array parameter based updates are significantly faster, than simple ones.

The extension works, when default oracle config is used:

```fsharp
let generatorConfig = Oracle.Config.createDefaultConfig createConnection
```
