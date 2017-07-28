namespace SqlFun

module Types = 

    open System

    let isOption (t: Type) = 
        let x = t.Name.StartsWith("FSharpOption`1")
        x

    let isAsync (t: Type) = 
        let x = t.Name.StartsWith("FSharpAsync`1")
        x

    let isSimpleType (t: Type) = 
        let x = (t.FullName.StartsWith("System") && not (t.FullName.StartsWith("System.Collections")) && not (t.FullName.StartsWith("System.Tuple"))) || t.IsEnum
        x

    let getUnderlyingType (optionType: Type) = 
        optionType.GetGenericArguments().[0]

    let isAsyncOf (t: Type) (underlying: Type) = 
        isAsync t && getUnderlyingType t = underlying

    let isSimpleTypeOption (t: Type) = 
        (isOption t) && isSimpleType (getUnderlyingType t)

    let isCollectionType (t: Type) = 
        typeof<System.Collections.IEnumerable>.IsAssignableFrom(t) && t <> typeof<string>

    let isComplexType (t: Type) =
        not (isOption t || isSimpleType t || isCollectionType t)