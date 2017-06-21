namespace SqlFun.Exceptions

open System
open Microsoft.FSharp.Reflection

type CompileTimeException(func: Type, commandType: string, command: string, innerException: Exception) = 
    inherit Exception(sprintf "Error generating function %s for %s %s" (CompileTimeException.prettyPrint func) commandType command, innerException)

    static member isGeneric<'t> (t: Type) = 
        t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<'t>
    
    static member getItemType (t: Type) = 
        t.GetGenericArguments().[0]

    static member prettyPrint (t: Type) = 
        if FSharpType.IsFunction t then
            let arg, ret = FSharpType.GetFunctionElements t
            (CompileTimeException.prettyPrint arg) + " -> " + (CompileTimeException.prettyPrint ret)
        elif FSharpType.IsTuple t then
            "(" + (FSharpType.GetTupleElements t |> Array.map CompileTimeException.prettyPrint |> String.concat " * ") + ")"
        elif t = typeof<unit> then
            "unit"
        elif CompileTimeException.isGeneric<seq<_>> t then
            (CompileTimeException.prettyPrint (CompileTimeException.getItemType t)) + " seq"
        elif CompileTimeException.isGeneric<list<_>> t then
            (CompileTimeException.prettyPrint (CompileTimeException.getItemType t)) + " list"
        elif CompileTimeException.isGeneric<array<_>> t then
            (CompileTimeException.prettyPrint (CompileTimeException.getItemType t)) + " array"
        elif CompileTimeException.isGeneric<option<_>> t then
            (CompileTimeException.prettyPrint (CompileTimeException.getItemType t)) + " option"
        elif CompileTimeException.isGeneric<Async<_>> t then
            (CompileTimeException.prettyPrint (CompileTimeException.getItemType t)) + " Async"
        elif t.IsGenericType then
            t.Name + "<" + (t.GetGenericArguments() |> Array.map CompileTimeException.prettyPrint |> String.concat ", ") + ">"
        else
            t.Name



