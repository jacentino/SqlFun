# Clean result structures

Sometimes return types of query functions are not 100% compliant with business logic needs. To define result transformations, we often need fields for foreign keys. These fields are often usesles in business logic code. Of course, we can make a trade-off, and keep result structures a little bit polluted with data access needs, but there is a cleaner way. Instead of defining foreign key directly:
```fsharp 
    type Comment = {
        id: int
        postId: int
        parentId: int
        content: string
        author: string
        createdAt: DateTime
        modifiedAt: DateTime option
        modifiedBy: string option
    }
```
we define two structures:
```fsharp 
    type PostRelated<'t> = {
        postId: int
        related: 't
    }

    type Comment = {
        id: int
        parentId: int
        content: string
        author: string
        createdAt: DateTime
        modifiedAt: DateTime option
        modifiedBy: string option
    }
```
and define transformations slightly different:
```fsharp 
    let buildCommentTree (cl: Comment PostRelated list) = 
        cl |> Seq.map (fun c -> c.related) |> buildTree

    let getPostsWithComments: int -> Post list AsyncDb = 
        sql "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy, status from post where blogId = @id;
             select c.id, c.postId, c.parentId, c.content, c.author, c.createdAt from comment c join post p on c.postId = p.id where p.blogId = @id"
    >> AsyncDb.map (join (fun p -> p.id) (fun c -> c.postId) (fun p cl -> { p with comments = buildCommentTree cl }))
```
