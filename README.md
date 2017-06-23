# SqlFun
Idiomatic data access for F#

## Principles

* Plain Old SQL
* Type safety
* Composability
* Performance
* Compliance with FP paradigm

## How it works
### Prerequisites
First step is to define function creating database connection,

    let createConnection () = new SqlConnection(<your database connection string>)

and wire it up with functions responsible for generating queries (using partial application):
 
    let sql commandText = sql createConnection defaultParamBuilder commandText

    let storedproc name = storedproc createConnection defaultParamBuilder name

and executing them:

    let run f = DataContext.run createConnection f

    let runAsync f = DataContext.runAsync createConnection f
    

### Data structures
Then, data structures should be defined for results of your queries.

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
    
The most preferrable way is to use F# record types.    
    
### Queries
The preferrable way of defining queries is to define them as variables and place in a module.

    module Blogging =    
 
        let getBlog: int -> DataContext -> Blog = 
            sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy 
                 from Blog 
                 where id = @id"
            
        let getPosts: int -> DataContext -> Post list) = 
            sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status 
                 from post 
                 where blogId = @blogId"
        
The functions executing queries are generated during a first access to the module contents.

At that stage, all the type checking is performed, so it's easy to make type checking part of automatic testing - one line of code for each module is needed.

The generating process uses reflection heavily, but no reflection is used while processing a query.

### Executing queries
Since your queries have a DataContext as last parameter, they can be passed to the `run` function after applying preceding parameters.

    let blog = Blogging.getBlog 1 |> run

### Async support
The query can be defined as asynchronous:

        let getBlog: int -> DataContext -> Blog Async = 
            sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy 
                 from Blog 
                 where id = @id"

and then, executed as async:

    async {
        let! blog = Blogging.getBlog 1 |> runAsync
        ...
    }

### Result transformations
Since the ADO.NET allows to execute many sql commands at once, it's possible to utilize it with SqlFun. The result is a tuple:

        let getBlogWithPosts: int -> DataContext -> Blog * Post list = 
            sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy 
                 from Blog 
                 where id = @id;
                 select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status 
                 from post 
                 where blogId = @id"
 
 Since the call of `sql` returns some function, it can be composed with another function, possibly performing result transformations.
 Let extend the blog type with a `posts: Post list` property. In this case, we can combine two results using blog id as a key:
 
        let getBlogWithPosts: int -> DataContext -> Blog = 
            sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy 
                 from Blog 
                 where id = @id;
                 select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status 
                 from post 
                 where blogId = @id"
            >> join (fun b -> b.id) (fun p -> p.blogId) (fun b pl -> { b with posts = pl })
            |> curry  

The `curry` function is required because the function composition operator (>>) accepts only one-arg functions.

### Utilizing `dbaction` and `asyncdb` computation expressions

## Features
## Supported databases
In its core SqlFun does not use any features specific to some db provider, so it works with all the ADO.NET providers.
There is an extension for MS SQL, that allows to use table valued parameters, and another extension for PostgreSQL, making array parameters possible. 
