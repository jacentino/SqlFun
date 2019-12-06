# Bulk operations with MS SQL TVP parameters

One of the coolest features of ADO.NET provider are table-valued parameters. They greatly simplify bulk operations. 
Create user defined table type, and you can pass data table as parameter, if it reflects the table type structure.
```sql
create type dbo.Tag as table(
	postId int not null,
	name nvarchar(50) not null
)
```
With SqlFun, you can use TVP-s even easier way, since you can use list of records, instead of a data table:
```fsharp 
let updateTags: int -> Tag list -> DataContext -> unit = 
    sql "delete from tag where postId = @id;
         insert into tag (postId, name) select postId, name from @tags"
```
Lists, that are parts of some record, can be used too:
```fsharp 
let updateTags: Post -> DataContext -> unit = 
    sql "delete from tag where postId = @id;
         insert into tag (postId, name) select postId, name from @tags"
```
To make this feature available, you have to define sql and storedproc functions using defaults from SqlFun.MsSql instead of SqlFun.Queries:
```fsharp 
let createConnection () = new SqlConnection(connectionString)
let generatorConfig = MsSql.createDefaultConfig createConnection

let sql commandText = sql generatorConfig commandText

let proc procName = proc generatorConfig procName
```
