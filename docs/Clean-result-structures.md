# Clean result structures

Sometimes return types of query functions are not 100% compliant with business logic needs. To define result transformations, we often need fields for foreign keys. These fields are often usesles in business logic code. Of course, we can make a trade-off, and keep result structures a little bit polluted with data access needs, but there is a cleaner way. Instead of defining foreign key directly:
```fsharp 
    type Comment = {
        id: int
        postId: int
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
        content: string
        author: string
        createdAt: DateTime
        modifiedAt: DateTime option
        modifiedBy: string option
    }
```
and define transformations slightly different:
```fsharp     
    let getPostsWithComments: int -> Post list AsyncDb = 
        sql "select id, blogId, name, title, content, author, 
                    createdAt, modifiedAt, modifiedBy, status 
             from post 
             where blogId = @id;

             select c.id, c.postId, c.content, c.author, c.createdAt 
             from comment c join post p on c.postId = p.id 
             where p.blogId = @id"
    >> AsyncDb.map (join (fun p -> p.id) 
                         (fun c -> c.postId) 
                         (fun p cl -> { p with comments = cl |> Seq.map (fun c -> c.related) }))
```
This approach can be used with convention-based transformations too, but you have to mark a wrapper type (i.e. PostRelated<'t>) with the interface `IChildObject<'t>`:
```fsharp
    type PostRelated<'t> = {
        postId: int
        related: 't
    }
    interface IChildObject<'t> with
        member this.Child = this.related
```
and use it when defining transformation:
```fsharp     
    let getPostsWithComments: int -> Post list AsyncDb = 
        sql "select id, blogId, name, title, content, author, 
                    createdAt, modifiedAt, modifiedBy, status 
             from post 
             where blogId = @id;

             select c.id, c.postId, c.content, c.author, c.createdAt 
             from comment c join post p on c.postId = p.id 
             where p.blogId = @id"
    >> AsyncDb.map (join<_, PostRelated<Comment>>)
```
