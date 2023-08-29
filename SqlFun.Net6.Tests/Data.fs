namespace SqlFun.Net6.Tests

open System
open SqlFun
open Common

module Data = 
    
    type PostStatus = 
        | [<EnumValue("N")>] New = 0
        | [<EnumValue("P")>] Published = 1
        | [<EnumValue("A")>] Archived = 2

    type Comment = {
        id: int
        postId: int
        parentId: int option
        content: string
        author: string
        createdAt: DateOnly
    }

    type Comment2 = {
        id: int
        postId: int
        parentId: int option
        content: string
        author: string
        createdAt: TimeOnly
    }

    type Comment3 = {
        id: int
        postId: int
        parentId: int option
        content: string
        author: string
        createdAt: DateOnly option
    }

    type Comment4 = {
        id: int
        postId: int
        parentId: int option
        content: string
        author: string
        createdAt: TimeOnly option
    }

    type Post = {
        id: int
        blogId: int
        name: string
        title: string
        content: string
        author: string
        createdAt: DateOnly
        modifiedAt: DateOnly option
        modifiedBy: string option
        status: PostStatus
    }

module Tooling = 
    
    let cleanup: AsyncDb<unit> = 
        sql "delete from post where id > 2;
             delete from Comment where id > 3"



