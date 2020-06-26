SqlFun is a tool for writing data access code in F# functional way. 
It's fast, type safe and gives you all powers of SQL, no custom query language constraints you.
It's also lightweight, you need to know a [general idea](https://github.com/jacentino/SqlFun/wiki/Basic-concepts) and few functions (and, of course SQL).

It's available as a [Nuget package](https://www.nuget.org/packages/SqlFun/)
There are also extensions for [PostgreSQL](https://www.nuget.org/packages/SqlFun.NpgSql) and [Oracle](https://www.nuget.org/packages/SqlFun.Oracle) databases.

## Features
* [Works with any ADO.NET provider](#supported-databases)
* [All SQL features available](Defining-queries#query-language)
* [Type safety](Type-safety)
* [High performance](Performance)
* [Compound, hierarchical query parameters](Defining-queries#parameters)
* [Compound, hierarchical query results](Defining-queries#results)
* [Support for parameter conversions](Custom-conversions)
* [Support for result transformations](Transforming-query-results)
* [Support for enum types](Enum-support)
* [Asynchronous queries](#async-support)
* [Composable, template-based queries](Composite-queries)
* [Auto-generated CRUD operations](CRUD-templates)
* [Computation expressions for connection and transaction handling](#utilizing-dbaction-and-asyncdb-computation-expressions)
* [Support for large dataset processing](Processing-large-results)

## Supported databases
In its core SqlFun does not use any features specific to some db provider, so it works with any ADO.NET provider. 
The only limitation is properly working commands executed in `SchemaOnly` mode.

It was tested against MS SqlServer, PostgreSQL, Oracle, MySQL and SQLite.

There are four extensions, enabling provider-specific features:
* the extension for MS SQL, that allows to use table valued parameters
* the extension for PostgreSQL, making use of array parameters possible and adding more comfortable Bulk Copy mechanism
* the extension for Oracle, adding some adaptations, like binding parameters by name, and allowing to use array parameters
* the extension for SQLite, that allows to use date and time values


## How it works
Most of us think about data access code as a separate layer. We don't like to spread SQL queries across all the application.
Better way is to build an API exposing your database, consisting of structures representing database data, and functions responsible for processing this data (great object-oriented example is [Insight.Database](https://github.com/jonwagner/Insight.Database/wiki/Auto-Interface-Implementation) automatic interface implementation). SqlFun makes it a design requirement.

### Installation
SqlFun can be added to your solution from Package Manager Console:

```powershell
PM> Install-Package SqlFun
```

### Configuration
First step is to define function creating database connection and config record:
```fsharp
let createConnection () = new SqlConnection(<your database connection string>)
let generatorConfig = createDefaultConfig createConnection
```
and wire it up with functions responsible for generating queries (using partial application):
```fsharp 
let sql commandText = sql generatorConfig commandText

let proc name = proc generatorConfig name
```
and for executing them:
```fsharp 
let run f = DbAction.run createConnection f

let runAsync f = AsyncDb.run createConnection f
```    
### Data structures
Then, data structures should be defined for results of your queries.
```fsharp 
type Post = {
    id: int
    blogId: int
    name: string
    title: string
    content: string
    author: string
    createdAt: DateTime
    modifiedAt: DateTime option
    modifiedBy: string option
    status: PostStatus
}
    
type Blog = {
    id: int
    name: string
    title: string
    description: string
    owner: string
    createdAt: DateTime
    modifiedAt: DateTime option
    modifiedBy: string option
    posts: Post list
}
```    
The most preferrable way is to use F# record types. Record fields should reflect query result columns, because they are mapped by name.
    
### Defining queries
The best way of defining queries is to create variables for them and place in some module:
```fsharp 
module Blogging =    
 
    let getBlog: int -> DbAction<Blog> = 
        sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy 
             from Blog 
             where id = @id"
            
    let getPosts: int -> DbAction<Post list> = 
        sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status 
             from post 
             where blogId = @blogId"
```        
The functions executing queries are generated during a first access to the module contents. 

At that stage, all the type checking is performed, so it's easy to make type checking part of automatic testing - one line of code for each module is needed.

The generating process uses reflection heavily, but no reflection is used while processing a query, since generated code is executed.

### Executing queries
Since your queries return `DbAction<'t>`, they can be passed to the `run` function after applying preceding parameters.
```fsharp 
let blog = Blogging.getBlog 1 |> run
```
### Async support
The preferrable way is to define query as asynchronous:
```fsharp 
let getBlog: int -> AsyncDb<Blog> = 
    sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy 
         from Blog 
         where id = @id"
```
and then, execute as async:
```fsharp 
async {
    let! blog = Blogging.getBlog 1 |> runAsync
    ...
}
```
### Result transformations
Since the ADO.NET allows to execute many sql commands at once, it's possible to utilize it with SqlFun. The result is a tuple:
```fsharp 
let getBlogWithPosts: int -> AsyncDb<Blog * Post list> = 
    sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy 
         from Blog 
         where id = @id;
         select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status 
         from post 
         where blogId = @id"
 ```
 The call of `sql` returns some function, thus it can be composed with another function, possibly performing result transformations.
 Let extend the blog type with a `posts: Post list` property. In this case, two results can be combined with simple function:
 ```fsharp 
let getBlogWithPosts: int -> AsyncDb<Blog> = 
    sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy 
         from Blog 
         where id = @id;
         select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status 
         from post 
         where blogId = @id"
    >> AsyncDb.map (fun b pl -> { b with posts = pl })
```
In simple cases, when code follows conventions, transormations can be specified more declarative way:

```fsharp 
let getBlogWithPosts: int -> AsyncDb<Blog> = 
    sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy 
         from Blog 
         where id = @id;
         select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status 
         from post 
         where blogId = @id"
    >> AsyncDb.map combine<_, Post>
```
There are also functions that allow to combine multi-row results by joining many results or grouping wide results.

### Compound parameters
Records can be parameters as well:
```fsharp 
let insertPost: Post -> AsyncDb<int> = 
    sql "insert into post 
                (blogId, name, title, content, author, createdAt, status)
         values (@blogId, @name, @title, @content, @author, @createdAt, @status);
         select scope_identity()"
```
The record fields are mapped to query parameters by name.

### Stored procedures
The result of a function calling stored procedure should be a three-element tuple (return code, output params, result):
```fsharp 	
let findPosts: (PostSearchCriteria * SignatureSearchCriteria) -> AsyncDb<int * unit * Post list> =
    proc "FindPosts"
```
but there are transformers, that allow to ignore parts of it:
```fsharp 
let findPosts: (PostSearchCriteria * SignatureSearchCriteria) -> Post list AsyncDb =
    proc "FindPosts"
    >> AsyncDb.map resultOnly
```	 
### Utilizing `dbaction` and `asyncdb` computation expressions
It's easy to execute one query with `runAsync` or `run` function. To execute more queries in a context of one open connection, computation expression can be used:
```fsharp 
asyncdb {
    let! postId = Blogging.insertPost post
    do! Blogging.insertComments postId comments
    do! Blogging.insertTags postId tags
} |> runAsync
```    
The synchronous equivalent of this expression is `dbaction`.

### Transactions
To execute some queries in transaction, the `inTransaction` function should be used:
```fsharp 
asyncdb {
    let! postId = Blogging.insertPost post
    do! Blogging.insertComments postId comments
    do! Blogging.insertTags postId tags
} 
|> AsyncDb.inTransaction
|> runAsync
```
Its synchronous equivalent is `DbAction.inTransaction`.

## Documentation & examples 

For more detail documentation refer sections under **Basic Usage** topic, starting from  [Configuration](Configuration) section.

For more examples refer [test project](https://github.com/jacentino/SqlFun/tree/master/SqlFun.Tests).


