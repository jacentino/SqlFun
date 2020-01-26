namespace SqlFun.Tests

open NUnit.Framework
open SqlFun
open Data
open SqlFun.Exceptions
open SqlFun.Transforms
open SqlFun.Transforms.Standard
open Common
open System
open System.IO

type TestQueries() =    

    static member getBlog: int -> DataContext -> Blog = 
        sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy from Blog where id = @id"

    static member getAllBlogs: DataContext -> Blog list = 
        sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy from Blog"

    static member getAllBlogsWithUnit: unit -> DataContext -> Blog list = 
        sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy from Blog"

    static member getAllBlogsWithExcessiveArg: int -> DataContext -> Blog list = 
        sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy from Blog"


    static member getBlogWithExcessiveArgs: (int * int * int) -> DataContext -> Blog = 
        sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy from Blog where id = @id"

    static member getBlogOptional: int -> DataContext -> Blog option = 
        sql "select * from Blog where id = @id"

    static member getBlogIncomplete: int -> DataContext -> Blog = 
        sql "select id, name, title, description from Blog where id = @id"

    static member incorrect: int -> DataContext -> Blog = 
        sql "some totally incorrect sql with @id parameter"

    static member getBlogInvalidType: int -> DataContext -> BlogWithInvalidType = 
        sql "select id, name, title, description, owner from Blog where id = @id"

    static member getNumberOfPosts: int -> DataContext -> int = 
        sql "select count(*) from post where blogId = @id"

    static member getPostIds: int -> DataContext -> int list = 
        sql "select id from Post where blogId = @id"

    static member getBlogOwner: int -> DataContext -> string = 
        sql "select owner from blog where id = @id"

    static member getBlogOwnerOptional: int -> DataContext -> string option = 
        sql "select owner from blog where id = @id"

    static member getPostAndItsComments: int -> DataContext -> (Post * Comment list) = 
        sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post where id = @id;
             select id, postId, parentId, content, author, createdAt from comment where postId = @id"

    static member getPostAndItsCommentsAsProduct: int -> DataContext -> (Post * Comment) list = 
        sql "select p.id, p.blogId, p.name, p.title, p.content, p.author, p.createdAt, p.modifiedAt, p.modifiedBy, p.status,
                    c.id, c.postId, c.parentId, c.content, c.author, c.createdAt from post p
             left join comment c on c.postId = p.id
             where p.id = @id"

    static member getDecomposedPost: int -> DataContext -> DecomposedPost = 
        sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post where id = @id"

    static member getPostsWithTags: int -> DataContext -> Post list = 
        sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post where blogId = @id;
             select t.postId, t.name from tag t join post p on t.postId = p.id where p.blogId = @id"
        >> join Post.Id Tag.PostId (Post.withTags List.ofSeq)
        >> List.ofSeq
        |> curry 

    static member getSomePostsByIds: int list -> DataContext -> Post list = 
        sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post where id in (@postId)"

    static member getSomePostsByTags: string list -> DataContext -> Post list = 
        sql "select id, blogId, p.name, title, content, author, createdAt, modifiedAt, modifiedBy, status from 
             post p join tag t on t.postId = p.id
             where t.name in (@tagName)
             group by id, blogId, p.name, title, content, author, createdAt, modifiedAt, modifiedBy, status"

    static member getPostsWithTags2: int -> DataContext -> Post list = 
        sql "select p.id, p.blogId, p.name, p.title, p.content, p.author, p.createdAt, p.modifiedAt, p.modifiedBy, p.status,
                   t.postId as item_postId, t.name as item_name
            from post p left join tag t on t.postId = p.id
            where p.id = @id" 
        >> group (Post.withTags (aliasedAsItem >> List.ofSeq))
        >> List.ofSeq
        |> curry

    static member getPostsWithTagsAndComments: int -> DataContext -> Post list = 
        sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post where blogId = @id;
             select t.postId, t.name from tag t join post p on t.postId = p.id where p.blogId = @id;
             select c.id, c.postId, c.parentId, c.content, c.author, c.createdAt from comment c join post p on c.postId = p.id where p.blogId = @id"
        >> combineTransforms 
            (join Post.Id Tag.PostId (Post.withTags List.ofSeq)) 
            (join Post.Id Comment.PostId (Post.withComments Tooling.buildTree))
        >> List.ofSeq
        |> curry 

    static member findPostsByCriteria: PostSearchCriteria -> DataContext -> Post list = 
        sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post
             where (blogId = @blogId or @blogId is null)
               and (title like '%' + @title + '%' or @title is null)
               and (content like '%' + @content + '%' or @content is null)"

    static member findPosts: (int option * string option * string option * string option) -> DataContext -> Post list = 
        sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post
             where (blogId = @blogId or @blogId is null)
               and (title like '%' + @title + '%' or @title is null)
               and (content like '%' + @content + '%' or @content is null)
               and (author = @author or @author is null)"

    static member findPostsByMoreCriteria: (PostSearchCriteria * SignatureSearchCriteria) -> DataContext -> Post list = 
        sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post
             where (blogId = @blogId or @blogId is null)
               and (title like '%' + @title + '%' or @title is null)
               and (content like '%' + @content + '%' or @content is null)
               and (author = @author or @author is null)
               and (createdAt >= @createdAtFrom or @createdAtFrom is null)
               and (createdAt <= @createdAtTo or @createdAtTo is null)
               and (modifiedAt >= @modifiedAtFrom or @modifiedAtFrom is null)
               and (modifiedAt <= @modifiedAtTo or @modifiedAtTo is null)
               and (status = @status or @status is null)"
      
    static member getBlogAsync: int -> DataContext -> Blog Async = 
        sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy from Blog where id = @id"

    static member getPostsWithTagsAsync: int -> DataContext -> Post list Async = 
        sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post where blogId = @id;
             select t.postId, t.name from tag t join post p on t.postId = p.id where p.blogId = @id"
        >> AsyncDb.map (join Post.Id Tag.PostId (Post.withTags List.ofSeq) >> List.ofSeq)

    static member getPostsWithTagsAndCommentsAsync: int -> DataContext -> Post list Async = 
        sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post where blogId = @id;
             select t.postId, t.name from tag t join post p on t.postId = p.id where p.blogId = @id;
             select c.id, c.postId, c.parentId, c.content, c.author, c.createdAt from comment c join post p on c.postId = p.id where p.blogId = @id"
        >> AsyncDb.map (combineTransforms 
                            (join Post.Id Tag.PostId (Post.withTags List.ofSeq)) 
                            (join Post.Id Comment.PostId (Post.withComments Tooling.buildTree))
                        >> List.ofSeq)

    static member getPostWithOneCommentAsync: int -> DataContext -> PostWithLimitedSubItems Async = 
        sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status, 
                    0 as firstCommentid, 0 as firstCommentpostId, 0 as firstCommentparentId, '' as firstCommentcontent, '' as firstCommentauthor, cast('2000/01/01' as datetime) as firstCommentcreatedAt
             from post where id = @id;
             select top 1 c.id, c.postId, c.parentId, c.content, c.author, c.createdAt from comment c where c.postId = @id"
        >> AsyncDb.map Conventions.combine<_, Comment>

    static member getPostWithOneTagAsync: int -> DataContext -> PostWithLimitedSubItems Async = 
        sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status, 
                    0 as firstCommentid, 0 as firstCommentpostId, 0 as firstCommentparentId, '' as firstCommentcontent, '' as firstCommentauthor, cast('2000/01/01' as datetime) as firstCommentcreatedAt
             from post where id = @id;
             select t.postId, t.name from tag t where t.postId = @id"
        >> AsyncDb.map Conventions.combine<_, Tag>

    static member getPostsWithTagsRel: int -> DataContext -> Post list = 
        sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post where blogId = @id;
             select t.postId, t.name from tag t join post p on t.postId = p.id where p.blogId = @id"
        >> DbAction.map (Conventions.join<_, Tag> >> List.ofSeq)

    static member getPostsWithTagsAndCommentsAsyncTOps: int -> DataContext -> Post list Async = 
        sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post where blogId = @id;
             select t.postId, t.name from tag t join post p on t.postId = p.id where p.blogId = @id;
             select c.id, c.postId, c.parentId, c.content, c.author, c.createdAt from comment c join post p on c.postId = p.id where p.blogId = @id"
        >> AsyncDb.map (Conventions.join<_, Tag> >-> (mapSnd Tooling.buildTree >> Conventions.join<_, Comment> >> List.ofSeq))

    static member getBlogWithPostsWithTagsAndCommentsAsyncTOps: int -> DataContext -> Blog Async = 
        sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy from Blog where id = @id
             select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post where blogId = @id;
             select t.postId, t.name from tag t join post p on t.postId = p.id where p.blogId = @id;
             select c.id, c.postId, c.parentId, c.content, c.author, c.createdAt from comment c join post p on c.postId = p.id where p.blogId = @id"
        >> AsyncDb.map (Conventions.combine<_, Post> >>- (Conventions.join<_, Tag> >-> (mapSnd Tooling.buildTree >> Conventions.join<_, Comment>)))

    static member getBlogsWithWrongTransform: DataContext -> Blog list Async = 
        sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy from Blog;
             select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post"
        |> AsyncDb.map (Conventions.combine<_, Post>)

    static member getBlogWithoutPostsWithoutAnyIds: int -> DataContext -> BlogWithPostsWithoutAnyIds Async = 
        sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy from Blog where id = @id;
             select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post where blogId = @id"
        >> AsyncDb.map (Conventions.combine<_, PostWithoutAnyIds>)


    static member insertPost: Post -> DataContext -> int Async = 
        sql "insert into post 
                    (blogId, name, title, content, author, createdAt, status)
             values (@blogId, @name, @title, @content, @author, @createdAt, @status);
             select scope_identity()"

    static member insertTag: Tag -> DataContext -> unit Async = 
        sql "insert into tag (postId, name) values (@postId, @name)"

    static member getSpid: DataContext -> int Async = 
        sql "select @@spid"

    static member statementWithDeclare: int -> DataContext -> Post list Async = 
        sql "declare @postId int; set @postId = @p;
             select * from post where id = @postId"

    static member getBlogsByCreatedBeforeDate: DateTimeOffset -> DataContext -> Blog list Async = 
        sql "select id, name, title, description, owner, createdAt, modifiedAt, modifiedBy from Blog where createdAt < @createdAt"
        
    static member getPostArrayByIds: int array -> DataContext -> PostWithArray array = 
        sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post where id in (@postId)"

    static member getPostSeqByIds: int seq -> DataContext -> PostWithSeq seq = 
        sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post where id in (@postId)"

    static member getPostSetByIds: int Set -> DataContext -> PostWithSet Set = 
        sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post where id in (@postId)"

    static member getPostStreamByIds: int list -> DataContext -> PostWithResultStream ResultStream = 
        sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post where id in (@postId)"


    static member getSomePostsByTagsWithStream: string list -> DataContext -> Post ResultStream = 
        sql "select id, blogId, p.name, title, content, author, createdAt, modifiedAt, modifiedBy, status from 
             post p join tag t on t.postId = p.id
             where t.name in (@tagName)
             group by id, blogId, p.name, title, content, author, createdAt, modifiedAt, modifiedBy, status"

    static member getSomePostsByTagsAsyncWithStream: string list -> DataContext -> Post ResultStream Async = 
        sql "select id, blogId, p.name, title, content, author, createdAt, modifiedAt, modifiedBy, status from 
             post p join tag t on t.postId = p.id
             where t.name in (@tagName)
             group by id, blogId, p.name, title, content, author, createdAt, modifiedAt, modifiedBy, status"


    static member getPostAndItsCommentsResultStream: int -> DataContext -> (Post * Comment ResultStream) = 
        sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post where id = @id;
             select id, postId, parentId, content, author, createdAt from comment where postId = @id"

    static member insertUser: UserProfile -> DataContext -> unit = 
        sql "insert into UserProfile (id, name, email, avatar) 
             values (@id, @name, @email, @avatar)"

    static member getUsers: DataContext -> UserProfile list = 
        sql "select id, name, email, avatar from UserProfile"

[<TestFixture>]
type SqlQueryTests() = 

    [<SetUp>]
    member this.Setup() =
        Tooling.cleanup |> run

    [<Test>]
    member this.``Query returning one row returns proper result when the requested row exists``() = 
        let blog = TestQueries.getBlog 1 |> run
        Assert.AreEqual (1, blog.id)
        
    [<Test>]
    member this.``Queries can be defined without parameters``() = 
        let blogs = TestQueries.getAllBlogs |> run
        Assert.AreEqual (1, blogs.Length)
        
    [<Test>]
    member this.``Unit can be used as a parameter``() = 
        let blogs = TestQueries.getAllBlogsWithUnit () |> run
        Assert.AreEqual (1, blogs.Length)

    [<Test>]
    member this.``Query returning one row fails when the requested row does not exist``() = 
        Assert.Throws (fun () -> TestQueries.getBlog 10 |> run |> ignore) |> ignore

    [<Test>]
    member this.``Query returning one row optionally returns None when the requested row doesn't exist``() = 
        let result = TestQueries.getBlogOptional 10 |> run
        Assert.IsNull result

    [<Test>]
    member this.``Query returning to narrow result (there are no columns for some fields) fails during compilation``() = 
        Assert.Throws<CompileTimeException>(fun () -> TestQueries.getBlogIncomplete 1 |> run |> ignore) |> ignore

    [<Test>]
    member this.``Query containing incorrect sql fails during compilation``() = 
        Assert.Throws<CompileTimeException>(fun () -> TestQueries.incorrect 1 |> run |> ignore) |> ignore

    [<Test>]
    member this.``Query mapped to object with incompatible attributes fails in compile time``() = 
        Assert.Throws<CompileTimeException>(fun () -> TestQueries.getBlogInvalidType 1 |> run |> ignore) |> ignore

    [<Test>]
    member this.``Query returning scalar returns proper result``() = 
        let result = TestQueries.getNumberOfPosts 1 |> run
        Assert.AreEqual (2, result)

    [<Test>]
    member this.``Query returning list of scalars returns proper result``() = 
        let result = TestQueries.getPostIds 1 |> run
        Assert.AreEqual (2, result.Length)

    [<Test>]
    member this.``Query returning scalar fails when the requested value doesn't exist``() =
        Assert.Throws(fun () -> TestQueries.getBlogOwner 10 |> run |> ignore) |> ignore

    [<Test>]
    member this.``Query returning scalar optionally returns None if the requested value doesn't exist``() =
        let result = TestQueries.getBlogOwnerOptional 10 |> run 
        Assert.IsNull result

    [<Test>]
    member this.``Two queries are mapped to two-dimensional tuple``() = 
        let (p, c) = TestQueries.getPostAndItsComments 1 |> run
        Assert.AreEqual(1, p.id)
        Assert.AreEqual(3, c |> List.length)

    [<Test>]
    member this.``Query returning one result can be mapped to list of tuples of records``() =
        let l = TestQueries.getPostAndItsCommentsAsProduct 1 |> run
        let (p, c) = l |> List.head
        Assert.AreEqual(1, p.id)
        Assert.AreEqual(1, c.postId)
        Assert.AreEqual(3, l |> List.length)

    [<Test>]
    member this.``Result type can contain substructures``() =
        let p = TestQueries.getDecomposedPost 1 |> run
        Assert.AreEqual("jacenty", p.signature.author)

    [<Test>]
    member this.``Lists of ints are valid parameters (listDirectParamBuilder)``() =
        let p = TestQueries.getSomePostsByIds [1; 2; 3] |> run
        Assert.IsNotEmpty(p)

    [<Test>]
    member this.``Lists of strings are valid parameters (listParamBuilder)``() =
        let p = TestQueries.getSomePostsByTags ["framework"; "options"] |> run
        Assert.IsNotEmpty(p)

    [<Test>]
    member this.``Two queries can be combined by key value with join``() = 
        let pl = TestQueries.getPostsWithTags 1 |> run
        Assert.AreEqual(2, pl |> List.length)
        let p = pl |> List.head
        Assert.AreEqual(1, p.blogId)
        Assert.AreEqual(3, p.tags |> List.length)

    [<Test>]
    member this.``Two queries can be combined by key value with join using conventions``() = 
        let pl = TestQueries.getPostsWithTagsRel 1 |> run
        Assert.AreEqual(2, pl |> List.length)
        let p = pl |> List.head
        Assert.AreEqual(1, p.blogId)
        Assert.AreEqual(3, p.tags |> List.length)

    [<Test>]
    member this.``Child relations can be references``() = 
        let pl = TestQueries.getPostWithOneCommentAsync 1 |> runAsync |> Async.RunSynchronously
        Assert.AreEqual(1, pl.firstComment.postId)

    [<Test>]
    member this.``Child relations can be options``() = 
        let pl = TestQueries.getPostWithOneTagAsync 1 |> runAsync |> Async.RunSynchronously
        Assert.AreEqual(1, pl.firstTag.Value.postId)

    [<Test>]
    member this.``Query result can be mapped to list of tuples, then grouped by first tuple and combined``() = 
        let pl = TestQueries.getPostsWithTags2 1 |> run
        Assert.AreEqual(1, pl |> List.length)
        let p = pl |> List.head
        Assert.AreEqual(1, p.blogId)
        Assert.AreEqual(3, p.tags |> List.length)

    [<Test>]
    member this.``Three queries can be combined by key value with two combined joins``() = 
        let pl = TestQueries.getPostsWithTagsAndComments 1 |> run
        Assert.AreEqual(2, pl |> List.length)
        let p = pl |> List.head
        Assert.AreEqual(1, p.blogId)
        Assert.AreEqual(3, p.tags |> List.length)
        Assert.AreEqual(1, p.comments |> List.length)

    [<Test>]
    member this.``Query parameters can be specified as a record``() = 
        let p = TestQueries.findPostsByCriteria {
                    blogId = Some 1
                    title = Some "another"
                    content = None
                } |> run |> List.head     
        Assert.AreEqual("Yet another sql framework", p.title)

    [<Test>]
    member this.``Query parameters can be specified as a tuple``() = 
        let p = TestQueries.findPosts (Some 1, Some "another", None, None) |> run |> List.head     
        Assert.AreEqual("Yet another sql framework", p.title)

    [<Test>]
    member this.``Query parameters can be specified as a tuple of records``() = 
        let p = TestQueries.findPostsByMoreCriteria 
                    ({
                        blogId = Some 1
                        title = Some "another"
                        content = None
                    }, {
                        author = None
                        createdAtFrom = Some <| DateTime(2017, 04, 20)
                        createdAtTo = None
                        modifiedAtFrom = None
                        modifiedAtTo = None
                        modifiedBy = None
                        status = Some PostStatus.Published
                    }) 
                    |> run |> List.head     
        Assert.AreEqual("Yet another sql framework", p.title)

    [<Test>]
    member this.``Asynchronous query returning one row returns proper result when the requested row exists``() = 
        let blog = TestQueries.getBlogAsync 1 |> runAsync |> Async.RunSynchronously
        Assert.AreEqual (1, blog.id)


    [<Test>]
    member this.``Two asynchronous queries can be combined by key value with join wrapped in mapAsync``() = 
        let pl = TestQueries.getPostsWithTagsAsync 1 |> runAsync |> Async.RunSynchronously
        Assert.AreEqual(2, pl |> List.length)
        let p = pl |> List.head
        Assert.AreEqual(1, p.blogId)
        Assert.AreEqual(3, p.tags |> List.length)

    [<Test>]
    member this.``Three asynchronous queries can be combined by key value with two combined joins``() = 
        let pl = TestQueries.getPostsWithTagsAndCommentsAsync 1 |> runAsync |> Async.RunSynchronously
        Assert.AreEqual(2, pl |> List.length)
        let p = pl |> List.head
        Assert.AreEqual(1, p.blogId)
        Assert.AreEqual(3, p.tags |> List.length)
        Assert.AreEqual(1, p.comments |> List.length)

    [<Test>]
    member this.``Three asynchronous queries can be combined by key value with two joins combined with transform operators``() = 
        let pl = TestQueries.getPostsWithTagsAndCommentsAsyncTOps 1 |> runAsync |> Async.RunSynchronously
        Assert.AreEqual(2, pl |> List.length)
        let p = pl |> List.head
        Assert.AreEqual(1, p.blogId)
        Assert.AreEqual(3, p.tags |> List.length)
        Assert.AreEqual(1, p.comments |> List.length)

    [<Test>]
    member this.``Both types of transform operators work correctly``() = 
        let b = TestQueries.getBlogWithPostsWithTagsAndCommentsAsyncTOps 1 |> runAsync |> Async.RunSynchronously
        Assert.AreEqual(2, b.posts |> List.length)
        let p = b.posts |> List.head
        Assert.AreEqual(1, p.blogId)
        Assert.AreEqual(3, p.tags |> List.length)
        Assert.AreEqual(1, p.comments |> List.length)

    [<Test>]
    member this.``Combine on collection raises meaningful error message``() = 
        try
            TestQueries.getBlogsWithWrongTransform |> runAsync |> Async.RunSynchronously |> ignore
            Assert.Fail()
        with
        | e -> Assert.True(e.InnerException.Message.Contains("is not an F# record type"))

    [<Test>]
    member this.``Combine without parent key is allowed``() = 
            TestQueries.getBlogWithoutPostsWithoutAnyIds 1 |> runAsync |> Async.RunSynchronously |> ignore

    [<Test>]
    member this.``Asynchronous queries can be executed on the same connection with asyncdb computation expression``() =
        let id = (asyncdb {
                    let post = { 
                        id = 0 // fake value
                        blogId = 1
                        name = "my-expectations"
                        title = "My expectations" 
                        content = "What I expect from an sql framework?"
                        author = "jacenty"
                        createdAt = DateTime.Now
                        modifiedAt = None
                        modifiedBy = None
                        status = PostStatus.New
                        comments = []
                        tags = []
                    }
                    let! postId = TestQueries.insertPost post

                    let sqlTag = { postId = postId; name = "sql" }
                    do! TestQueries.insertTag sqlTag

                    let fSharpTag = { postId = postId; name = "F#" }
                    do! TestQueries.insertTag fSharpTag

                    let ormTag = { postId = postId; name = "orm" }
                    do! TestQueries.insertTag ormTag

                    return postId
                }) |> runAsync |> Async.RunSynchronously
        let pl = TestQueries.getPostsWithTags 1 |> run 
        let p = pl |> List.find (fun p -> p.id = id)
        Assert.AreEqual (3, p.tags |> List.length)

    [<Test>]
    member this.``Transaction is committed automatically``() =
        let id = asyncdb {
                        let post = { 
                            id = 0 // fake value
                            blogId = 1
                            name = "my-expectations"
                            title = "My expectations" 
                            content = "What I expect from an sql framework?"
                            author = "jacenty"
                            createdAt = DateTime.Now
                            modifiedAt = None
                            modifiedBy = None
                            status = PostStatus.New
                            comments = []
                            tags = []
                        }
                        return! TestQueries.insertPost post
                    } 
                    |> AsyncDb.inTransaction
                    |> runAsync
                    |> Async.RunSynchronously
        let p = Tooling.getPost id |> run
        Assert.IsTrue (Option.isSome p)

    [<Test>]
    member this.``Exception within transaction causes rollback``() =
        let mutable id = 0
        asyncdb {
            let post = { 
                id = 0 // fake value
                blogId = 1
                name = "my-expectations"
                title = "My expectations" 
                content = "What I expect from an sql framework?"
                author = "jacenty"
                createdAt = DateTime.Now
                modifiedAt = None
                modifiedBy = None
                status = PostStatus.New
                comments = []
                tags = []
            }
            let! postId = TestQueries.insertPost post
            id <- postId
            raise <| Exception "Just for rollback."
        } 
        |> AsyncDb.inTransaction
        |> runAsync
        |> Async.Catch
        |> Async.RunSynchronously
        |> ignore
        let p = Tooling.getPost id |> run
        Assert.IsTrue (Option.isNone p)
    
    [<Test>]        
    member this.``Statements with variables started with double monkey does not fail``() =
        let id = TestQueries.getSpid |> runAsync |> Async.RunSynchronously
        Assert.IsTrue(id > 0)
        
    
    [<Test>]        
    member this.``Statements with variable declarations does not fail.``() =
        let l = TestQueries.statementWithDeclare 1 |> runAsync |> Async.RunSynchronously
        Assert.IsNotEmpty l

    [<Test>]
    member this.``For statements are allowed in asyncdb``() =
        let tags = [
            { Tag.postId= 2; name = "Testing" }
            { Tag.postId= 2; name = "Even more testing" }
        ]
        asyncdb {
            for t in tags do
                do! TestQueries.insertTag t
        }
        |> AsyncDb.inTransaction
        |> runAsync
        |> Async.RunSynchronously

        let pl = TestQueries.getPostsWithTags 1 |> run 
        let p = pl |> List.find (fun p -> p.id = 2)
        Assert.AreEqual (2, p.tags |> List.length)

    [<Test>]
    member this.``DateTimeOffset is handled correctly``() = 
        let d = DateTimeOffset.Now
        TestQueries.getBlogsByCreatedBeforeDate d |> runAsync |> Async.RunSynchronously |> ignore

    [<Test>]
    member this.``Results and fields can be arrays``() =
        let p = TestQueries.getPostArrayByIds [| 1; 2; 3 |] |> run
        Assert.IsNotEmpty(p)

    [<Test>]
    member this.``Results and fields can be sequences``() =
        let p = TestQueries.getPostSeqByIds (seq { yield 1; yield 2; yield 3 }) |> run
        Assert.IsNotEmpty(p)

    [<Test>]
    member this.``Results and fields can be sets``() =
        let p = TestQueries.getPostSetByIds (Set [1; 2; 3]) |> run
        Assert.IsNotEmpty(p)
        

    [<Test>]
    member this.``Fields can not be result streams``() =
        let exn = Assert.Throws<CompileTimeException>(fun () -> TestQueries.getPostStreamByIds [1; 2; 3] |> run |> ignore) 
        StringAssert.Contains("Unsupported collection type", exn.InnerException.Message)


    [<Test>]
    member this.``Query results can be streamed``() =
        let posts = 
            dbaction {
                use! p = TestQueries.getSomePostsByTagsWithStream ["framework"; "options"] 
                return p |> List.ofSeq       
            } |> run
        Assert.IsNotEmpty(posts) 
            

    [<Test>]
    member this.``Query results can be streamed in async methods``() =
        asyncdb {
            use! p = TestQueries.getSomePostsByTagsAsyncWithStream ["framework"; "options"]
            Assert.IsNotEmpty(p)
        } |> runAsync |> Async.RunSynchronously


    [<Test>]
    member this.``MultiResult queries can not use ResultStream``() = 
        let exn = Assert.Throws<CompileTimeException>(fun () -> TestQueries.getPostAndItsCommentsResultStream 1 |> run |> ignore)
        StringAssert.Contains("Unsupported collection type", exn.InnerException.Message)


    [<Test>]
    member this.``Queries with wrong number of curried arguments raise meaningful exceptions``() =
        let exn = Assert.Throws<CompileTimeException>(fun () -> TestQueries.getAllBlogsWithExcessiveArg 1 |> run |> ignore) 
        StringAssert.Contains("Function arguments don't match query parameters", exn.InnerException.Message)


    [<Test>]
    member this.``Queries with wrong number of tuple arguments raise meaningful exceptions``() =
        let exn = Assert.Throws<CompileTimeException>(fun () -> TestQueries.getBlogWithExcessiveArgs (1, 1, 1) |> run |> ignore) 
        StringAssert.Contains("Function arguments don't match query parameters", exn.InnerException.Message)

    [<Test>]
    member this.``Byte array parameters are handled properly``() = 
        let assemblyFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
        let user = {
            id = "jacentino"
            name = "Jacentino Placentino"
            email = "jacentino.placentino@pp.com"
            avatar = File.ReadAllBytes(Path.Combine(assemblyFolder, "jacenty.jpg"))
        }
        Assert.DoesNotThrow(fun () -> TestQueries.insertUser user |> run)

    [<Test>]
    member this.``Records with byte array fields are loaded correctly``() =
        let assemblyFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
        let expected = [
            {
                id = "jacenty"
                name = "Jacek Placek"
                email = "jacek.placek@pp.com"
                avatar = File.ReadAllBytes(Path.Combine(assemblyFolder, "jacenty.jpg"))
            }
        ]
        let users = TestQueries.getUsers |> run
        Assert.AreEqual(expected, users)