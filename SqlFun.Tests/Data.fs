namespace SqlFun.Tests

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
    with    
        static member ParentId (c: Comment) = c.parentId
        static member PostId (c: Comment) = c.postId

    type Tag = {
        postId: int
        name: string
    }
    with 
        static member PostId (t: Tag) = t.postId

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
    with
        static member Id (p: Post) = p.id
        static member BlogId (p: Post) = p.blogId
        static member withTags (transform: 't seq -> Tag list) (p: Post) (tags: 't seq) = { p with tags = tags |> transform }
        static member withComments (transform: 't seq -> Comment list) (p: Post) (comments: 't seq) = { p with comments = transform comments }

    type PostWithLimitedSubItems = {
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
        [<Prefixed>]
        firstComment: Comment
        [<Prefixed>]
        firstTag: Tag option
    }

    type Signature = {
        author: string
        createdAt: DateTime
        modifiedAt: DateTime option
        modifiedBy: string option
        status: PostStatus
    }

    type DecomposedPost = {
        id: int
        blogId: int
        name: string
        title: string
        content: string
        signature: Signature
        comments: Comment list
        tags: Tag list
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

    type BlogWithInvalidType = {
        id: string
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

    type PostWithoutAnyIds = {
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

    type BlogWithPostsWithoutAnyIds = {
        name: string
        title: string
        description: string
        owner: string
        createdAt: DateTime
        modifiedAt: DateTime option
        modifiedBy: string option
        posts: PostWithoutAnyIds list
    }

    type PostWithArray = {
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
        commentArray: Comment array
        tagArray: Tag array
    }

    type PostWithSeq = {
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
        commentSeq: Comment seq
        tagSeq: Tag seq
    }

    type PostWithSet = {
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
        commentSet: Comment Set
        tagSet: Tag Set
    }


    type PostWithResultStream = {
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
        commentStream: Comment ResultStream
        tagStream: Tag ResultStream
    }

    type UserProfile = {
        id: string
        name: string
        email: string
        avatar: byte array
    }

    type PostChild<'t> = 
        {
            PostWithTagsWithoutKeysId: int
            Child: 't
        }
        interface SqlFun.Transforms.IChildObject<'t> with
            member this.Child = this.Child

    type TagWithoutKey = 
        {
            name: string
        }

    type PostWithTagsWithoutKeys = {
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
        tags: TagWithoutKey list
    }

open Data

module Tooling = 
    
    let cleanup: IDataContext -> unit = 
        sql "delete from post where id > 2;
             delete from tag where postId = 2
             delete from UserProfile where id <> 'jacenty'"

    let getNumberOfPosts: IDataContext -> int = 
        sql "select count(*) from post"

    let getNumberOfBlogs: IDataContext -> int = 
        sql "select count(*) from blog"


    let deleteAllButFirstBlog: IDataContext -> unit = 
        sqlTm 60 "delete from blog where id > 1"

    let getPostByName: string -> IDataContext -> Post = 
        sql "select * from post where name = @name"

    let getPost: int -> IDataContext -> Post option = 
        sql "select * from post where id = @id"

    let insertPost: Post -> IDataContext -> int = 
        sql "insert into post 
                    (blogId, name, title, content, author, createdAt, status)
             values (@blogId, @name, @title, @content, @author, @createdAt, @status);
             select scope_identity()"

    let deletePost: int -> IDataContext -> unit =
        sql "delete from post where id = @id"

    let getTags: int -> IDataContext -> Tag list = 
        sql "select postId, name from Tag where postId = @postId"

    let rec buildSubtree (parenting: Map<int option, Comment seq>) (c: Comment) = 
        { c with replies = match parenting |> Map.tryFind (Some c.id) with
                            | Some comments -> comments |> List.ofSeq |> List.map (buildSubtree parenting) 
                            | None -> []
        }

    let buildTree (comments: Comment seq) = 
        let (roots, children) = comments |> Seq.groupBy Comment.ParentId 
                                         |> List.ofSeq 
                                         |> List.partition (fst >> Option.isNone)
        let parenting = children |> Map.ofList
        roots |> List.map snd 
              |> List.collect List.ofSeq 
              |> List.map (buildSubtree parenting)

