namespace SqlFun.MySql.Tests

open System
open SqlFun
open Common

module Data = 

    type Comment = {
        id: int
        postId: int
        parentId: int option
        content: string
        author: string
        createdAt: DateTime
        replies: Comment list
    }

    type Tag = {
        postId: int
        name: string
    }

    type PostStatus = 
        | [<EnumValue("N")>] New = 0
        | [<EnumValue("P")>] Published = 1
        | [<EnumValue("A")>] Archived = 2

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
        comments: Comment list
        tags: Tag list
    }

    type Signature = {
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

    
    type UserProfile = {
        id: string
        name: string
        email: string
        avatar: byte array
    }


open Data

module Tooling = 
    
    let getNumberOfBlogs: IDataContext -> int = 
        sql "select count(*) from blog"

    let deleteAllButFirstBlog: IDataContext -> unit = 
        sql "delete from blog where id > 1"
   
    let deleteAllUsers: IDataContext -> unit = 
        sql "delete from userprofile"
