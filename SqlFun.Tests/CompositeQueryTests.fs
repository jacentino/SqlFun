namespace SqlFun.Tests

open NUnit.Framework
open SqlFun
open Data
open SqlFun.Exceptions
open SqlFun.Transforms
open Common
open System
open Composite

module CompositeQueries =
    
    type PostCriteria =
        | TitleContains of string
        | ContentContains of string
        | AuthorIs of string
        | HasTag of string
        | HasOneOfTags of string list
        | HasAllTags of string list
        | CreatedAfter of DateTime
        | CreatedBefore of DateTime

    type PostSortOrder = 
        | Title
        | CreationDate
        | Author
        | Desc of PostSortOrder
    
            
    let expandWhere = expandTemplate "WHERE-CLAUSE" "where " " and "
    let expandOrderBy = expandTemplate "ORDER-BY-CLAUSE" "order by " ", "
    let expandJoins = expandTemplate "JOIN-CLAUSES" "" " "
    let expandGroupBy = expandTemplate "GROUP-BY-CLAUSE" "group by " ", "
    let expandHaving = expandTemplate "HAVING-CLAUSE" "having " " and "
    

    let buildQuery ctx = 
        FinalQueryPart(ctx, createConnection, defaultParamBuilder)


    type FilterPostsPart(criteria: PostCriteria list, next: QueryPart) =
        inherit ListQueryPart<PostCriteria>(criteria, next) 

        member this.DeepCombine<'t> (template: string) (tags: string list) (remainingItems: PostCriteria list): 't =
            match tags with
            | t :: r ->
                let f = this.DeepCombine<string -> 't> template r remainingItems
                f t
            | [] -> this.CombineList template remainingItems                

        override this.CombineItem template item remainingItems: 't = 
            match item with
            | TitleContains title ->
                let exp = template |> expandWhere "title like '%' + @title + '%'"
                let f = this.CombineList<string -> 't> exp remainingItems
                f title
            | ContentContains content ->
                let exp = template |> expandWhere "content like '%' + @content + '%'"
                let f = this.CombineList<string -> 't> exp remainingItems
                f content
            | AuthorIs author ->
                let exp = template |> expandWhere "author = @author"
                let f = this.CombineList<string -> 't> exp remainingItems
                f author
            | HasTag tag ->
                let exp = template |> expandWhere "t.name = @tag"
                                   |> expandJoins "join tag t on t.postId = p.id" 
                                   |> expandGroupBy "p.id, p.blogId, p.name, p.title, p.content, p.author, p.createdAt, p.modifiedAt, p.modifiedBy, p.status"
                let f = this.CombineList<string -> 't> exp remainingItems
                f tag
            | HasOneOfTags tags ->
                let condition = tags |> Seq.mapi (fun i _ -> "t.name = @tag" + string(i)) |> String.concat " or "
                let exp = template |> expandWhere ("(" + condition + ")")
                                   |> expandJoins "join tag t on t.postId = p.id" 
                                   |> expandGroupBy "p.id, p.blogId, p.name, p.title, p.content, p.author, p.createdAt, p.modifiedAt, p.modifiedBy, p.status"
                this.DeepCombine exp tags remainingItems
            | HasAllTags tags ->
                let condition = tags |> Seq.mapi (fun i _ -> "t.name = @tag" + string(i)) |> String.concat " or "
                let exp = template |> expandWhere ("(" + condition + ")")
                                   |> expandJoins "join tag t on t.postId = p.id" 
                                   |> expandGroupBy "p.id, p.blogId, p.name, p.title, p.content, p.author, p.createdAt, p.modifiedAt, p.modifiedBy, p.status"
                                   |> expandHaving ("count(t.name) = " + string(tags |> List.length))
                this.DeepCombine exp tags remainingItems
            | CreatedAfter date -> 
                let exp = template |> expandWhere "createdAt >= @date"
                let f = this.CombineList<DateTime -> 't> exp remainingItems
                f date
            | CreatedBefore date -> 
                let exp = template |> expandWhere "createdAt <= @date"
                let f = this.CombineList<DateTime -> 't> exp remainingItems
                f date

    let filterPostsBy =     
        FilterPostsPart |> curry
        
    let rec getOrderSql order = 
        match order with
        | Title -> "title"
        | CreationDate -> "createdAt"
        | Author -> "author"
        | Desc ord -> (getOrderSql ord) + " DESC"

    let sortPostsBy orders (next: QueryPart) = 
        { new QueryPart with 
            override this.Combine template: 't = 
                let cols = orders |> Seq.map getOrderSql |> String.concat ", "
                let exp = template |> expandOrderBy cols
                next.Combine exp
        }

    let selectPosts (next: QueryPart): Post list = 
        next.Combine "select p.id, p.blogId, p.name, p.title, p.content, p.author, p.createdAt, p.modifiedAt, p.modifiedBy, p.status
                      from post p {{JOIN-CLAUSES}}
                      {{WHERE-CLAUSE}}
                      {{GROUP-BY-CLAUSE}}
                      {{ORDER-BY-CLAUSE}}
                      {{HAVING-CLAUSE}}"

open CompositeQueries

[<TestFixture>]
type CompositeQueryTests() = 

    [<Test>]
    member this.``Composite queries return valid results``() =
        let l = fun ctx ->
                    ctx
                    |> buildQuery
                    |> filterPostsBy [TitleContains "framework"]
                    |> sortPostsBy [Author; CreationDate]
                    |> selectPosts
                |> run
        Assert.AreEqual(2, l |> List.length)

    [<Test>]
    member this.``Composite queries with multiple clauses return valid results``() = 
        let l = fun ctx ->
                    ctx
                    |> buildQuery
                    |> filterPostsBy [TitleContains "framework"; HasTag "options"]
                    |> selectPosts
                |> run
        Assert.AreEqual(1, l |> List.length)        


    [<Test>]
    member this.``Composite queries with some really complex clauses return valid results``() = 
        let l = fun ctx ->
                    ctx
                    |> buildQuery
                    |> filterPostsBy [HasOneOfTags ["options"; "framework"]]
                    |> selectPosts
                |> run
        Assert.AreEqual(1, l |> List.length)        

    [<Test>]
    member this.``Composite queries with some even more complex clauses return valid results``() = 
        let l = fun ctx ->
                    ctx
                    |> buildQuery
                    |> filterPostsBy [HasAllTags ["options"; "framework"]]
                    |> selectPosts
                |> run
        Assert.AreEqual(1, l |> List.length)        

