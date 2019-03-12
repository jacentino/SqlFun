namespace SqlFun.Oracle.Tests

open System

module Data = 

    type Blog = {
        blogId: int
        name: string
        title: string
        description: string
        owner: string
        createdAt: DateTime
        modifiedAt: DateTime option
        modifiedBy: string option
    }


