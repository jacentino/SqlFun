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

    let connectionString = ConfigurationManager.ConnectionStrings.["<your database config entry>"].ConnectionString
    let createConnection () = new SqlConnection(connectionString)

and wire it up by partial application of functions responsible for generating and executing queries:
 
    let run f = DataContext.run createConnection f

    let runAsync f = DataContext.runAsync createConnection f
    
    let sql commandText = sql createConnection defaultParamBuilder commandText

    let storedproc name = storedproc createConnection defaultParamBuilder name

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
### Result transformations
### Utilizing `dbaction` and `asyncdb` computation expressions

## Features
## Supported databases
