namespace SqlFun

open System
open System.Collections.Generic
open System.Linq.Expressions
open System.Reflection
open Microsoft.FSharp.Reflection

module ExpressionExtensions = 


    type Toolbox() = 
        static member MapOption (f: Func<'t1, 't2>) (opt: 't1 option) = Option.map f.Invoke opt

    let private getConcreteMethod (ownerType: Type) concreteTypes methodName = 
        let m = ownerType.GetMethod(methodName, BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
        if m <> null then
            Some <| m.MakeGenericMethod(concreteTypes)
        else
            None
        
    let rec findMethod (name: string) (paramTypes: Type[]) (typ: Type) = 
        let mth = typ.GetMethod(name, paramTypes)
        if mth <> null then
            Some mth
        else
            typ.GetInterfaces() |> Seq.map (findMethod name paramTypes) |> Seq.tryPick id

    type Expression with
    
        static member NewTuple (elements: Expression list) : Expression = 
            let elementTypes = elements |> List.map (fun expr -> expr.Type) |> List.toArray
            let creator = typeof<Tuple>.GetMethods() 
                            |> Array.find (fun m -> m.Name = "Create" && m.GetGenericArguments().Length = Array.length elementTypes)
            Expression.Call(creator.MakeGenericMethod(elementTypes), elements) :> Expression

        static member TupleGet (tuple: Expression, index: int) = 
            Expression.Property(tuple, "Item" + (index + 1).ToString())

        static member NewRecord (recordType: Type, elements: Expression list) = 
            let fieldTypes = FSharpType.GetRecordFields recordType |> Array.map (fun p -> p.PropertyType)
            let construct = recordType.GetConstructor(fieldTypes)
            Expression.New(construct, elements) :> Expression

        static member UnitConstant = Expression.Convert(Expression.Constant(()), typeof<unit>)

        static member UnitAsyncConstant = Expression.Convert(Expression.Constant(async { return () }), typeof<unit Async>)

        static member GetSomeUnionCase (optionType: Type) (value: Expression) = 
            Expression.Call(optionType, "Some", value)

        static member GetNoneUnionCase optionType = 
            Expression.Property(null, optionType, "None")

        static member Call (instance: Expression, methodName: string, arguments: IEnumerable<Expression>) = 
            let types = arguments |> Seq.map (fun e -> e.Type) |> Seq.toArray
            let mth = findMethod methodName types instance.Type
            match mth with
            | Some m -> Expression.Call(instance, m, arguments)
            | None -> failwith ("Method not found: " + methodName)

        static member Call (instance: Expression, methodName: string, [<ParamArray>]arguments: Expression array) = 
            let types = arguments |> Seq.map (fun e -> e.Type) |> Seq.toArray
            let mth = findMethod methodName types instance.Type
            match mth with
            | Some m -> Expression.Call(instance, m, arguments)
            | None -> failwith ("Method not found: " + methodName)

        static member Call (ownerType: Type, methodName: string, [<ParamArray>]arguments: Expression array) = 
            let types = arguments |> Seq.map (fun e -> e.Type) |> Seq.toArray
            let mth = findMethod methodName types ownerType
            match mth with
            | Some m -> Expression.Call(m, arguments)
            | None -> failwith ("Method not found: " + methodName)

        static member Call (ownerType: Type, genericParams: Type array, methodName: string, [<ParamArray>]arguments: Expression array ) =
            let mth = getConcreteMethod ownerType genericParams methodName 
            match mth with
            | Some m -> Expression.Call(m, arguments)
            | None -> failwith ("Method not found: " + methodName)

        static member Call (genericParams: Type array, methodName: string, [<ParamArray>]arguments: Expression array ) =
            Expression.Call(typeof<Toolbox>, genericParams, methodName, arguments)
