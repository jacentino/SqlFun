namespace SqlFun

open System.Data
open System.Collections.Concurrent
open Queries

module Composite = 

    /// <summary>
    /// The interface for query specification components.
    /// </summary>
    type QueryPart = 

        /// <summary>
        /// Combines current query part with a remaining query parts.
        /// </summary>
        abstract member Combine: string -> 't

    /// <summary>
    /// The specialization of query part processing collections of specification items.
    /// </summary>
    [<AbstractClass>]    
    type ListQueryPart<'c> (items: 'c list, next: QueryPart) = 

        /// <summary>
        /// Combines one element.
        /// </summary>
        abstract member CombineItem: string -> 'c -> 'c list -> 't

        /// <summary>
        /// Combines list of elements.
        /// </summary>
        /// <param name="template">
        /// The template, that can be expanded by the current part.
        /// </param>
        /// <param name="items">
        /// Items of the collection.
        /// </param>
        member this.CombineList<'t> (template: string) (items: 'c list) : 't = 
            match items with
            | one :: remaining -> this.CombineItem template one remaining
            | [] -> next.Combine<'t> template 

        interface QueryPart with
            override this.Combine (template: string) : 't =
                this.CombineList template (List.rev items)
            

    /// <summary>
    /// The cache of generated sql caller functions.
    /// </summary>
    type CommandCache<'t>() = 
        static member private cache = ConcurrentDictionary<string, 't>()

        /// <summary>
        /// Gets a caller function from a cache or generates one, if none exists.
        /// </summary>
        /// <param name="command">
        /// The sql command.
        /// </param>
        /// <param name="generator">
        /// The function, that generates a caller of an sql command.
        /// </param>
        static member GetOrAdd (command: string) (generator: string -> 't) = CommandCache<'t>.cache.GetOrAdd(command, generator)

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
    let buildAndRunQuery (ctx: DataContext) (cmd: string) (generator: string -> DataContext -> 't): 't =
        let cmdExp = cleanUpTemplate cmd
        let f = CommandCache<DataContext -> 't>.GetOrAdd cmdExp generator
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
    /// <param name="paramBuilder">
    /// Function creating sql parameters.
    /// </param>
    type FinalQueryPart<'c when 'c :> IDbConnection>(ctx: DataContext, createConnection: unit -> 'c, paramBuilder: ParamBuilder -> ParamBuilder) = 
        interface QueryPart with
            override this.Combine (template: string) : 't =
                let generator = sql createConnection paramBuilder
                buildAndRunQuery ctx template generator
