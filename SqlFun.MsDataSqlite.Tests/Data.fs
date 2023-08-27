namespace SqlFun.MsDataSqlite.Tests

open SqlFun

module Data = 
    open Common
    open System

    type PostStatus = 
        | [<EnumValue("N")>] New = 0
        | [<EnumValue("P")>] Published = 1
        | [<EnumValue("A")>] Archived = 2


    type Blog = {
        id: int
        name: string
        title: string
        description: string
        owner: string
        createdAt: DateTime
        modifiedAt: DateTime option
        modifiedBy: string option
    }

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



    module Tooling = 
        open SqlFun

        let deleteAllButFirstBlog: IDataContext -> unit = 
            sql "delete from blog where id <> 1"

        let deleteAllPosts: IDataContext -> unit =
            sql "delete from post"

