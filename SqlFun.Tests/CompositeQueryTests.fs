namespace SqlFun.Tests

open NUnit.Framework
open SqlFun
open Data
open SqlFun.Exceptions
open SqlFun.Transforms
open Common
open System
open Composite

open FsCheck

module CompositeQueries =
    
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
        
    
            
    let expandWhere = expandTemplate "WHERE-CLAUSE" "where " " and "
    let expandOrderBy = expandTemplate "ORDER-BY-CLAUSE" "order by " ", "
    let expandJoins = expandTemplate "JOIN-CLAUSES" "" " "
    let expandGroupBy = expandTemplate "GROUP-BY-CLAUSE" "group by " ", "
    let expandHaving = expandTemplate "HAVING-CLAUSE" "having " " and "
    

    let buildQuery ctx = async {
        return FinalQueryPart(ctx, createConnection, None, defaultParamBuilder) :> IQueryPart
    }

    let rec filterPosts (criteria: PostCriteria) (next: AsyncDbAction<IQueryPart>) = 
            match criteria with
            | { TitleContains = Some title }  ->
                let intermediate = filterPosts { criteria with TitleContains = None } next
                transformWithValue (expandWhere "title like '%' + @title + '%'") title intermediate
            | { ContentContains = Some content }  ->
                let intermediate = filterPosts { criteria with ContentContains = None } next
                transformWithValue (expandWhere "content like '%' + @content + '%'") content intermediate
            | { AuthorIs = Some author } ->
                let intermediate = filterPosts { criteria with AuthorIs = None } next
                transformWithValue (expandWhere "author = @author") author intermediate
            | { HasTag = Some tag } ->
                let intermediate = filterPosts { criteria with HasTag = None } next
                transformWithValue (expandWhere "t1.name = @tag1"
                                    >> expandJoins "join tag t1 on t1.postId = p.id" 
                                    >> expandGroupBy "p.id, p.blogId, p.name, p.title, p.content, p.author, p.createdAt, p.modifiedAt, p.modifiedBy, p.status")
                                    tag
                                    intermediate
            | { HasOneOfTags = tags } when not (tags |> List.isEmpty) ->
                let condition = tags |> Seq.mapi (fun i _ -> "t1n.name = @tag1N" + string(i)) |> String.concat " or "
                let intermediate = filterPosts { criteria with HasOneOfTags = [] } next
                transformWithList (expandWhere ("(" + condition + ")")
                              >> expandJoins "join tag t1n on t1n.postId = p.id" 
                              >> expandGroupBy "p.id, p.blogId, p.name, p.title, p.content, p.author, p.createdAt, p.modifiedAt, p.modifiedBy, p.status")
                             tags
                             intermediate
            | { HasAllTags = tags } when not (tags |> List.isEmpty)  ->
                let condition = tags |> Seq.mapi (fun i _ -> "tnn.name = @tagNN" + string(i)) |> String.concat " or "
                let intermediate = filterPosts { criteria with HasAllTags = [] } next
                transformWithList (expandWhere ("(" + condition + ")")
                              >> expandJoins "join tag tnn on tnn.postId = p.id" 
                              >> expandGroupBy "p.id, p.blogId, p.name, p.title, p.content, p.author, p.createdAt, p.modifiedAt, p.modifiedBy, p.status"
                              >> expandHaving ("count(tnn.name) = " + string(tags |> List.length)))
                             tags
                             intermediate
            | { CreatedAfter = Some date } -> 
                let intermediate = filterPosts { criteria with CreatedAfter = None } next
                transformWithValue (expandWhere "createdAt >= @afterDate") date intermediate
            | { CreatedBefore = Some date } -> 
                let intermediate = filterPosts { criteria with CreatedBefore = None } next
                transformWithValue (expandWhere "createdAt <= @beforeDate") date intermediate
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
        if not (List.isEmpty orders)
        then
            let cols = orders |> Seq.map getOrderSql |> String.concat ", "
            transformWithText (expandOrderBy cols) next
        else 
            next

    let selectPosts next: AsyncDbAction<Post list> =
        next |> withTemplate "select p.id, p.blogId, p.name, p.title, p.content, p.author, p.createdAt, p.modifiedAt, p.modifiedBy, p.status
                              from post p {{JOIN-CLAUSES}}
                              {{WHERE-CLAUSE}}
                              {{GROUP-BY-CLAUSE}}
                              {{HAVING-CLAUSE}}
                              {{ORDER-BY-CLAUSE}}"

open CompositeQueries

type Arbs = 
    static member strings() =
        Arb.filter ((<>) null) <| Arb.Default.String()

[<TestFixture>]
type CompositeQueryTests() = 

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

    (*[<Test>]
    member this.``Composie queries can be tested with FsCheck``() = 
        
        let property criteria ordering = 
            buildQuery
            |> filterPosts criteria
            |> sortPostsBy (ordering |> List.distinctBy fst) // FsCheck generates duplicates
            |> selectPosts
            |> runAsync
            |> Async.RunSynchronously
            |> ignore

        let cfg = { Config.QuickThrowOnFailure with Arbitrary = [ typeof<Arbs> ] }
        Check.One(cfg, property)*)