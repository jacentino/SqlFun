namespace SqlFun

open System
open System.Runtime.ExceptionServices
open Microsoft.FSharp.Reflection
open System.Diagnostics

module Diagnostics = 


    /// <summary>
    /// Creates a function of a given signature that raises an exception.
    /// </summary>
    type ThrowHelper(ex: exn) = 

        let reraise (): 'r = 
            ExceptionDispatchInfo.Capture(ex).Throw()
            Unchecked.defaultof<'r>

        let rec getArgs (t: Type) = 
            if FSharpType.IsFunction t then
                let arg, others = FSharpType.GetFunctionElements t
                Array.append [| arg |] (getArgs others)
            else
                [| t |]

        member this.Caller<'t1, 'r>(): 't1 -> 'r = fun _ -> reraise ()
        member this.Caller<'t1, 't2, 'r>(): 't1 -> 't2 -> 'r = fun _ _ -> reraise ()
        member this.Caller<'t1, 't2, 't3, 'r>(): 't1 -> 't2 -> 't3 -> 'r = fun _ _ _ -> reraise ()
        member this.Caller<'t1, 't2, 't3, 't4, 'r>(): 't1 -> 't2 -> 't3 -> 't4 -> 'r = fun _ _ _ _ -> reraise ()
        member this.Caller<'t1, 't2, 't3, 't4, 't5, 'r>(): 't1 -> 't2 -> 't3 -> 't4 -> 't5 -> 'r = fun _ _ _ _ _ -> reraise ()

        member this.Placeholder<'q>(): 'q =
            let genericArgs = getArgs typeof<'q>
            let genericCaller = this.GetType().GetMethods() |> Array.find (fun m -> m.Name = "Caller" && m.GetGenericArguments().Length = genericArgs.Length)
            let caller = genericCaller.MakeGenericMethod genericArgs
            caller.Invoke(this, null) :?> 'q
        
    /// <summary>
    /// Intercepts execution of query generation function to allow full compilation error report.
    /// In case of code generation exception catches it and adds to a log with additional stack trace info,
    /// then returns placeholder function throwing exception instead of generated function.
    /// </summary>
    /// <param name="log">
    /// The variable storing code generation exceptions.
    /// </param>
    /// <param name="generator">
    /// Function generating query function.
    /// </param>
    /// <param name="command">
    /// The sql command.
    /// </param>
    /// <returns>
    /// The query function or placeholder that throws exception.
    /// </returns>
    let logged (log: (exn * string) list Ref) (generator: string -> 'q) (command: string) = 
        try
            generator command
        with 
        | ex ->
            let stackTrace = StackTrace(true)
            let allFrames = Seq.init stackTrace.FrameCount stackTrace.GetFrame
            let frame = 
                allFrames 
                |> Seq.tryFind (fun f -> f.GetMethod().Name = ".cctor" && f.GetMethod().DeclaringType.FullName.StartsWith "<StartupCode$")
                |> Option.defaultValue (stackTrace.GetFrame 2)
            log := (ex, sprintf "%s, line: %d" (frame.GetFileName()) (frame.GetFileLineNumber())) :: !log
            ThrowHelper(ex).Placeholder<'q>()

    /// <summary>
    /// Builds human-readable message from code generation errors and stack traces
    /// and throws an exception.
    /// </summary>
    /// <param name="log">
    /// The variable storing code generation exceptions.
    /// </param>
    let buildReport (log: Ref<(exn * string) list>) = 
        [ for ex, stackFrame in log.Value |> List.rev do
            sprintf "Invalid query in: %s\n%s\n%s" 
                stackFrame 
                ex.Message 
                (if ex.InnerException <> null then ex.InnerException.Message else "")
        ]
        |> String.concat "\n=================\n"

        