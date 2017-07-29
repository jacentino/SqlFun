namespace SqlFun

module Types = 

    open System
    open FSharp.Reflection

    let isOption (t: Type) = 
        let x = t.Name.StartsWith("FSharpOption`1")
        x

    let isAsync (t: Type) = 
        let x = t.Name.StartsWith("FSharpAsync`1")
        x

    let isSimpleType (t: Type) = 
        t.IsPrimitive || t.IsEnum || t = typeof<string> || t = typeof<DateTime> || t = typeof<Decimal> || t = typeof<byte[]>

    let getUnderlyingType (optionType: Type) = 
        optionType.GetGenericArguments().[0]

    let isAsyncOf (t: Type) (underlying: Type) = 
        isAsync t && getUnderlyingType t = underlying

    let isSimpleTypeOption (t: Type) = 
        (isOption t) && isSimpleType (getUnderlyingType t)

    let isCollectionType (t: Type) = 
        typeof<System.Collections.IEnumerable>.IsAssignableFrom(t) && t <> typeof<string> && t <> typeof<byte[]>

    let isComplexType (t: Type) =
        FSharpType.IsRecord t