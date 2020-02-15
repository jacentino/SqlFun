namespace SqlFun

open System

module Testing = 

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

