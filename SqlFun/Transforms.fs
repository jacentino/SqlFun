﻿namespace SqlFun

module Transforms =
    
    open System
    open System.Reflection
    open System.Linq.Expressions
    open Microsoft.FSharp.Reflection

    let inline private unwrapAlias (alias: ^t): ^u =
        (^t: (member aliasedItem: ^u) alias)


    /// <summary>
    /// Transforms a sequence by getting an aliasedItem attribute values of each item.
    /// Used to add alias defined by PrefixedAttribute of the aliasedItem attribute to detail set in group.
    /// </summary>
    let inline aliased (items: ^t seq): ^u seq =
        items |> Seq.map unwrapAlias


    /// <summary>
    /// Type used to provide "item_" prefix for detail results in conjunction with aliasedAsItem function.
    /// </summary>
    type ItemAlias<'t> = 
        {
            [<Prefixed("item_")>] aliasedItem: 't
        }


    /// <summary>
    /// Transforms sequence by getting an aliasedItem attribute values of each item.
    /// Used to add "item_" alias to detail set in a group.
    /// </summary>
    let aliasedAsItem (items: ItemAlias<'t> seq): 't seq =
        items |> Seq.map (fun alias -> alias.aliasedItem)

    /// <summary>
    /// Provides basic set of result transformation functions.
    /// </summary>
    module Standard = 

        /// <summary>
        /// Performs grouping on tuple sequence, taking the first element of a tuple as a key, and the second as a value list.
        /// </summary>
        /// <param name="combine">
        /// Combines an element with a detail list.
        /// </param>
        /// <param name="seq">
        /// The source sequence of tuples.
        /// </param>
        let group (combine: 't1 -> 't2 seq -> 't1) (seq: ('t1 * 't2 option) seq) = 
            seq |> Seq.groupBy fst 
                |> Seq.map (fun (fst, grpseq) -> combine fst (grpseq |> Seq.map snd |> Seq.choose id))
    
        /// <summary>
        /// Joins two sequences by key.
        /// </summary>
        /// <param name="getKey1">
        /// Calculates a key of element of a first sequence.
        /// </param>
        /// <param name="getKey2">
        /// Calculates a key of element of a second sequence.
        /// </param>
        /// <param name="combine">
        /// Combines an element with a detail list.
        /// </param>
        /// <param name="seq1">
        /// First sequence participating in join.
        /// </param>
        /// <param name="seq2">
        /// Second sequence participating in join.
        /// </param>
        let join (getKey1: 't1 -> 'k) (getKey2: 't2 -> 'k) (combine: 't1 -> 't2 seq -> 't1) (seq1: 't1 seq, seq2: 't2 seq) = 
            let seq2ByKey = seq2 |> Seq.groupBy getKey2 |> Map.ofSeq
            seq1 |> Seq.map (fun item -> match Map.tryFind (getKey1 item) seq2ByKey with
                                            | Some values -> combine item (values |> List.ofSeq)
                                            | None -> item)

        /// <summary>
        /// Joins three results by combining two joins.
        /// </summary>
        /// <param name="join1">
        /// Function performing first join.
        /// </param>
        /// <param name="join2">
        /// Function performing second join.
        /// </param>
        /// <param name="l1">
        /// First sequence participating in join.
        /// </param>
        /// <param name="l2">
        /// Second sequence participating in join.
        /// </param>
        /// <param name="l3">
        /// Third sequence participating in join.
        /// </param>
        let combineTransforms (join1: ('t1 * 't2) -> 'r1) (join2: ('r1 * 't3) -> 'r2) (l1: 't1, l2: 't2, l3: 't3) = 
            (join1 (l1, l2), l3) |> join2
                            

    /// <summary>
    /// Transforms a value wrapped in Async object using a given function.
    /// </summary>
    /// <param name="f">
    /// Function transforming wrapped value.
    /// </param>
    /// <param name="x">
    /// Source value.
    /// </param>
    let mapAsync (f: 't -> 'v) (v: Async<'t>) = async {
            let! v1 = v
            return f(v1)
        }

    /// <summary>
    /// Transforms two single-arg functions to one function with tupled params, returning tuple of results of input functions.
    /// Used to combine two functions executing sql commands, to further join them or whatever. 
    /// </summary>
    let merge (f1: 'x1 -> 'y1) (f2: 'x2 -> 'y2) = fun (x1: 'x1, x2: 'x2) -> f1 x1, f2 x2


    /// <summary>
    /// Extract result sets from stored procedure result tuple.
    /// </summary>
    let resultOnly (_: int, (), result: 't) = result

    /// <summary>
    /// Extract output parameters from stored procedure result tuple.
    /// </summary>
    let outParamsOnly (_: int, outParams, ()) = outParams

    /// <summary>
    /// Converts two-arg tupled function to its curried form.
    /// </summary>
    let curry f x y = f(x, y)

    /// <summary>
    /// Converts three-arg tupled function to its curried form.
    /// </summary>
    let curry3 f x y z = f(x, y, z)

    /// <summary>
    /// Converts four-arg tupled function to its curried form.
    /// </summary> 
    let curry4 f x y z t = f(x, y, z, t)

    /// <summary>
    /// Interface allowing to use convention based transformations
    /// when child objects have no parent key fields.
    /// </summary>
    type IChildObject<'Child> = 
        abstract member Child: 'Child

            
    type private RelationshipBuilder<'Parent, 'Child, 'Key when 'Key: comparison>() = 
                
        static let parentKeyGetter: ('Parent -> 'Key) option = 
            let param = Expression.Parameter(typeof<'Parent>) 
            match RelationshipBuilder<'Parent, 'Child>.ParentKeyProp with
            | Some prop ->
                let getter = Expression.Lambda<Func<'Parent, 'Key>>(Expression.MakeMemberAccess(param, prop), param).Compile()
                Some getter.Invoke
            | None -> None

        static let childKeyGetter: ('Child -> 'Key) option = 
            let param = Expression.Parameter(typeof<'Child>)
            match RelationshipBuilder<'Parent, 'Child>.ChildKeyProp with
            | Some prop ->
                let getter = Expression.Lambda<Func<'Child, 'Key>>(Expression.MakeMemberAccess(param, prop), param).Compile()
                Some getter.Invoke
            | None -> None

        static let combiner = fun p c -> RelationshipBuilder<'Parent, 'Child>.combine (p, c)

        static member join (p: 'Parent seq, cs: 'Child seq) = 
            match parentKeyGetter, childKeyGetter with
            | None, _ -> failwith <| sprintf "Parent key of %s -> %s relation not found. Its name should be one of %s" 
                                             typeof<'Parent>.Name typeof<'Child>.Name 
                                             (RelationshipBuilder<'Parent, 'Child>.ParentKeyNames |> String.concat ", ")
            | _, None -> failwith <| sprintf "Child key of %s -> %s relation not found. Its name should be one of %s" 
                                             typeof<'Parent>.Name typeof<'Child>.Name 
                                             (RelationshipBuilder<'Parent, 'Child>.ChildKeyNames |> String.concat ", ")
            | Some pkGetter, Some ckGetter ->
                
                Standard.join pkGetter ckGetter combiner (p, cs)
    
    and RelationshipBuilder<'Parent, 'Child>() =
            
        static let childKeyNames = 
            let pname = typeof<'Parent>.Name.Split('`').[0].ToLower() 
            [pname + "id"; pname + "_id"]

        static let parentKeyNames = "id" :: childKeyNames
    
        static let tryGetKeyProp (t: Type) attrib names = 
            t.GetProperties() 
            |> Seq.tryFind (fun p -> p.GetCustomAttribute(attrib) <> null || names |> List.exists ((=) (p.Name.ToLower())))

        static let keyType = 
            tryGetKeyProp typeof<'Parent> typeof<IdAttribute> parentKeyNames 
            |> Option.map (fun p -> p.PropertyType)            

        static let joinerOpt = 
            keyType 
            |> Option.map (fun ktype -> 
                let builder = typedefof<RelationshipBuilder<_, _, _>>.MakeGenericType(typeof<'Parent>, typeof<'Child>, ktype)
                let parents = Expression.Parameter(typeof<'Parent seq>)
                let children = Expression.Parameter(typeof<'Child seq>)
                let joinMethod = builder.GetMethod("join", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
                let joiner = Expression.Lambda<Func<'Parent seq, 'Child seq, 'Parent seq>>(
                                Expression.Call(null, joinMethod, parents, children), parents, children).Compile()                        
                joiner.Invoke)

        static let unwrappedChildType = 
            match typeof<'Child>.GetInterface("IChildObject`1") with
            | null -> typeof<'Child>
            | rel  -> rel.GetGenericArguments().[0]

        static let fsharpCore = Assembly.Load("FSharp.Core")

        static let headMethodInfo = 
            fsharpCore.GetType("Microsoft.FSharp.Collections.SeqModule")
              .GetMethod("Head", BindingFlags.Static ||| BindingFlags.Public)
              .MakeGenericMethod(unwrappedChildType)

        static let tryHeadMethodInfo = 
            fsharpCore.GetType("Microsoft.FSharp.Collections.SeqModule")
              .GetMethod("TryHead", BindingFlags.Static ||| BindingFlags.Public)
              .MakeGenericMethod(unwrappedChildType)

        static let listOfSeqMethodInfo = 
            fsharpCore.GetType("Microsoft.FSharp.Collections.ListModule")
              .GetMethod("OfSeq", BindingFlags.Static ||| BindingFlags.Public)
              .MakeGenericMethod(unwrappedChildType)

        static let arrayOfSeqMethodInfo = 
            fsharpCore.GetType("Microsoft.FSharp.Collections.ArrayModule")
              .GetMethod("OfSeq", BindingFlags.Static ||| BindingFlags.Public)
              .MakeGenericMethod(unwrappedChildType)

        static let setOfSeqMethodInfo = 
            fsharpCore.GetType("Microsoft.FSharp.Collections.SetModule")
              .GetMethod("OfSeq", BindingFlags.Static ||| BindingFlags.Public)
              .MakeGenericMethod(unwrappedChildType)

        static let listType = typedefof<list<_>>.MakeGenericType(unwrappedChildType)
        static let arrayType = unwrappedChildType.MakeArrayType()
        static let setType = typedefof<Set<_>>.MakeGenericType(unwrappedChildType)
        static let seqType = typedefof<seq<_>>.MakeGenericType(unwrappedChildType)
        static let optionType = typedefof<option<_>>.MakeGenericType(unwrappedChildType)

        static let supportedFieldTypes = [ 
            listType
            arrayType
            setType
            seqType
            optionType
            unwrappedChildType 
        ]

        static member ParentKeyNames = parentKeyNames    
        static member ChildKeyNames = childKeyNames

        static member ParentKeyProp = 
            tryGetKeyProp typeof<'Parent> typeof<IdAttribute> parentKeyNames

        static member ChildKeyProp =  
            tryGetKeyProp typeof<'Child> typeof<ParentIdAttribute> childKeyNames

        static member join (p: 'Parent seq, c: 'Child seq): 'Parent seq = 
            let joiner = joinerOpt |> Option.defaultWith (fun () -> failwithf "Can not determine key type for: %A" typeof<'Parent>)
            joiner (p, c)

        static member UnwrapChildSeq (children: IChildObject<'RealChild> seq) = 
            children |> Seq.map (fun c -> c.Child)

        static member val combine: 'Parent * 'Child seq -> 'Parent = 
            let fieldTypes = FSharpType.GetRecordFields typeof<'Parent> |> Array.map (fun p -> p.PropertyType)
            let construct = typeof<'Parent>.GetConstructor(fieldTypes)
            let parent = Expression.Parameter(typeof<'Parent>)
            let children = Expression.Parameter(typeof<'Child seq>)
            let unwrappedChildSeq = 
                if unwrappedChildType = typeof<'Child> then
                    children :> Expression
                else
                    Expression.Call(typeof<RelationshipBuilder<_, _>>
                                        .GetMethod("UnwrapChildSeq")
                                        .MakeGenericMethod(unwrappedChildType), 
                                    children)
                    :> Expression
            let values = 
                FSharpType.GetRecordFields typeof<'Parent>
                |> Array.map (fun prop -> 
                                if prop.PropertyType = seqType
                                then unwrappedChildSeq 
                                elif prop.PropertyType = listType
                                then Expression.Call(listOfSeqMethodInfo, unwrappedChildSeq) :> Expression
                                elif prop.PropertyType = arrayType
                                then Expression.Call(arrayOfSeqMethodInfo, unwrappedChildSeq) :> Expression
                                elif prop.PropertyType = setType
                                then Expression.Call(setOfSeqMethodInfo, unwrappedChildSeq) :> Expression
                                elif prop.PropertyType = unwrappedChildType
                                then Expression.Call(headMethodInfo, unwrappedChildSeq) :> Expression
                                elif prop.PropertyType = optionType
                                then Expression.Call(tryHeadMethodInfo, unwrappedChildSeq) :> Expression
                                else Expression.MakeMemberAccess(parent, prop) :> Expression)
            match values |> Seq.filter (fun v -> supportedFieldTypes |> List.contains v.Type) |> Seq.length with
            | 1 ->
                let builder = Expression.Lambda<Func<'Parent, 'Child seq, 'Parent>>(Expression.New(construct, values), parent, children).Compile()         
                fun (p, cs) -> builder.Invoke(p, cs)
            | 0 ->
                failwithf "Property of type %s list not found in parent type %s" unwrappedChildType.Name typeof<'Parent>.Name
            | _ -> 
                failwithf "More than one property of type %s list found in parent type %s" unwrappedChildType.Name typeof<'Parent>.Name


    /// <summary>
    /// Combines two functions transforming query results into one function with more parameters.
    /// The return value of the first function becomes a first parameter of the second function.
    /// </summary>
    /// <param name="f">
    /// The first result transformation function.
    /// </param>
    /// <param name="g">
    /// The second result transformation function.
    /// </param>
    /// <param name="t1">
    /// The first argument of constructed function.
    /// </param>
    /// <param name="t2">
    /// The second argument of constructed function.
    /// </param>
    let (>->) (f: 't1 -> 'r1) (g: 'r1 * 't2 -> 'r2) = fun (t1, t2) -> g(f t1, t2)

    /// <summary>
    /// Combines two functions transforming query results into one function with more parameters.
    /// The return value of the second function becomes a second parameter of the first function.
    /// </summary>
    /// <param name="f">
    /// The first result transformation function.
    /// </param>
    /// <param name="g">
    /// The second result transformation function.
    /// </param>
    /// <param name="t1">
    /// The first argument of constructed function.
    /// </param>
    /// <param name="t2">
    /// The second argument of constructed function.
    /// </param>
    let (>>-) (f: 't1 * 'r1 -> 'r2) (g: 't2 -> 'r1) = fun (t1, t2) -> f(t1, g(t2))


    /// <summary>
    /// Provides result transformation functions based on conventions.
    /// <summary>
    module Conventions =         

        /// <summary>
        /// Joins collections of parent and child records by key.
        /// </summary>
        /// <remarks>
        /// Join can be used when:
        /// - parent record's key has one of id, <parent-name>Id or <parent-name>_id names, 
        /// - key in child record has <parent-name>Id or <parent-name>_id name,
        /// - parent has a property of child list type.
        ///</remarks>
        /// <param name="p">
        /// Perent sequence.
        /// </param>
        /// <param name="c">
        /// Child sequence.
        /// </param>
        let join<'p, 'c>(p, c) = RelationshipBuilder<'p, 'c>.join(p, c)

        /// <summary>
        /// Combines a parent record with child record sequence.
        /// </summary>
        /// <param name="p">
        /// Parent record.
        /// </param>
        /// <param name="c">
        /// Child record sequence.
        /// </param>
        let combine<'p, 'c>(p, c) = RelationshipBuilder<'p, 'c>.combine(p, c)

        /// <summary>
        /// Builds parent record sequence from list of parent * child tuples.
        /// </summary>
        /// <param name="pc">
        /// Sequence of parent * child tuples.
        /// </param>
        let group<'p, 'c when 'p: equality>(pc) = 
            pc |> Standard.group (RelationshipBuilder<'p, 'c>.combine |> curry)


    type private CopyBuilder<'Source, 'Target>() = 

        static member val Copier = 
            let fieldTypes = FSharpType.GetRecordFields typeof<'Target> |> Array.map (fun p -> p.PropertyType)
            let construct = typeof<'Target>.GetConstructor(fieldTypes)
            let source = Expression.Parameter(typeof<'Source>)
            let target = Expression.Parameter(typeof<'Target>)
            let srcFields = FSharpType.GetRecordFields typeof<'Source>
                            |> Seq.map (fun p -> (p.Name, p.PropertyType.FullName), p)
                            |> Map.ofSeq
            let values = FSharpType.GetRecordFields typeof<'Target>
                            |> Array.map (fun tgt -> 
                                            match srcFields.TryFind (tgt.Name, tgt.PropertyType.FullName) with
                                            | Some p -> Expression.MakeMemberAccess(source, p) :> Expression
                                            | None -> Expression.Default tgt.PropertyType :> Expression)
            let copier = Expression.Lambda<Func<'Source, 'Target>>(Expression.New(construct, values), source).Compile()
            copier.Invoke

    /// <summary>
    /// Makes a copy of a record of one type as another type. When source field  does not exist or has
    /// different type, than target field, assigns it with default value.
    /// </summary>
    type Copy<'Target>() =
        
        /// <summary>
        /// Performs copy.
        /// </summary>
        /// <param name="source">
        /// Source record.
        /// </param>
        static member From (source: 'Source) = CopyBuilder<'Source, 'Target>.Copier source