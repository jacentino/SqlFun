namespace SqlFun

module Transforms =
    
    open System
    open System.Reflection
    open System.Linq.Expressions
    open Microsoft.FSharp.Reflection
    open ExpressionExtensions

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
        /// Builds a tree from a sequence.
        /// </summary>
        /// <param name="getKey1">
        /// Calculates a key of the element.
        /// </param>
        /// <param name="getKey2">
        /// Calculates a key of the elements parent.
        /// </param>
        /// <param name="combine">
        /// Combines an element with a child list.
        /// </param>
        /// <param name="source">
        /// First sequence to be transformed.
        /// </param>
        let tree (getKey1: 't -> 'k) (getKey2: 't -> 'k option) (combine: 't -> 't seq -> 't) (source: 't seq) =
            let rec makeSubtree (children: Map<'k option, 't seq>) (item: 't) =
                children 
                |> Map.tryFind (Some (getKey1 item) )
                |> Option.map (Seq.map (makeSubtree children) >> combine item) 
                |> Option.defaultValue item
            let roots, children = source |> Seq.groupBy getKey2 |> Seq.toList |> List.partition (fst >> Option.isSome)
            roots |> Seq.collect snd |> Seq.map (makeSubtree (children |> Map.ofList))

        /// <summary>
        /// Composes bigger transformation from two smaller ones.
        /// </summary>
        /// <param name="transform1">
        /// Function performing first transformation.
        /// </param>
        /// <param name="transform2">
        /// Function performing second transformation.
        /// </param>
        /// <param name="l1">
        /// First sequence participating in transformation.
        /// </param>
        /// <param name="l2">
        /// Second sequence participating in transformation.
        /// </param>
        /// <param name="l3">
        /// Third sequence participating in transformation.
        /// </param>
        let combineTransforms (transform1: ('t1 * 't2) -> 'r1) (transform2: ('r1 * 't3) -> 'r2) (l1: 't1, l2: 't2, l3: 't3) = 
            (transform1 (l1, l2), l3) |> transform2
                            

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
                
        static let parentKeyGetter: 'Parent -> 'Key = 
            let param = Expression.Parameter(typeof<'Parent>) 
            let getter = Expression.Lambda<Func<'Parent, 'Key>>(RelationshipBuilder<'Parent, 'Child>.GetParentKeyExpression param, param).Compile()
            getter.Invoke

        static let childKeyGetter: 'Child -> 'Key = 
            let param = Expression.Parameter(typeof<'Child>)
            let getter = Expression.Lambda<Func<'Child, 'Key>>(RelationshipBuilder<'Parent, 'Child>.GetChildKeyExpression param, param).Compile()
            getter.Invoke

        static let combiner = fun p c -> RelationshipBuilder<'Parent, 'Child>.combine (p, c)

        static member ValidateGetters () =
            parentKeyGetter |> ignore
            childKeyGetter |> ignore

        static member join (p: 'Parent seq, cs: 'Child seq) = 
            Standard.join parentKeyGetter childKeyGetter combiner (p, cs)
    
    and RelationshipBuilder<'Parent, 'Child>() =
            
        static let isChildObjectWrapper (typ: Type) = 
            typ.GetInterface("IChildObject`1") <> null

        static let isChildProperty (prop: PropertyInfo) = 
            let intf = prop.DeclaringType.GetInterface("IChildObject`1")
            intf <> null && intf.GetGenericArguments().[0] = prop.PropertyType

        static let getChildProperty (wrapperType: Type) = 
            let childType = wrapperType.GetInterface("IChildObject`1").GetGenericArguments().[0]
            wrapperType.GetProperties() |> Array.find (fun p -> p.PropertyType = childType)
            

        static let getWrapperChain (t: Type) =
            let allTypes = 
                Array.unfold (fun (p: Type) -> 
                    if p <> null then
                        let np = p.GetInterface("IChildObject`1")
                        Some (p, if np <> null then np.GetGenericArguments().[0] else null)
                    else
                        None) t
            Array.take (allTypes.Length - 1) allTypes, Array.last allTypes

        static let _, unwrappedParentType = getWrapperChain typeof<'Parent>
            
        static let childWrapperTypes, unwrappedChildType = getWrapperChain typeof<'Child>

        static let unwrappedChildKeyNames = 
            let pname = unwrappedParentType.Name.Split('`').[0].ToLower() 
            [ pname + "id"; pname + "_id" ]

        static let childKeyNames = 
            (childWrapperTypes 
             |> Seq.collect (fun wt -> wt.GetProperties() |> Seq.filter (isChildProperty >> not) |> Seq.map (fun p -> p.Name.ToLower()))
             |> Seq.toList)
            @ unwrappedChildKeyNames

        static let unwrappedParentKeyNames = "id" :: unwrappedChildKeyNames

        static let parentKeyNames = "id" :: childKeyNames
    
        static let tryGetKeyProp attrib names (t: Type) = 
            t.GetProperties() 
            |> Seq.tryFind (fun p -> p.GetCustomAttribute(attrib) <> null || names |> List.exists ((=) (p.Name.ToLower())))

        static let keyType = 
            if childWrapperTypes.Length <> 0 then
                childWrapperTypes 
                |> Array.collect (fun t -> t.GetProperties() |> Array.filter (isChildProperty >> not))
                |> Some
            else
                unwrappedChildType
                |> tryGetKeyProp typeof<IdAttribute> unwrappedChildKeyNames 
                |> Option.map Array.singleton
            |> Option.map (Array.map (fun p -> p.PropertyType) >> FSharpType.MakeTupleType)


        static let joinerOpt = 
            keyType 
            |> Option.map (fun ktype -> 
                let builder = typedefof<RelationshipBuilder<_, _, _>>.MakeGenericType(typeof<'Parent>, typeof<'Child>, ktype)
                let parents = Expression.Parameter(typeof<'Parent seq>)
                let children = Expression.Parameter(typeof<'Child seq>)
                let joinMethod = builder.GetMethod("join", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
                let joiner = Expression.Lambda<Func<'Parent seq, 'Child seq, 'Parent seq>>(
                                Expression.Call(null, joinMethod, parents, children), parents, children).Compile()
                let validator = builder.GetMethod("ValidateGetters", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
                validator.Invoke (null, [||]) |> ignore
                joiner.Invoke)

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

        static let rec getKeyProperties (parent: Expression, target: Type, tryGetKeyProp: Type -> PropertyInfo option) =
            if parent.Type = target then
                tryGetKeyProp target 
                |> Option.map (fun p -> p.Name, Expression.MakeMemberAccess(parent, p) :> Expression) 
                |> Option.toList
            else
            [ for p in parent.Type.GetProperties() do  
                if not (isChildProperty p) then
                    p.Name, Expression.MakeMemberAccess(parent, p) :> Expression
                else
                    yield! getKeyProperties(Expression.MakeMemberAccess(parent, p), target, tryGetKeyProp)
            ]

        static let mapMethodInfo = 
            (typeof<System.Linq.Enumerable>.GetMethods() |> Array.find (fun m -> m.Name = "Select"))
              .MakeGenericMethod(typeof<'Child>, unwrappedChildType)

        static member GetParentKeyExpression (parameter: ParameterExpression) = 
            let keyProps = 
                getKeyProperties(parameter, unwrappedParentType, tryGetKeyProp typeof<IdAttribute> unwrappedParentKeyNames)
                |> List.filter (fun (name, _) -> parentKeyNames |> List.contains (name.ToLower()))
                |> List.sortBy (fun (name, _) -> parentKeyNames |> List.findIndex ((=) (name.ToLower())))
            if not keyProps.IsEmpty then
                keyProps |> List.map snd |> Expression.NewTuple 
            else
                failwithf "No key fields found in %s type." parameter.Type.Name

        static member GetChildKeyExpression (parameter: ParameterExpression) = 
            let keyProps = 
                getKeyProperties(parameter, unwrappedChildType, 
                    if childWrapperTypes.Length = 0 then
                        tryGetKeyProp typeof<ParentIdAttribute> unwrappedChildKeyNames
                    else
                        fun _ -> None)
                |> List.map snd
            if not keyProps.IsEmpty then
                keyProps |> Expression.NewTuple 
            else
                failwithf "No key fields found in %s type. Expected one of: %A" 
                    parameter.Type.Name 
                    unwrappedChildKeyNames
                

        static member join: 'Parent seq * 'Child seq -> 'Parent seq = 
            joinerOpt |> Option.defaultWith (fun () -> failwithf "Can not determine key type for relation: %s -> %s" typeof<'Parent>.Name typeof<'Child>.Name)

        static member GetUnwrapChildExpression (child: Expression): Expression = 
            if isChildObjectWrapper child.Type then
                RelationshipBuilder<'Parent, 'Child>.GetUnwrapChildExpression (Expression.MakeMemberAccess(child, getChildProperty child.Type))
            else    
                child

        static member GetCombineExpression (parent: Expression, unwrappedChildren: Expression): Expression = 
            let fields = FSharpType.GetRecordFields parent.Type 
            let construct = parent.Type.GetConstructor(fields |> Array.map (fun p -> p.PropertyType))
            let values = 
                if isChildObjectWrapper parent.Type then
                    [| for prop in fields do
                        if not (isChildProperty prop) then
                            Expression.MakeMemberAccess(parent, prop) :> Expression
                        else
                            RelationshipBuilder<'Parent, 'Child>.GetCombineExpression(Expression.MakeMemberAccess(parent, prop), unwrappedChildren)
                    |]
                else
                    let values = 
                        [| for prop in fields do
                            if prop.PropertyType = seqType then
                                yield unwrappedChildren 
                            elif prop.PropertyType = listType then
                                yield Expression.Call(listOfSeqMethodInfo, unwrappedChildren) :> Expression
                            elif prop.PropertyType = arrayType then
                                yield Expression.Call(arrayOfSeqMethodInfo, unwrappedChildren) :> Expression
                            elif prop.PropertyType = setType then
                                yield Expression.Call(setOfSeqMethodInfo, unwrappedChildren) :> Expression
                            elif prop.PropertyType = unwrappedChildType then
                                yield Expression.Call(headMethodInfo, unwrappedChildren) :> Expression
                            elif prop.PropertyType = optionType then
                                yield Expression.Call(tryHeadMethodInfo, unwrappedChildren) :> Expression
                            else 
                                yield Expression.MakeMemberAccess(parent, prop) :> Expression
                        |]
                    match values |> Seq.filter (fun v -> supportedFieldTypes |> List.contains v.Type) |> Seq.length with
                    | 1 -> values  
                    | 0 -> failwithf "Property of type %s list not found in parent type %s" unwrappedChildType.Name unwrappedParentType.Name
                    | _ -> failwithf "More than one property of type %s list found in parent type %s" unwrappedChildType.Name unwrappedParentType.Name
            Expression.New(construct, values) :> Expression

        static member val combine: 'Parent * 'Child seq -> 'Parent = 
            let parent = Expression.Parameter typeof<'Parent>
            let children = Expression.Parameter typeof<'Child seq>
            let child = Expression.Parameter typeof<'Child>
            let unwrapOneChild = Expression.Lambda(RelationshipBuilder<'Parent, 'Child>.GetUnwrapChildExpression child, child) 
            let unwrappedChildren = Expression.Call(mapMethodInfo, children, unwrapOneChild)
            let combineExpr = RelationshipBuilder<'Parent, 'Child>.GetCombineExpression(parent, unwrappedChildren)
            let builder = Expression.Lambda<Func<'Parent, 'Child seq, 'Parent>>(combineExpr, parent, children).Compile()
            builder.Invoke


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
    /// </summary>
    module Conventions =         

        /// <summary>
        /// Joins collections of parent and child records by key.
        /// </summary>
        /// <remarks>
        /// Join can be used when:
        /// - parent record's key has one of id, {parent-name}Id or {parent-name}_id names, 
        /// - key in child record has {parent-name}Id or {parent-name}_id name,
        /// - parent has a property of child list type.
        ///</remarks>
        let inline join<'p, 'c> = RelationshipBuilder<'p, 'c>.join

        /// <summary>
        /// Combines a parent record with child record sequence.
        /// </summary>
        let combine<'p, 'c> = RelationshipBuilder<'p, 'c>.combine

        /// <summary>
        /// Builds parent record sequence from list of parent * child tuples.
        /// </summary>
        let group<'p, 'c when 'p: equality> = Standard.group (RelationshipBuilder<'p, 'c>.combine |> curry)


    type private CopyBuilder<'Source, 'Target>() = 

        static member val Copier = 
            let fieldTypes = FSharpType.GetRecordFields typeof<'Target> |> Array.map (fun p -> p.PropertyType)
            let construct = typeof<'Target>.GetConstructor(fieldTypes)
            let source = Expression.Parameter(typeof<'Source>)
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