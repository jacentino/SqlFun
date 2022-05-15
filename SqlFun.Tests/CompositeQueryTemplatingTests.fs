namespace SqlFun.Tests

open NUnit.Framework
open SqlFun
open Data
open Common
open System
open Composite
open Templating.Advanced

open FsCheck

module CompositeQueryTemplating =
    
    type PostCriteria = {
        TitleContains: string option
        ContentContains: string option
        AuthorIs: string option
        HasTag: string option
        HasOneOfTags: string list
        HasAllTags: string list
        CreatedAfter: DateTime option
        CreatedBefore: DateTime option
    } with
        static member Empty = {
            TitleContains = None
            ContentContains = None
            AuthorIs = None
            HasTag = None
            HasOneOfTags = []
            HasAllTags = []
            CreatedAfter = None
            CreatedBefore = None
        }
    
    type SortDirection = 
        | Asc
        | Desc

    type SortColumn = 
        | Title
        | CreationDate
        | Author

    type PostSortOrder = SortColumn * SortDirection
        
    let holes = [
        "WHERE-CLAUSE",     { pattern = "where {0}";    separator = " and " }
        "ORDER-BY-CLAUSE",  { pattern = "order by {0}"; separator = ", "    }
        "JOIN-CLAUSES",     { pattern = "{0}";          separator = " "     }
        "GROUP-BY-CLAUSE",  { pattern = "group by {0}"; separator = ", "    }
        "HAVING-CLAUSE",    { pattern = "having {0}";   separator = " and " }
    ]

    let withWhere       = withValue     "WHERE-CLAUSE"
    let withWhereT      = withSubtmpl   "WHERE-CLAUSE"
    let withOrderByT    = withSubtmpl   "ORDER-BY"
    let withJoin        = withValue     "JOIN-CLAUSES"
    let withGroupBy     = withValue     "GROUP-BY-CLAUSE"
    let withHaving      = withValue     "HAVING-CLAUSE"
            
    let orExpr items = items |> list "({0})" " or "

    let fieldList items = items |> list "{0}" ", "


    let buildQuery ctx = async {
        return FinalQueryPart(ctx, generatorConfig, stringify) :> IQueryPart<_>
    }

    let rec filterPosts (criteria: PostCriteria) (next: AsyncDb<IQueryPart<_>>) = 
            match criteria with
            | { TitleContains = Some title }  ->
                let intermediate = filterPosts { criteria with TitleContains = None } next
                transformWithValue (withWhere "title like '%' + @title + '%'") title intermediate
            | { ContentContains = Some content }  ->
                let intermediate = filterPosts { criteria with ContentContains = None } next
                transformWithValue (withWhere "content like '%' + @content + '%'") content intermediate
            | { AuthorIs = Some author } ->
                let intermediate = filterPosts { criteria with AuthorIs = None } next
                transformWithValue (withWhere "author = @author") author intermediate
            | { HasTag = Some tag } ->
                let intermediate = filterPosts { criteria with HasTag = None } next
                transformWithValue (withWhere "t1.name = @tag1"
                                    >> withJoin "join tag t1 on t1.postId = p.id"
                                    >> withGroupBy "p.id, p.blogId, p.name, p.title, p.content, p.author, p.createdAt, p.modifiedAt, p.modifiedBy, p.status")
                                   tag
                                   intermediate
            | { HasOneOfTags = tags } when not tags.IsEmpty ->
                let intermediate = filterPosts { criteria with HasOneOfTags = [] } next
                transformWithList (withWhereT (List.init tags.Length (sprintf "t1n.name = @tag1N%d" >> raw) |> orExpr)
                                    >> withJoin "join tag t1n on t1n.postId = p.id" 
                                    >> withGroupBy "p.id, p.blogId, p.name, p.title, p.content, p.author, p.createdAt, p.modifiedAt, p.modifiedBy, p.status")
                                  tags
                                  intermediate
            | { HasAllTags = tags } when not tags.IsEmpty  ->
                let intermediate = filterPosts { criteria with HasAllTags = [] } next
                transformWithList (withWhereT (List.init tags.Length (sprintf "tnn.name = @tagNN%d" >> raw) |> orExpr)
                                    >> withJoin "join tag tnn on tnn.postId = p.id"
                                    >> withGroupBy "p.id, p.blogId, p.name, p.title, p.content, p.author, p.createdAt, p.modifiedAt, p.modifiedBy, p.status"
                                    >> withHaving ("count(tnn.name) = " + string(tags |> List.length)))
                                  tags
                                  intermediate
            | { CreatedAfter = Some date } -> 
                let intermediate = filterPosts { criteria with CreatedAfter = None } next
                transformWithValue (withWhere "createdAt >= @afterDate") date intermediate
            | { CreatedBefore = Some date } -> 
                let intermediate = filterPosts { criteria with CreatedBefore = None } next
                transformWithValue (withWhere "createdAt <= @beforeDate") date intermediate
            | _ -> next
        
    let getOrderSql (col, dir) = 
        let name = match col with
                    | Title -> "title"
                    | CreationDate -> "createdAt"
                    | Author -> "author"
        match dir with
        | Asc -> name
        | Desc -> name + " desc"

    let sortPostsBy orders next = 
        if not (List.isEmpty orders) then
            transformWithText (orders |> List.map (getOrderSql >> raw) |> fieldList |> withOrderByT) next
        else 
            next

    let selectPosts next: AsyncDb<Post list> =
        next |> withTemplate {
            pattern = 
                "select p.id, p.blogId, p.name, p.title, p.content, p.author, p.createdAt, p.modifiedAt, p.modifiedBy, p.status
                    from post p {{JOIN-CLAUSES}}
                    {{WHERE-CLAUSE}}
                    {{GROUP-BY-CLAUSE}}
                    {{HAVING-CLAUSE}}
                    {{ORDER-BY-CLAUSE}}"
            holes = holes
            values = Map.empty
        }

    type Arbs = 
        static member strings() =
            Arb.filter ((<>) null) <| Arb.Default.String()


open CompositeQueryTemplating

[<TestFixture>]
type CompositeQueryTemplatingTests() = 

    [<Test>]
    member this.``Composite queries return valid results``() =
        let l = buildQuery
                |> filterPosts { PostCriteria.Empty with TitleContains = Some "framework" }
                |> sortPostsBy [Author, Asc; CreationDate, Asc]
                |> selectPosts
                |> runAsync
                |> Async.RunSynchronously
        Assert.AreEqual(2, l |> List.length)

    [<Test>]
    member this.``Composite queries with multiple clauses return valid results``() = 
        let l = buildQuery
                |> filterPosts { PostCriteria.Empty with TitleContains = Some "framework"; HasTag = Some "options" }
                |> selectPosts
                |> runAsync
                |> Async.RunSynchronously
        Assert.AreEqual(1, l |> List.length)        


    [<Test>]
    member this.``Composite queries with some really complex clauses return valid results``() = 
        let l = buildQuery
                |> filterPosts { PostCriteria.Empty with HasOneOfTags = ["options"; "framework"] }
                |> selectPosts
                |> runAsync
                |> Async.RunSynchronously
        Assert.AreEqual(1, l |> List.length)        

    [<Test>]
    member this.``Composite queries with some even more complex clauses return valid results``() = 
        let l = buildQuery
                |> filterPosts { PostCriteria.Empty with HasAllTags = ["options"; "framework"] }
                |> selectPosts
                |> runAsync
                |> Async.RunSynchronously
        Assert.AreEqual(1, l |> List.length)        

    [<Test>]
    member this.``Composite queries can be tested with FsCheck``() = 
        
        let property criteria ordering = 
            buildQuery
            |> filterPosts criteria
            |> sortPostsBy (ordering |> List.distinctBy fst) // FsCheck generates duplicates
            |> selectPosts
            |> runAsync
            |> Async.RunSynchronously
            |> ignore

        let cfg = { Config.QuickThrowOnFailure with Arbitrary = [ typeof<Arbs> ] }
        Check.One(cfg, property)

