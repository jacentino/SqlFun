namespace SqlFun.Oracle.Tests

open System
open Common

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

module Tooling = 
    open SqlFun

    let deleteAllButFirstBlog: IDataContext -> unit = 
        sql "delete from blog where blogid > 1"


