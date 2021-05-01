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
    /// <param name="expand">
    /// Function transforming template.
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

        /// <summary>
        /// Adds parameters from list as hierarchical tuple to a composite query.
        /// </summary>
        /// <param name="template">
        /// The query template.
        /// </param>        
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
    /// <param name="next">
    /// The next query part in a composition.
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
    let buildAndRunQuery (ctx: IDataContext) (cmd: string) (generator: string -> IDataContext -> 'q): 'q =
        let f = CommandCache<IDataContext -> 'q>.GetOrAdd cmd generator
        f ctx

    /// <summary>
    /// The part responsible for generating and launching a query.
    /// </summary>
    /// <param name="ctx">
    /// The data context.
    /// </param>
    /// <param name="config">
    /// The code generation config.
    /// </param>   
    /// <param name="stringify">
    /// Function converting query template to string. 
    /// </param>
    /// <typeparam name="'t">
    /// The type of a template.
    /// </typeparam>
    type FinalQueryPart<'t>(ctx: IDataContext, config: GeneratorConfig, stringify: 't -> string) = 
        interface IQueryPart<'t> with
            override this.Combine (template: 't) : 'q =
                let generator = sql config
                buildAndRunQuery ctx (stringify template) generator


