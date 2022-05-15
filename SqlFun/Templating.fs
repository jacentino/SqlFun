namespace SqlFun

open System

module Templating = 

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
    /// Conditionally applies a function.
    /// </summary>
    /// <param name="condition">
    /// Condition determining whether to apply a function  or not.
    /// </param>
    /// <param name="f">
    /// The function to be applied
    /// </param>
    let applyWhen condition f = if condition then f else id


    module Advanced = 
    
        /// <summary>
        /// Represents hole definition in a template.
        /// </summary>
        type Hole = 
            {
                /// The String.Format-like pattern specifying hole expansion. 
                pattern:    string
                /// Separator used in list expansion.
                separator:  string
            }
        and Template = 
            {
                /// The pattern containing holes specified as {{HOLE-NAME}}.
                pattern:    string
                /// Hole names and definitions.
                holes:      (string * Hole) list
                /// Values assigned to holes. Each hole can have list of values.
                values:     Map<string, Template list>
            }

        /// <summary>
        /// Helper function creating template.
        /// </summary>
        /// <param name="holes">
        /// List of holes of the template.
        /// </param>
        /// <param name="pattern">
        /// The pattern to be expanded. Holes occurs in double braces, i.e. {{HOLE-NAME}}
        /// </param>
        let template holes pattern = { pattern = pattern; holes = holes; values = Map.empty }

        /// <summary>
        /// helper function creating hole definition.
        /// </summary>
        /// <param name="name">
        /// The hole name.
        /// </param>
        /// <param name="pattern">
        /// The String.Format-style pattern used to expand the template.
        /// </param>
        /// <param name="sep">
        /// The string separating expanded values.
        /// </param>
        let hole name pattern sep = name, { pattern = pattern; separator = sep }

        /// <summary>
        /// Helper function creating static template - pattern without holes.
        /// </summary>
        /// <param name="value">
        /// The value of the template.
        /// </param>
        let raw value = { pattern = value; holes = []; values = Map.empty }
    
        /// <summary>
        /// Creates template for expanding lists with given separator.
        /// </summary>
        /// <param name="pattern">
        /// The String.Format-style pattern used to expand the template.
        /// </param>
        /// <param name="separator">
        /// The string separating expanded values.
        /// </param>
        /// <param name="items">
        /// The list of templates used to be expanded.
        /// </param>
        let list pattern separator items = {
            pattern = "{{ITEMS}}"
            holes = [ "ITEMS", { pattern = pattern; separator = separator } ]
            values = ["ITEMS", items ] |> Map.ofList 
        }

        /// <summary>
        /// Adds subtemplate to the specified hole of the template.
        /// </summary>
        /// <param name="name">
        /// The hole name.
        /// </param>
        /// <param name="subtmpl">
        /// The template to be used to expand the hole.
        /// </param>
        /// <param name="template">
        /// The template to be expanded.
        /// </param>
        let withSubtmpl name subtmpl template = 
            { template with 
                values = template.values |> Map.add name (subtmpl :: (template.values |> Map.tryFind name |> Option.defaultValue [])) 
            }

        /// <summary>
        /// Adds string value to the specified hole of the template.
        /// </summary>
        /// <param name="name">
        /// The hole name.
        /// </param>
        /// <param name="value">
        /// The value to be used to expand the hole.
        /// </param>
        /// <param name="template">
        /// The template to be expanded.
        /// </param>
        let withValue name value template = 
            withSubtmpl name (raw value) template

        /// <summary>
        /// Expands the tempate to string.
        /// </summary>
        /// <param name="template">
        /// The template to be expanded.
        /// </param>
        let rec stringify (template: Template) =    
            let expandedHoles = 
                [ for name, hole in template.holes do 
                    if template.values |> Map.containsKey name then
                        let values = template.values.[name] |> List.map stringify |> String.concat hole.separator
                        yield name, String.Format(hole.pattern, values)
                    else
                        yield name, ""
                ]
            expandedHoles
            |> List.fold (fun (pattern: string) (name, value) -> pattern.Replace("{{" + name + "}}", value)) template.pattern 

        /// <summary>
        /// Determines, whether the subtemplate exists in a specified hole.
        /// </summary>
        /// <param name="name">
        /// Hole name.
        /// </param>
        /// <param name="subtmpl">
        /// The subtemplate to be searched for.
        /// </param>
        /// <param name="template">
        /// The target template.
        /// </param>
        let subtmplExists name subtmpl template = 
            template.values
            |> Map.tryFind name
            |> Option.defaultValue []
            |> List.contains subtmpl
        
        /// <summary>
        /// Determines, whether the value exists in a specified hole.
        /// </summary>
        /// <param name="name">
        /// Hole name.
        /// </param>
        /// <param name="value">
        /// The value to be searched for.
        /// </param>
        /// <param name="template">
        /// The target template.
        /// </param>
        let valueExists name value template = 
            template.values 
            |> Map.tryFind name 
            |> Option.defaultValue []
            |> List.exists (stringify >> (=) value)
