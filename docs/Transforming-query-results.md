# Transforming query results

The query result is always tabular. Even though ADO.NET allows to execute many SQL commands at once, their in-memory representation form much more complex, hierarchical structures. 

Fortunately, queries are functions and as such, they can be composed with other functions using `>>` and `|>` operators.
Thus, result transformations are functions too:
* one group is explicit but rather verbose
* another group, based on conventions, uses reflection and code generation to access key fields, etc...

Most transformations fall into four categories.

## Join two or more tabular results by some key

Function, returning two results, must return tuple:
```fsharp 
let getPostsWithTags: int -> AsyncDb<Post list * Tag list> = 
    sql "select id, blogId, name, title, content, author, 
                createdAt, modifiedAt, modifiedBy, status 
         from post 
         where blogId = @id;   
          
         select t.postId, t.name 
         from tag t join post p on t.postId = p.id 
         where p.blogId = @id"
```
The result can be transformed with the `join` function:

```fsharp 
let getPostsWithTags: int -> AsyncDb<Post list> = 
    sql "select id, blogId, name, title, content, author, 
                createdAt, modifiedAt, modifiedBy, status 
         from post 
         where blogId = @id;

         select t.postId, t.name 
         from tag t join post p on t.postId = p.id 
         where p.blogId = @id"
    >> AsyncDb.map (join (fun p -> p.id) 
                         (fun t -> t.postId) 
                         (fun p t -> { p with tags = t }))
```
Note, that we can not simply compose two functions, since the result is wrapped in the AsyncDb type.
For this reason, the transformation is passed to `AsyncDb.map` function.

## Join by convention

When following some simple naming conventions, i.e. 
* the join key has `Id`, `<Parent>Id` or '<Parent>_id` name in parent record (case insensitive), 
* the join key has `<Parent>Id` or `<Parent>_id` in child record (case insensitive), 
* exactly one proprty of `<Child>` list type exists in parent record,

the more concise join version can be used:
```fsharp 
let getPostsWithTags: int -> Post list AsyncDb = 
    sql "select id, blogId, name, title, content, author, 
                createdAt, modifiedAt, modifiedBy, status 
         from post 
         where blogId = @id;

         select t.postId, t.name 
         from tag t join post p on t.postId = p.id 
         where p.blogId = @id"
    >> AsyncDb.map join<_, Tag>
```

## Group rows of one denormalized result by some columns

The result, that is a product, can be represented as a list of tuples:

```fsharp 
let getPostsWithTags: int -> (Post * Tag) list AsyncDb = 
    sql "select p.id, p.blogId, p.name, p.title, p.content, p.author, 
                p.createdAt, p.modifiedAt, p.modifiedBy, p.status,
                t.postId as item_postId, t.name as item_name
         from post p left join tag t on t.postId = p.id
         where p.id = @id" 
```
It can be transformed using grouping by first item of a tuple:
```fsharp 
let getPostsWithTags: int -> Post list AsyncDb = 
    sql "select p.id, p.blogId, p.name, p.title, p.content, p.author, 
                p.createdAt, p.modifiedAt, p.modifiedBy, p.status,
                t.postId as item_postId, t.name as item_name
         from post p left join tag t on t.postId = p.id
         where p.id = @id" 
    >> AsyncDb.map (group (fun p t -> { p with tags = aliasedAsItem t }))
```
In the example above, there are two 'name' columns. To make it possible to use aliasing, we use `aliasedAsItem` function, that allows to use item_{column name} aliases for detail data. There are also `group3` and `group4` functions.

## Group by convention

There is, of course, convention-based approach, that works when no tranformation of child list is needed and only one child list collection appears in a parent record:
```fsharp 
let getPostsWithTags: int -> Post list AsyncDb = 
    sql "select p.id, p.blogId, p.name, p.title, p.content, p.author, 
                p.createdAt, p.modifiedAt, p.modifiedBy, p.status,
                t.postId, t.name as tagName
         from post p left join tag t on t.postId = p.id
         where p.id = @id" 
    >> AsyncDb.map group<_, Tag>    
```
## Consolidate many results to one root object

When reading one object and some detail data, no joining or grouping is needed. In this case, we can use ordinary function:
```fsharp 
let getBlogWithPosts: int -> Blog AsyncDb = 
    sql "select id, name, title, description, owner, 
                createdAt, modifiedAt, modifiedBy 
         from Blog 
         where id = @id;

         select id, blogId, name, title, content, author, 
                createdAt, modifiedAt, modifiedBy, status 
         from post 
         where blogId = @id"
    >> AsyncDb.map (fun b p -> { b with posts = p })
```
## Consolidate by convention

As with join, when exactly one property of `<Child> list` exists, more concise way can be used:
```fsharp 
let getBlogWithPosts: int -> Blog AsyncDb = 
    sql "select id, name, title, description, owner, 
                createdAt, modifiedAt, modifiedBy 
         from Blog 
         where id = @id;

         select id, blogId, name, title, content, author, 
                createdAt, modifiedAt, modifiedBy, status 
         from post 
         where blogId = @id"
    >> AsyncDb.map combine<_, Post>
```
## Combining many transformations

Sometimes more, than two results should be joined. In this case we can use a mechanism, that combine joins:
```fsharp 
let getPostsWithTagsAndComments: int -> Post list AsyncDb = 
    sql "select id, blogId, name, title, content, author, 
                createdAt, modifiedAt, modifiedBy, status 
         from post 
         where blogId = @id;

         select t.postId, t.name 
         from tag t join post p on t.postId = p.id 
         where p.blogId = @id;

         select c.id, c.postId, c.parentId, c.content, c.author, c.createdAt 
         from comment c join post p on c.postId = p.id 
         where p.blogId = @id"
    >> AsyncDb.map (join<_, Tag> >-> join<_, Comment>)
```
The `>->` operator combines two functions by passing result of the first one as a first argument of the second one:
```fsharp
let (>->) f g = fun (t1, t2) -> g(f t1, t2)
```
It allows to build a chain of functions, that subsequently transform result of the same type.

There is also another operator: `>>-`, that passes result of the second function as a second argument of the first function:
```fsharp
let (>>-) f g = fun (t1, t2) -> f(t1, g(t2))
```
It allows to define deep hierarchies.
In the example below the `>>-` operator is used to build nested hierarchy of posts with tags and comments, then add them to a blog:
```fsharp
let getBlogWithPostsWithTagsAndComments: int -> Blog AsyncDb = 
    sql "select id, name, title, description, owner, 
                createdAt, modifiedAt, modifiedBy 
         from Blog 
         where id = @id
          
         select id, blogId, name, title, content, author, 
                createdAt, modifiedAt, modifiedBy, status 
         from post 
         where blogId = @id;

         select t.postId, t.name 
         from tag t join post p on t.postId = p.id 
         where p.blogId = @id;

         select c.id, c.postId, c.parentId, c.content, c.author, c.createdAt 
         from comment c join post p on c.postId = p.id 
         where p.blogId = @id"
    >> AsyncDb.map (combine<_, Post> >>- (join<_, Tag> >-> join<_, Comment>))
```
It's also possible to mix joins, groups and updates in one result transformation.

## Transforming stored procedure result tuples

There are two functions transforming tuples, returned from stored procedures:
```fsharp 
resultOnly (_: int, (), result: 't)
```
that ignores return code and output parameters, and returns query results with possible mapping function, and:
```fsharp 
outParamsOnly (_: int, outParams, ())
```
that ignores return code and results, and returns output parameters.
They can be called the same way, as other transformations:

```fsharp 
let findPosts: (PostCriteria * SignatureCriteria) -> Post list AsyncDb =
    proc "FindPosts"
    >> AsyncDb.map resultOnly
```
