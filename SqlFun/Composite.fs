namespace SqlFun

open System.Data
open System.Collections.Concurrent
open Queries

module Composite = 

    /// <summary>
    /// The interface for query specification components.
    /// </summary>
    /// <typeparam name="'t">
    /// The type of a template.
    /// </typeparam>
    type IQueryPart<'t> = 

        /// <summary>
        /// Combines current query part with a remaining query parts.
        /// </summary>
        abstract member Combine: 't -> 'q          

    type IQueryPart = IQueryPart<string>

    /// <summary>
    /// Adds parameters from list as hierarchical tuple to a composite query.
    /// </summary>
    /// <param name="template">
    /// The query template.
    /// </param>
    /// <param name="items">
    /// Query parameters.
    /// </param>
    /// <param name="next">
    /// The next query part in a composition.
    /// </param>
    /// <typeparam name="'t">
    /// The type of a template.
    /// </typeparam>
    /// <typeparam name="'e">
    /// The type of an element.
    /// </typeparam>
    type TransformWithListQueryPart<'t, 'e>(expand: 't -> 't, items: 'e list, next: IQueryPart<'t>) = 

        member private this.buildHTuple<'q, 'e, 'v> (template: 't) (value: 'v) (items: 'e list) (next: IQueryPart<'t>): 'q =
            match items with
            | e :: r ->
                this.buildHTuple template (e, value) r next
            | [] -> 
                let f = next.Combine template
                f value

        member this.Combine (template: 't) : 'q =
            match items with
            | e :: remaining -> this.buildHTuple<'q, 'e, 'e> (expand template) e remaining next
            | [] -> next.Combine (expand template)

        interface IQueryPart<'t> with
            member this.Combine (template: 't) : 'q = this.Combine template
            


    let transformWithList expand items next = asyncdb {
        let! nextP = next
        return TransformWithListQueryPart(expand, items, nextP) :> IQueryPart<'t>
    }

    /// <summary>
    /// Expands query template with a specified function without adding any parameters 
    /// and changing query function type.
    /// </summary>
    /// <typeparam name="'t">
    /// The type of a template.
    /// </typeparam>
    type TransformWithTextQueryPart<'t>(expand: 't -> 't, next: IQueryPart<'t>) = 
        interface IQueryPart<'t> with
            member this.Combine template = 
                next.Combine <| expand template

    let transformWithText expand next = asyncdb {
        let! nextP = next
        return TransformWithTextQueryPart(expand, nextP) :> IQueryPart<'t>
    }

    /// <summary>
    /// Expands template and applies a specified value.
    /// </summary>
    /// <typeparam name="'t">
    /// The type of a template.
    /// </typeparam>
    type TransformWithValueQueryPart<'t, 'v>(expand: 't -> 't, value: 'v, next: IQueryPart<'t>) = 
        interface IQueryPart<'t> with
            member this.Combine<'q> template = 
                let exp = expand template
                let f = next.Combine<'v -> 'q> exp
                f value

    let transformWithValue expand value next = asyncdb {
        let! nextP = next
        return TransformWithValueQueryPart(expand, value, nextP) :> IQueryPart<'t>
    }

    /// <summary>
    /// Starts query composition chain by providing sql command template.
    /// </summary>
    /// <param name="template">
    /// The sql command template.
    /// </param>
    /// <param name="part">
    /// The next query part.
    /// </param>
    /// <typeparam name="'t">
    /// The type of a template.
    /// </typeparam>
    let withTemplate (template: 't) (next: AsyncDb<IQueryPart<'t>>) = asyncdb {
        let! part = next 
        return part.Combine<'q> template    
    }

    /// <summary>
    /// The cache of generated sql caller functions.
    /// </summary>
    type CommandCache<'q>() = 
        static member private cache = ConcurrentDictionary<string, 'q>()

        /// <summary>
        /// Gets a caller function from a cache or generates one, if none exists.
        /// </summary>
        /// <param name="command">
        /// The sql command.
        /// </param>
        /// <param name="generator">
        /// The function, that generates a caller of an sql command.
        /// </param>
        static member GetOrAdd (command: string) (generator: string -> 'q) = CommandCache<'q>.cache.GetOrAdd(command, generator)

    /// <summary>
    /// Expands some template placeholder with a value.
    /// </summary>
    /// <remarks>
    /// If the expansion occurs for the first time, the clause is added before a value.
    /// Otherwise a value is followed by a separator.
    /// </remarks>
    /// <param name="placeholder">
    /// The placeholder to be replaced with a value.
    /// </param>
    /// <param name="clause">
    /// The clause (e.g. WHERE, ORDER BY, HAVING) to be added when the value is placed for the first time.
    /// </param>
    /// <param name="separator">
    /// The separator injected between subsequent occurrances of a value.
    /// </param>
    /// <param name="template">
    /// The template to be expanded.
    /// </param>
    /// <param name="value">
    /// The value to replace a placeholder.
    /// </param>
    let expandTemplate (placeholder: string) (clause: string) (separator: string) (value: string) (template: string) : string =
        if template.Contains("{{" + placeholder + "}}")
        then template.Replace("{{" + placeholder + "}}", clause + "{{" + placeholder + "!}}" + value)
        else template.Replace("{{" + placeholder + "!}}", "{{" + placeholder + "!}}" + value + separator)

    /// <summary>
    /// Removes all remaining placeholders from an expanded template, making it valid sql command.
    /// </summary>
    /// <param name="template">
    /// The template to be cleaned-up.
    /// </param>
    let cleanUpTemplate (template: string) = 
        template.Split([| "{{"; "}}" |], System.StringSplitOptions.None) 
        |> Seq.mapi (fun i s -> if i % 2 = 0 then s else "")
        |> String.concat ""

    /// <summary> 
    /// Generates query function, caches it, and invokes
    /// </summary>
    /// <param name="ctx">
    /// The data context.
    /// </param>
    /// <param name="cmd">
    /// Expanded sql template with some placeholders.
    /// </param>
    /// <param name="generator">
    /// The function, that generates a caller of an sql command.
    /// </param>
    let buildAndRunQuery (ctx: DataContext) (cmd: string) (generator: string -> DataContext -> 'q): 'q =
        let f = CommandCache<DataContext -> 'q>.GetOrAdd cmd generator
        f ctx

    /// <summary>
    /// The part responsible for generating and launching a query.
    /// </summary>
    /// <param name="ctx>
    /// The data context.
    /// </param>
    /// <param name="createConnection">
    /// The function creating a database connection.
    /// </param>
    /// <param name="commandTimeout">
    /// The command timeout.
    /// </param>
    /// <param name="paramBuilder">
    /// Function generating expression creating sql parameters.
    /// </param>
    /// <param name="rowBuilder">
    /// Function generating expression creating row of results. 
    /// </param>
    /// <param name="stringify">
    /// Function converting query template to string. 
    /// </param>
    /// <typeparam name="'t">
    /// The type of a template.
    /// </typeparam>
    type FinalQueryPart<'t>(ctx: DataContext, config: GeneratorConfig, stringify: 't -> string) = 
        interface IQueryPart<'t> with
            override this.Combine (template: 't) : 'q =
                let generator = sql config
                buildAndRunQuery ctx (stringify template) generator


