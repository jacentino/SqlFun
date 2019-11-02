namespace SqlFun.Sqlite.Tests

module Data = 
    open Common
    open System

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

    module Tooling = 
        open SqlFun

        let deleteAllButFirstBlog: DataContext -> unit = 
            sql "delete from blog where id <> 1"

