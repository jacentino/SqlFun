namespace SqlFun

open System

module Testing = 

    /// <summary>
    /// Wraps TypeInitializationException in another exception, 
    /// exposing source file name and line number of invalid query.
    /// </summary>
    /// <param name="f">
    /// Function accessing the query.
    /// </param>
    let testQueries (f: unit -> 't) = 
        try
            f() |> ignore
        with 
        | :? TypeInitializationException as ex when ex.InnerException <> null && ex.InnerException.InnerException <> null ->
             let line = ex.InnerException.StackTrace.Split('\n') |> Seq.last
             let sourceRef = 
                match line.IndexOf(" in ") with
                | -1 -> line
                | idx -> line.Remove(0, idx + 4)
             failwithf "Invalid query in: %s\n%s" sourceRef ex.InnerException.InnerException.Message

