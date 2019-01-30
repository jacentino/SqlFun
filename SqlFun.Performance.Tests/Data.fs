namespace SqlFun.Performance.Tests

module Data = 
    open System
    open System.Data.SqlClient
    open System.Configuration
    open SqlFun.Queries

    type Comment = {
        id: int
        postId: int
        parentId: int option
        content: string
        author: string
        createdAt: DateTime
        replies: Comment list
    }

    [<CLIMutable>]
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
        status: string
        comments: Comment list
    }

    let createConnection () = new SqlConnection(ConfigurationManager.ConnectionStrings.["SqlFunTests"].ConnectionString)

    let generatorConfig = createDefaultConfig createConnection

