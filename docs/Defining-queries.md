# Defining queries

## Query functions

SqlFun provides two functions responsible for defining queries:
```fsharp 
val sql<'q> (commandText: string): 'q
```
that generates a function of type `'q` executing query specified by the `commandText` parameter.
The second function:
```fsharp 
val proc<'q> (procedureName: string): 'q
```
that generates a function of type `'q` executing stored procedure specified by `procedureName` parameter.
Typically, these functions are used to define variables of modules, responsible for data access:
```fsharp 
module Blogging = 
        
   let getBlog: int -> DataContext -> Blog Async = 
       sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy
            from Blog 
            where id = @id"

   let getPostsWithTags: int -> DataContext -> Async<Post list * Tag list> = 
       sql "select id, blogId, name, title, content, author, 
                   createdAt, modifiedAt, modifiedBy, status 
            from post 
            where blogId = @id;   
          
            select t.postId, t.name 
            from tag t join post p on t.postId = p.id 
            where p.blogId = @id"

   let findPosts: PostSearchCriteria -> DataContext -> Async<int * unit * Post list> =
       proc "FindPosts"
```
Queries can return multiple results. Corresponding result structures must be tuples with the same number of elements.

Additionally, functions executing stored procedures have to return 3-element tuples, since stored procedures return integer code, output parameters and query results.

**The one common constraint of query function is, that it must contain a parameter of the `IDbConnection` type (potentially indirectly) and if it can be executed within a transaction, `IDbTransaction` type parameter**. For convenience, there is the `DataContext` structure, that satisfies these constraints, since it contains fields of these type.  

`DataContext` parameter can be hidden for readeability using `DbAction` or `AsyncDb` type alias, e.g.:
```fsharp
module Blogging = 
        
   let getBlog: int -> Blog AsyncDb = 
       sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy
            from Blog 
            where id = @id"

   let findPosts: PostSearchCriteria -> AsyncDb<int * unit * Post list> =
       proc "FindPosts"
```
## Parameters

Function parameters are mapped to query parameters. Valid parameter types are:

* basic types
* enums
* records
* tuples
* lists/arrays/sets/sequences of records (for MsSql extension)
* lists/arrays/sets/sequences of basic types (when collection parameter extensions re used)
* arrays of basic types (for PostgreSQL and Oracle extensions)
* options

Tuple elements and parameters of basic types are mapped positionally, fields of records are mapped by name.
When mapping records with fields of record type, hierarchy is not reflected in name, unless the `Prefixed` attribute is used. Each query parameter must be reflected by some function parameter.

In the simplest case, some simple value representing record id or another column search value can be passed as a parameter:

```fsharp
let getPostsCreatedBefore: DateTimeOffset -> Blog list AsyncDb = 
    sql "select id, blogId, name, title, content, author, 
                createdAt, modifiedAt, modifiedBy, status 
         from Post
         where createdAt < @createdAt"
```

When more, than one parameter is used, subsequent function parameters are matched with query parameters positionally.
They can not be matched by name, since function args have no names:

```fsharp
let getPostsCreatedBefore: int -> DateTimeOffset -> Blog list AsyncDb = 
    sql "select id, blogId, name, title, content, author, 
                createdAt, modifiedAt, modifiedBy, status 
         from Post
         where blogId = @blogId and createdAt < @createdAt"
```

It means, that query parameter names have no meaning, unless some parameter is used more than once (only the first occurance is matched with function parameter).

The same stands for tuples of basic types:

```fsharp
let getPostsCreatedBefore: (int * DateTimeOffset) -> Blog list AsyncDb = 
    sql "select id, blogId, name, title, content, author, 
                createdAt, modifiedAt, modifiedBy, status 
         from Post
         where blogId = @blogId and createdAt < @createdAt"
```

Parameters can also be records, representing search criteria or data to insert/update:

```fsharp
type PostSearchCriteria = {
    blogId: int option
    title: string option
    content: string option
}

type SignatureCriteria = {
    author: string option
    createdAtFrom: DateTime option
    createdAtTo: DateTime option
    modifiedAtFrom: DateTime option
    modifiedAtTo: DateTime option
    modifiedBy: string option
    status: PostStatus option
}
```

Record fields are matched with query parameters by name:

```fsharp
let findPostsByCriteria: PostSearchCriteria -> Post list AsyncDb = 
    sql "select id, blogId, name, title, content, author, 
                createdAt, modifiedAt, modifiedBy, status 
         from post
         where (blogId = @blogId or @blogId is null)
           and (title like '%' + @title + '%' or @title is null)
           and (content like '%' + @content + '%' or @content is null)"
```

When more, than one record is used (e.g. tuple of records), matching is a mix of positional and name based mapping:

```fsharp
let findPostsByCriteria: (PostSearchCriteria * SignatureCriteria) -> Post list AsyncDb = 
    sql "select id, blogId, name, title, content, author, 
                createdAt, modifiedAt, modifiedBy, status 
         from post
         where (blogId = @blogId or @blogId is null)
           and (title like '%' + @title + '%' or @title is null)
           and (content like '%' + @content + '%' or @content is null)
           and (author = @author or @author is null)
           and (createdAt >= @createdAtFrom or @createdAtFrom is null)
           and (createdAt <= @createdAtTo or @createdAtTo is null)
           and (modifiedAt >= @modifiedAtFrom or @modifiedAtFrom is null)
           and (modifiedAt <= @modifiedAtTo or @modifiedAtTo is null)
           and (status = @status or @status is null)"
```

fields of each record are matched by name, but all fields of a first record must occur in a query before any field of
a second record.

Apart from this default mechanism, there are extensions, that allow to:

* use collections of simple values as parameters ([listParamBuilder](https://github.com/jacentino/SqlFun/wiki/Non-standard-parameter-conversions) must be used):

```fsharp
let getSomePostsByTags: string list -> Post list AsyncDb = 
    sql "select id, blogId, p.name, title, content, author, 
                createdAt, modifiedAt, modifiedBy, status 
         from post p join tag t on t.postId = p.id
         where t.name in (@tagName)
         group by id, blogId, p.name, title, content, author, 
                  createdAt, modifiedAt, modifiedBy, status"
```

* use collections of records ([works with MSSQL only, MsSql.createDefaultConfig must be used](https://github.com/jacentino/SqlFun/wiki/Bulk-operations-with-MS-SQL-TVP-parameters)):

```fsharp
let updateTags: int -> Tag list -> unit AsyncDb = 
    sql "delete from tag where postId = @id;
         insert into tag (postId, name) select @id, name from @tags"
```

## Results

Query results are mapped to function return types. Valid return types are:

* basic types
* enums
* records
* tuples (used for queries returning multiple results)
* lists/arrays/sets/sequences of records
* lists/arrays/sets/sequences of tuples
* options
* ResultStream for queries returning single results

Valid record field types are:
* basic types
* enums
* records
* tuples of records
* lists/arrays/sets/sequences of records
* lists/arrays/sets/sequences of tuples
* options

Consider following record type:

```fsharp
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

The query function result can be `Blog` itself, `option<Blog>`, `list<Blog>`, `array<Blog>`, [`ResultStream<Blog>`](https://github.com/jacentino/SqlFun/wiki/Processing-large-results), or even `list<Blog * Post>`, wrapped in `DbAction` or `AsyncDb`.

The record can contain subrecords:

```fsharp
type Signature = {
    owner: string
    createdAt: DateTime
    modifiedAt: DateTime option
    modifiedBy: string option
} 

type Blog = {
    id: int
    name: string
    title: string
    description: string
    signature: Signature
    posts: Post list
}
```
If some subrecord is used more, than once, it can be prefixed to avoid name clashes:

```fsharp
type Blog = {
    id: int
    name: string
    title: string
    description: string
    [<Prefixed("blog_sig_")>] signature: Signature
    posts: Post list
}
```
In this case, names of columns, corresponding to `Signature` fields, must be `blog_sig_owner`, `blog_sig_createdAt`, `blog_sig_modifiedAt` and `blog_sig_modifiedBy`. `Prefixed` attribute can be also specified without parameter. In this case, column names are prefixed with field name of subrecord ("signature" in the above example).

Each return type element must be represented by some result value, except record fields of collection types, i.e. `list`, `array`, `set` and `sequence`. They are meant as 
a basis for result [transformations](Transforming-query-results), e.g. joining two lists by key, etc.
