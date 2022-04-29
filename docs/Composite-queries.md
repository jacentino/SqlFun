# Composite queries

Sometimes writing arbitrary SQL command is not enough. Especially, when the query accepts complex, user-provided search criteria.
Of course, the criteria changeability can be achieved with some smart combination of `NULL` checking and `CASE WHEN`:
```sql
select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy
where (@name is null or name like '%' + @name + '%')
  and (@title is null or title like '%' + @title + '%')
  and (@content is null or content like '%' + @content + '%')
order by case 
             when @order = 1 then createdAt
             when @order = 2 then author
             when @order = 3 then status
         end
```
The ugly truth is, that whenever a `@param is null` or `order by case` is used, **the query optimizer abandons use of indexes** (at least when we use MS SQL).  
## Templating

With SqlFun, it's easy to change the query with dynamic SQL generation, since the command is an ordinary string. It even provides simple templating module, based on ordinary string manipulation.
A template is a string with placholders, denoted as names in double braces {{PLACEHOLDER-NAME}}, e.g.:

```fsharp
let template = "select id, blogId, name, title, content, author, createdAt, modifiedAt, modifiedBy 
                from Post {% raw %}
                {{WHERE-CLAUSE}} 
                {{ORDER-BY-CALUSE}}" {% endraw %}
```
The `expandTemplate placeholder clause separator value template` function substitutes placeholder with some text, potentially more, than once, adding separators between substitutions and a clause before a first item, e.g:

```fsharp
let templateWithWhereExpanded = 
    template 
    |> expandTemplate "WHERE-CLAUSE" "where " " and " "author = @author"
    |> expandTemplate "WHERE-CLAUSE" "where " " and " "createdAt >= @date"
```

The `cleanupTemplate template` function removes remaining placeholders from template, making it valid sql command:

```fsharp
let command = cleanupTemplate templateWithWhereExpanded 
```

It's a good habit to define some small templating DSL before implementing template transformations, e.g.:

```fsharp
let expandWhere = expandTemplate "WHERE-CLAUSE" "where " " and "
let expandOrderBy = expandTemplate "ORDER-BY-CLAUSE" "order by " ", "
```

It greatly improves conciseness and readibility:

```fsharp
let templateWithWhereExpanded = template |> expandWhere "author = @author" |> expandWhere "createdAt >= @date"
```

## Records of options
With templates, we can generate queries with different number of parameters, depending on data provided, e.g. we can add conditions to the where clause only for parameters that are not ``None``.

Simplest way of managing changeable parameter list is to define our search criteria as a record of options. It allows to take advantage of parameter matching by name (not positionally), e.g.:

```fsharp 
type PostCriteria =
    {
        Title: string option
        Content: string option
        Author: string option
        CreatedAfter: DateTime option
        CreatedBefore: DateTime option
    }
```

The sort order descriptor can be defined like this:

```fsharp
type SortDirection = 
    | Asc
    | Desc

type SortColumn = 
    | Title
    | CreationDate
    | Author

type PostSortOrder = SortColumn * SortDirection
```

Now, we can generate a query from a template, adding parameters containing values:

```fsharp
let filterPosts (criteria: PostSearchCriteria list) template = 
    let template = if criteria.Title.IsSome 
                   then template |> expandWhere "title like '%' + @title + '%'" 
                   else template
    let template = if criteria.Content.IsSome 
                   then template |> expandWhere "content like '%' + @content + '%'" 
                   else template
    let template = if criteria.Author.IsSome 
                   then template |> expandWhere "author = @author" 
                   else template
    let template = if criteria.CreatedBefore.IsSome 
                   then template |> expandWhere "createdAt < @createdBefore" 
                   else template
    let template = if criteria.CreatedAfter.IsSome 
                   then template |> expandWhere "createdAt > @createdAfter" 
                   else template
    template

let getOrderSql (col, dir) = 
    let name = match col with
                | Title -> "title"
                | CreationDate -> "createdAt"
                | Author -> "author"
    match dir with
    | Asc -> name
    | Desc -> name + " desc"

let sortPostsBy orders template = 
    if not (List.isEmpty orders) then
        let cols = orders |> Seq.map getOrderSql |> String.concat ", "
        template |> expandOrderBy cols
    else 
        template

let findPosts (criteria: PostSearchCriteria list) (order: PostSortOrder) = 
    let template = "select p.id, p.blogId, p.name, p.title, p.content, 
                           p.author, p.createdAt, p.modifiedAt, p.modifiedBy, p.status
                    from post p
                    {{WHERE-CLAUSE}}
                    {{ORDER-BY-CLAUSE}}"
    let query = template |> filterPosts criteria |> sortPostsBy order |> cleanupTemplate
    buildAndMemoizeQuery sql query criteria
```

## Query Parts

There is also another mechanism of composition, based on recursive function type definition. The composition blocks are classes implementing following interface:
```fsharp 
type IQueryPart<'t> = 
    abstract member Combine: 't-> 'q
```
and, since the string type is the most obvious template type:
```fsharp
type IQueryPart = IQueryPart<string>
```
where the parameter is a query template and return value is a query function. The whole point is to define `Combine` method:
```fsharp 
type FilterByName(string name, next: IQueryPart) =

interface IQueryPart with
    override this.Combine (template: string) : 't = 
        let exp = template |> expandTemplate "WHERE-CLAUSE" "where " " and " "name like '%' + @name + '%'"
        let f = this.Combine<string -> 't> exp
        f name
```
by passing to next query part modified command template and modified function type, i.e. with additional parameter, then applying provided parameter value to returned function.  
The last query part in a chain should be an object of the `FinalQueryPart`, that generates needed function and **caches** it.  
The `expandTemplate placeholder clause separator value` function, used in the example above, allows to incrementally replace `{{placeholder}}` pattern in template with a value, adding a specified clause and separators when needed.

Although it's possible to implement each query part by implementing the interface, SqlFun offers some built-in, generic parts:
* `TransformWithValueQueryPart<'t>(expand, value)` allows to specify transformations that add single parameters,
* `TransformWithValueListQueryPart<'t>(expand, valueList)` allows to specify transformations that add multiple parameters basing on lists of values
* `TransformWithTextQueryPart` allows to specify transformations that expand query template without adding any parameters

where `expand` is a query template expansion function of type `string -> string`.
Additionally there are functions `transformWithValue`, `transformWithValueList` and `transformWithText`, that build subsequent query parts, wrapped in AsyncDb.

At first sight, this technique needs a lot of boilerplate. But it is not about ad-hoc querying, it's about building query DSL-s, that need more effort. 
If we apply it to five parameters, using list of discriminated unions, it starts to make much more sense:
```fsharp 
type PostCriteria =
    | TitleContains of string
    | ContentContains of string
    | AuthorIs of string
    | CreatedAfter of DateTime
    | CreatedBefore of DateTime

let rec filterPosts(criteria: PostCriteria list) (next: AsyncDb<IQueryPart>) =
    match criteria with
    | (TitleContains title) :: others ->
        let intermediate = filterPosts others next
        transformWithValue (expandWhere "title like '%' + @title + '%'") title intermediate
    | (ContentContains content) :: others ->
        let intermediate = filterPosts others next
        transformWithValue (expandWhere "content like '%' + @content + '%'") content intermediate
    | (AuthorIs author) :: others ->
        let intermediate = filterPosts others next
        transformWithValue (expandWhere "author = @author") author intermediate
    | (CreatedAfter date) :: others -> 
        let intermediate = filterPosts others next
        transformWithValue (expandWhere "createdAt >= @date") date intermediate
    | (CreatedBefore date) :: others -> 
        let intermediate = filterPosts others next
        transformWithValue (expandWhere "createdAt <= @date") date intermediate
    | [] ->
        next
```
Composing with another query parts is fairly easy. Let's define query part for sorting posts
```fsharp 
type SortDirection = 
    | Asc
    | Desc

type SortColumn = 
    | Title
    | CreationDate
    | Author

type PostSortOrder = SortColumn * SortDirection

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

```
Before we can use the `FinalQueryPart` class, we have to bind query generation config to it:
```fsharp 
let createConnection () = new SqlConnection(connectionString)
let generatorConfig = createDefaultConfig createConnection

let buildQuery ctx = async {
    FinalQueryPart(ctx, generatorConfig, cleanUpTemplate)
}
```
The last step is to specify SQL template:
```fsharp 
let selectPosts (next: QueryPart): Post list = 
    next |> withTemplate "select p.id, p.blogId, p.name, p.title, p.content, 
                                 p.author, p.createdAt, p.modifiedAt, p.modifiedBy, p.status
                          from post p
                          {{WHERE-CLAUSE}}
                          {{ORDER-BY-CLAUSE}}"
```
And then, we can compose parts:
```fsharp 
let findPosts (criteria: PostSearchCriteria list) (orders: PostSortOrder list) = 
    buildQuery
    |> filterPosts criteria
    |> sortPostsBy orders
    |> selectPosts
```
Or without sorting:
```fsharp 
let findPosts (criteria: PostSearchCriteria list) = 
    buildQuery
    |> filterPosts criteria
    |> selectPosts
```
Query parts and it's creation functions allow for many approaches to define query DSL. One can build a DSL as a set of small functions, that can be composed with `|>` operator, another one can define one big function, that consumes list of discriminated union values (like in example above), yet another can define simple record with optional values instead of discriminated union.

The downside of composite queries is, that they are not generated during module initialization, but during their first use, and as such, they don't participate in usual SqlFun type safety mechanisms. Thus, each of them should have it's own tests defined. I recommend to use [FsCheck](https://fscheck.github.io/FsCheck/) for comprehensive query DSL testing.
