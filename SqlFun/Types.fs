namespace SqlFun

module Types = 

    open System
    open FSharp.Reflection

    let isOption (t: Type) = 
        let x = t.Name.StartsWith("FSharpOption`1")
        x

    let (|OptionOf|_|) (t: Type) = 
        if t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<option<_>>
        then Some <| t.GetGenericArguments().[0]
        else None

    let isAsync (t: Type) = 
        let x = t.Name.StartsWith("FSharpAsync`1")
        x

    let (|AsyncOf|_|) (t: Type) = 
        if t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Async<_>>
        then Some <| t.GetGenericArguments().[0]
        else None

    let isSimpleType (t: Type) = 
        t.IsPrimitive || t.IsEnum || t = typeof<string> || t = typeof<DateTime> || t = typeof<Decimal> || t = typeof<byte[]> || t = typeof<Guid>

    let (|SimpleType|_|) (t: Type) = 
        if isSimpleType t then Some () else None

    let (|Unit|_|) (t: Type) = 
        if t = typeof<unit> then Some () else None

    let getUnderlyingType (optionType: Type) = 
        optionType.GetGenericArguments().[0]

    let isAsyncOf (t: Type) (underlying: Type) = 
        isAsync t && getUnderlyingType t = underlying

    let isSimpleTypeOption (t: Type) = 
        (isOption t) && isSimpleType (getUnderlyingType t)

    let (|SimpleTypeOption|_|) (t: Type) = 
        if isSimpleTypeOption t then Some () else None

    let isCollectionType (t: Type) = 
        typeof<System.Collections.IEnumerable>.IsAssignableFrom(t) && t <> typeof<string> && t <> typeof<byte[]>

    let (|CollectionOf|_|) (t: Type) = 
        if typeof<System.Collections.IEnumerable>.IsAssignableFrom(t) && t <> typeof<string> && t <> typeof<byte[]> 
        then Some <| t.GetGenericArguments().[0]
        else None

    let isComplexType (t: Type) =
        FSharpType.IsRecord t

    let (|Record|_|) (t: Type) = 
        if FSharpType.IsRecord t 
        then Some <| FSharpType.GetRecordFields t
        else None

    let (|Tuple|_|) (t: Type) = 
        if FSharpType.IsTuple t
        then Some <| FSharpType.GetTupleElements t
        else None

    let (|Function|_|) (t: Type) = 
        if FSharpType.IsFunction t
        then Some <| FSharpType.GetFunctionElements t
        else None