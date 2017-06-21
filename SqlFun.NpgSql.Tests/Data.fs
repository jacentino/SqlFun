namespace SqlFun.NpgSql.Tests

open System
open SqlFun
open Common

module Data = 

    type Comment = {
        commentId: int
        postId: int
        parentId: int option
        content: string
        author: string
        createdAt: DateTime
        replies: Comment list
    }
    with
        static member getId (c: Comment) = c.commentId
        static member getParentId (c: Comment) = c.parentId
        static member getPostId (c: Comment) = c.postId

    type Tag = {
        postId: int
        name: string
    }
    with
        static member getPostId (t: Tag) = t.postId

    type PostStatus = 
        | [<EnumValue("N")>] New = 0
        | [<EnumValue("P")>] Published = 1
        | [<EnumValue("A")>] Archived = 2

    type Post = {
        postId: int
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
    with
        static member getId (p: Post) = p.postId
        static member getBlogId (p: Post) = p.blogId
        static member withTags (transform: 't list -> Tag list) (p: Post) (tags: 't list) = { p with tags = transform tags }
        static member withComments (transform: 't list -> Comment list) (p: Post) (comments: 't list) = { p with comments = transform comments }

    type Signature = {
        author: string
        createdAt: DateTime
        modifiedAt: DateTime option
        modifiedBy: string option
        status: PostStatus
    }

    type DecomposedPost = {
        postId: int
        blogId: int
        name: string
        title: string
        content: string
        signature: Signature
        comments: Comment list
        tags: Tag list
    }

    type Blog = {
        blogId: int
        name: string
        title: string
        description: string
        owner: string
        createdAt: DateTime
        modifiedAt: DateTime option
        modifiedBy: string option
        posts: Post list
    }
    with
        static member getId (b: Blog) = b.blogId

    type BlogWithInvalidType = {
        blogId: string
        name: string
        title: string
        description: string
        owner: string
        posts: Post list
    }

    type PostSearchCriteria = {
        blogId: int option
        title: string option
        content: string option
    }

    type SignatureSearchCriteria = {
        author: string option
        createdAtFrom: DateTime option
        createdAtTo: DateTime option
        modifiedAtFrom: DateTime option
        modifiedAtTo: DateTime option
        modifiedBy: string option
        status: PostStatus option
    }

open Data

module Tooling = 
    
    let cleanup: DataContext -> unit = 
        sql "delete from post where id > 2;
             delete from tag where postId = 2"

    let getNumberOfPosts: DataContext -> int = 
        sql "select count(*) from post"

    let getPostByName: string -> DataContext -> Post = 
        sql "select * from post where name = @name"

    let getPost: int -> DataContext -> Post = 
        sql "select * from post where id = @id"

    let insertPost: Post -> DataContext -> int = 
        sql "insert into post 
                    (blogId, name, title, content, author, createdAt, status)
             values (@blogId, @name, @title, @content, @author, @createdAt, @status);
             select scope_identity()"

    let deletePost: int -> DataContext -> unit =
        sql "delete from post where id = @id"

    let getTags: int -> DataContext -> Tag list = 
        sql "select postId, name from Tag where postId = @postId"

    let rec buildSubtree (parenting: Map<int option, Comment seq>) (c: Comment) = 
        { c with replies = match parenting |> Map.tryFind (Some c.commentId) with
                            | Some comments -> comments |> List.ofSeq |> List.map (buildSubtree parenting) 
                            | None -> []
        }

    let buildTree (comments: Comment list) = 
        let (roots, children) = comments |> Seq.groupBy Comment.getParentId 
                                         |> List.ofSeq 
                                         |> List.partition (fst >> Option.isNone)
        let parenting = children |> Map.ofList
        roots |> List.map snd 
              |> List.collect List.ofSeq 
              |> List.map (buildSubtree parenting)

