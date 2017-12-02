namespace SqlFun

module Transforms =
    
    open System
    open System.Reflection
    open System.Linq.Expressions

    open Queries
    open Microsoft.FSharp.Reflection

    let inline private unwrapAlias (alias: ^t): ^u =
        (^t: (member aliasedItem: ^u) alias)


    /// <summary>
    /// Transforms list by getting an aliasedItem attribute values of each item.
    /// Used to add alias defined by PrefixedAttribute of the aliasedItem attribute to detail set in group.
    /// </summary>
    let inline aliased (items: ^t list): ^u list =
        items |> List.map unwrapAlias


    /// <summary>
    /// Type used to provide "item_" prefix for detail results in conjunction with aliasedAsItem function.
    /// </summary>
    type ItemAlias<'t> = 
        {
            [<Prefixed("item_")>] aliasedItem: 't
        }


    /// <summary>
    /// Transforms list by getting an aliasedItem attribute values of each item.
    /// Used to add "item_" alias to detail set in group.
    /// </summary>
    let aliasedAsItem (items: ItemAlias<'t> list): 't list =
        items |> List.map (fun alias -> alias.aliasedItem)


    /// <summary>
    /// Performs grouping on tuple list, taking the first element of a tuple as a key, and the second as a value list.
    /// </summary>
    let group (combine: 't1 -> 't2 list -> 't1) (list: ('t1 * 't2 option) list) = 
        list |> Seq.groupBy fst 
             |> Seq.map (fun (fst, grplist) -> combine fst (grplist |> Seq.map snd |> Seq.choose id |> List.ofSeq))
             |> List.ofSeq
    

    /// <summary>
    /// Performs grouping on tuple list, taking the first element of a tuple as a key, and the second and third as value lists.
    /// </summary>
    let group3 (combine: 't1 -> 't2 list -> 't3 list -> 't1)  (list: ('t1 * 't2 * 't3) list)  = 
        list |> Seq.groupBy (fun (a, _, _) -> a)
             |> Seq.map (fun (fst, grplist) -> 
                         let (_, lb, lc) = grplist |> List.ofSeq |> List.unzip3 
                         combine fst (lb |> List.distinct) (lc |> List.distinct))
             |> List.ofSeq
    

    let private unzip4 (list: ('t1 * 't2 * 't3 * 't4) list): 't1 list * 't2 list * 't3 list * 't4 list =
        let l1, l2, l3, l4 = List.fold (fun (l1, l2, l3, l4) (a1, a2, a3, a4) -> a1 :: l1, a2 :: l2, a3 :: l3, a4 :: l4) ([], [], [], []) list
        List.rev l1, List.rev l2, List.rev l3, List.rev l4

    /// <summary>
    /// Performs grouping on tuple list, taking the first element of a tuple as a key, and the second and third and fourth as value lists.
    /// </summary>
    let group4 (combine: 't1 -> 't2 list -> 't3 list -> 't4 list -> 't1)  (list: ('t1 * 't2 * 't3 * 't4) list)  = 
        list |> Seq.groupBy (fun (a, _, _, _) -> a)
             |> Seq.map (fun (fst, grplist) -> 
                         let (_, lb, lc, ld) = grplist |> List.ofSeq |> unzip4
                         combine fst (lb |> List.distinct) (lc |> List.distinct))
             |> List.ofSeq
    

    /// <summary>
    /// Joins two lists by key.
    /// </summary>
    let join (getKey1: 't1 -> 'k) (getKey2: 't2 -> 'k) (combine: 't1 -> 't2 list -> 't1) (list1: 't1 list, list2: 't2 list) = 
        if list1.Length > 10 then
            let list2ByKey = list2 |> Seq.groupBy getKey2 |> Map.ofSeq
            list1 |> List.map (fun item -> match Map.tryFind (getKey1 item) list2ByKey with
                                            | Some values -> combine item (values |> List.ofSeq)
                                            | None -> item)
        else
            list1 |> List.map (fun item -> combine item (list2 |> List.filter (fun v -> (getKey2 v) = (getKey1 item))))

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
    /// First list participating in join.
    /// </param>
    /// <param name="l2">
    /// Second list participating in join.
    /// </param>
    /// <param name="l3">
    /// Third list participating in join.
    /// </param>
    let combineTransforms (join1: ('t1 * 't2) -> 'r1) (join2: ('r1 * 't3) -> 'r2) (l1: 't1, l2: 't2, l3: 't3) = 
        (join1 (l1, l2), l3) |> join2
                            
    /// <summary>
    /// Joins four results by combining three joins.
    /// </summary>
    /// <param name="join1">
    /// Function performing first join.
    /// </param>
    /// <param name="join2">
    /// Function performing second join.
    /// </param>
    /// <param name="join3">
    /// Function performing third join.
    /// </param>
    /// <param name="l1">
    /// First list participating in join.
    /// </param>
    /// <param name="l2">
    /// Second list participating in join.
    /// </param>
    /// <param name="l3">
    /// Third list participating in join.
    /// </param>
    /// <param name="l4">
    /// Fourth list participating in join.
    /// </param>
    let combineTransforms3 (join1: ('t1 * 't2) -> 'r1) 
                           (join2: ('r1 * 't3) -> 'r2) 
                           (join3: ('r2 * 't4) -> 'r3)
                           (l1: 't1 , l2: 't2, l3: 't3, l4: 't4) = 
        (combineTransforms join1 join2 (l1, l2, l3), l4) |> join3
                            
    /// <summary>
    /// Joins four results by combining three joins.
    /// </summary>
    /// <param name="join1">
    /// Function performing first join.
    /// </param>
    /// <param name="join2">
    /// Function performing second join.
    /// </param>
    /// <param name="join3">
    /// Function performing third join.
    /// </param>
    /// <param name="join4">
    /// Function performing fourth join.
    /// </param>
    /// <param name="l1">
    /// First list participating in join.
    /// </param>
    /// <param name="l2">
    /// Second list participating in join.
    /// </param>
    /// <param name="l3">
    /// Third list participating in join.
    /// </param>
    /// <param name="l4">
    /// Fourth list participating in join.
    /// </param>
    /// <param name="l4">
    /// Fifth list participating in join.
    /// </param>
    let combineTransforms4 (join1: ('t1 * 't2) -> 'r1) 
                           (join2: ('r1 * 't3) -> 'r2) 
                           (join3: ('r2 * 't4) -> 'r3)
                           (join4: ('r3 * 't5) -> 'r4)
                           (l1: 't1 , l2: 't2, l3: 't3, l4: 't4, l5: 't5) = 
        (combineTransforms3 join1 join2 join3 (l1, l2, l3, l4), l5) |> join4
                            
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
    let resultOnly (map: 't -> 'u) (_: int, (), result: 't) = 
        map result

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
    let curry3 f x y z= f(x, y, z)

    /// <summary>
    /// Converts four-arg tupled function to its curried form.
    /// </summary>
    let curry4 f x y z t = f(x, y, z, t)


            
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

        static let combiner: Choice<'Parent -> 'Child list -> 'Parent, string> = 
            let fieldTypes = FSharpType.GetRecordFields typeof<'Parent> |> Array.map (fun p -> p.PropertyType)
            let construct = typeof<'Parent>.GetConstructor(fieldTypes)
            let parent = Expression.Parameter(typeof<'Parent>)
            let children = Expression.Parameter(typeof<'Child list>)
            let values = FSharpType.GetRecordFields typeof<'Parent>
                            |> Array.map (fun prop -> if prop.PropertyType <> typeof<'Child list> 
                                                        then Expression.MakeMemberAccess(parent, prop) :> Expression
                                                        else children :> Expression)
            match values |> Seq.filter (fun v -> v.Type = typeof<'Child list>) |> Seq.length with
            | 1 ->
                let builder = Expression.Lambda<Func<'Parent, 'Child list, 'Parent>>(Expression.New(construct, values), parent, children).Compile()         
                Choice1Of2 (fun p cs -> builder.Invoke(p, cs))
            | 0 ->
                Choice2Of2 (sprintf "Property of type %s list not found in parent type %s" typeof<'Child>.Name typeof<'Parent>.Name)
            | _ -> 
                Choice2Of2 (sprintf "More than one property of type %s list found in parent type %s" typeof<'Child>.Name typeof<'Parent>.Name)

        static member join (p: 'Parent list, cs: 'Child list) = 
            match parentKeyGetter, childKeyGetter, combiner with
            | None, _, _ -> failwith <| sprintf "Parent key of %s -> %s relation not found. Its name should be one of %s" 
                                             typeof<'Parent>.Name typeof<'Child>.Name 
                                             (RelationshipBuilder<'Parent, 'Child>.ParentKeyNames |> String.concat ", ")
            | _, None, _ -> failwith <| sprintf "Child key of %s -> %s relation not found. Its name should be one of %s" 
                                             typeof<'Parent>.Name typeof<'Child>.Name 
                                             (RelationshipBuilder<'Parent, 'Child>.ChildKeyNames |> String.concat ", ")
            | _, _, Choice2Of2 err -> failwith err
            | Some pkGetter, Some ckGetter, Choice1Of2 combiner ->
                join pkGetter ckGetter combiner (p, cs)

        static member combine (p: 'Parent, cs:'Child list) = 
            match combiner with
            | Choice1Of2 c -> c p cs
            | Choice2Of2 err -> failwith err
    
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
            |> Option.get

        static let builderType = 
            typeof<RelationshipBuilder<_, _, _>>
                .GetGenericTypeDefinition()
                .MakeGenericType(typeof<'Parent>, typeof<'Child>, keyType)
        
        static member ParentKeyNames = parentKeyNames    
        static member ChildKeyNames = childKeyNames
        static member ParentKeyProp = tryGetKeyProp typeof<'Parent> typeof<IdAttribute> parentKeyNames
        static member ChildKeyProp = tryGetKeyProp typeof<'Child> typeof<ParentIdAttribute> childKeyNames

        static member val joiner: ('Parent list * 'Child list) -> 'Parent list = 
            let parents = Expression.Parameter(typeof<'Parent list>)
            let children = Expression.Parameter(typeof<'Child list>)
            let joinMethod = builderType.GetMethod("join", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
            let joiner = Expression.Lambda<Func<'Parent list, 'Child list, 'Parent list>>(
                            Expression.Call(null, joinMethod, parents, children), parents, children).Compile()                        
            joiner.Invoke

        static member val combiner: 'Parent * 'Child list -> 'Parent = 
            let combineMethod = builderType.GetMethod("combine", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
            let parent = Expression.Parameter(typeof<'Parent>)
            let children = Expression.Parameter(typeof<'Child list>)
            let combine = Expression.Lambda<Func<'Parent, 'Child list, 'Parent>>(
                            Expression.Call(null, combineMethod, parent, children), parent, children).Compile()                        
            combine.Invoke


    /// <summary>
    /// Provides methods for joining two results when code follows conventions.
    /// </summary>
    type Join<'Child>() = 
        
        /// <summary>
        /// Joins collections of parent and child records by key.
        /// </summary>
        /// <remarks>
        /// Join can be used when:
        /// - parent record's key has one of id, <parent-name>Id or <parent-name>_id names, 
        /// - key in child record has <parent-name>Id or <parent-name>_id name,
        /// - parent has a property of child list type.
        ///</remarks>
        /// <param name="ps">
        /// Perent record list.
        /// </param>
        /// <param name="cs">
        /// Child record list.
        /// </param>
        static member Left (ps: 'Parent list, cs: 'Child list): 'Parent list = 
            RelationshipBuilder<'Parent, 'Child>.joiner (ps, cs)
        
        /// <summary>
        /// Joins collections of parent and child records by key.
        /// </summary>
        /// <remarks>
        /// Join can be used when:
        /// - parent record's key has one of id, <parent-name>Id or <parent-name>_id names, 
        /// - key in child record has <parent-name>Id or <parent-name>_id name,
        /// - parent has a property of child list type.
        ///</remarks>
        /// <param name="cs">
        /// Child record list.
        /// </param>
        /// <param name="ps">
        /// Perent record list.
        /// </param>
        static member Right (cs: 'Child list, ps: 'Parent list): 'Parent list = 
            RelationshipBuilder<'Parent, 'Child>.joiner (ps, cs)


    /// <summary>
    /// Provides methods for joining three results when code follows conventions.
    /// </summary>
    type Join<'Child1, 'Child2>() = 
        /// <summary>
        /// Joins collections of parent, child1 and child2 records by key.
        /// </summary>
        /// <remarks>
        /// Join can be used when:
        /// - parent record's key has one of id, <parent-name>Id or <parent-name>_id names, 
        /// - key in child record has <parent-name>Id or <parent-name>_id name,
        /// - parent has a property of child list type.
        ///</remarks>
        /// <param name="ps">
        /// Perent record list.
        /// </param>
        /// <param name="cs1">
        /// First child record list.
        /// </param>
        /// <param name="cs2">
        /// Second child record list.
        /// </param>
        static member Left (ps: 'Parent list, cs1: 'Child1 list, cs2: 'Child2 list): 'Parent list = 
            let ps1 = Join<'Child1>.Left (ps, cs1)            
            RelationshipBuilder<'Parent, 'Child2>.joiner (ps1, cs2)

        /// <summary>
        /// Joins collections of parent, child1 and child1 records by key, 
        /// although the childs lists are leading parameters.
        /// </summary>
        /// <remarks>
        /// Join can be used when:
        /// - parent record's key has one of id, <parent-name>Id or <parent-name>_id names, 
        /// - key in child record has <parent-name>Id or <parent-name>_id name,
        /// - parent has a property of child list type.
        ///</remarks>
        /// <param name="ps">
        /// Parent record list.
        /// </param>
        /// <param name="cs1">
        /// First child record list.
        /// </param>
        /// <param name="cs2">
        /// Second child record list.
        /// </param>
        static member Right (cs1: 'Child1 list, cs2: 'Child2 list, ps: 'Parent list): 'Parent list = 
            let ps1 = Join<'Child1>.Left (ps, cs1)            
            RelationshipBuilder<'Parent, 'Child2>.joiner (ps1, cs2)


    /// <summary>
    /// Provides methods for joining three results when code follows conventions.
    /// </summary>
    type Join<'Child1, 'Child2, 'Child3>() = 
        
        /// <summary>
        /// Joins collections of parent, child1, child2 and child3 records by key.
        /// </summary>
        /// <remarks>
        /// Join can be used when:
        /// - parent record's key has one of id, <parent-name>Id or <parent-name>_id names, 
        /// - key in child record has <parent-name>Id or <parent-name>_id name,
        /// - parent has a property of child list type.
        ///</remarks>
        /// <param name="ps">
        /// Perent record list.
        /// </param>
        /// <param name="cs1">
        /// First child record list.
        /// </param>
        /// <param name="cs2">
        /// Second child record list.
        /// </param>
        /// <param name="cs3">
        /// Third child record list.
        /// </param>
        static member Left (ps: 'Parent list, cs1: 'Child1 list, cs2: 'Child2 list, cs3: 'Child3 list): 'Parent list = 
            let ps1 = Join<'Child1, 'Child2>.Left (ps, cs1, cs2)            
            RelationshipBuilder<'Parent, 'Child3>.joiner (ps1, cs3)

        /// <summary>
        /// Joins collections of parent, and three child records by key, 
        /// although the child list are leading parameters.
        /// </summary>
        /// <remarks>
        /// Join can be used when:
        /// - parent record's key has one of id, <parent-name>Id or <parent-name>_id names, 
        /// - key in child record has <parent-name>Id or <parent-name>_id name,
        /// - parent has a property of child list type.
        ///</remarks>
        /// <param name="ps">
        /// Parent record list.
        /// </param>
        /// <param name="cs1">
        /// First child record list.
        /// </param>
        /// <param name="cs2">
        /// Second child record list.
        /// </param>
        /// <param name="cs3">
        /// Third child record list.
        /// </param>
        static member Right (cs1: 'Child1 list, cs2: 'Child2 list, cs3: 'Child3 list, ps: 'Parent list): 'Parent list = 
            let ps1 = Join<'Child1, 'Child2>.Left (ps, cs1, cs2)            
            RelationshipBuilder<'Parent, 'Child3>.joiner (ps1, cs3)

    /// <summary>
    /// Provides methods for combining two results when code follows conventions.
    /// </summary>
    type Update<'Child>() = 
        
        /// <summary>
        /// Combines a parent record with child record list.
        /// </summary>
        /// <param name="p">
        /// Parent record.
        /// </param>
        /// <param name="cs">
        /// Child record list.
        /// </param>
        static member Left (p: 'Parent, cs: 'Child list): 'Parent = 
            RelationshipBuilder<'Parent, 'Child>.combiner (p, cs)
        
        /// <summary>
        /// Combines a parent record with child record list.
        /// </summary>
        /// <param name="p">
        /// Parent record.
        /// </param>
        /// <param name="cs">
        /// Child record list.
        /// </param>
        static member Right (cs: 'Child list, p: 'Parent): 'Parent = 
            RelationshipBuilder<'Parent, 'Child>.combiner (p, cs)

    /// <summary>
    /// Provides methods for combining three results when code follows conventions.
    /// </summary>
    type Update<'Child1, 'Child2>() = 
        
        /// <summary>
        /// Combines a parent record with child record list.
        /// </summary>
        /// <param name="p">
        /// Parent record.
        /// </param>
        /// <param name="cs1">
        /// First child record list.
        /// </param>
        /// <param name="cs2">
        /// Second child record list.
        /// </param>
        static member Left (p: 'Parent, cs1: 'Child1 list, cs2: 'Child2 list): 'Parent = 
            let p1 = RelationshipBuilder<'Parent, 'Child1>.combiner (p, cs1)
            let p2 = RelationshipBuilder<'Parent, 'Child2>.combiner (p1, cs2)
            p2
        
        /// <summary>
        /// Combines a parent record with child record list.
        /// </summary>
        /// <param name="p">
        /// Parent record.
        /// </param>
        /// <param name="cs1">
        /// First child record list.
        /// </param>
        /// <param name="cs2">
        /// Second child record list.
        /// </param>
        static member Right (cs1: 'Child1 list, cs2: 'Child2 list, p: 'Parent): 'Parent = 
            let p1 = RelationshipBuilder<'Parent, 'Child1>.combiner (p, cs1)
            let p2 = RelationshipBuilder<'Parent, 'Child2>.combiner (p, cs2)
            p2

    /// <summary>
    /// Provides methods for combining four results when code follows conventions.
    /// </summary>
    type Update<'Child1, 'Child2, 'Child3>() = 
        
        /// <summary>
        /// Combines a parent record with child record list.
        /// </summary>
        /// <param name="p">
        /// Parent record.
        /// </param>
        /// <param name="cs1">
        /// First child record list.
        /// </param>
        /// <param name="cs2">
        /// Second child record list.
        /// </param>
        /// <param name="cs3">
        /// Third child record list.
        /// </param>
        static member Left (p: 'Parent, cs1: 'Child1 list, cs2: 'Child2 list, cs3: 'Child3 list): 'Parent = 
            let p2 = Update<'Child1, 'Child2>.Left (p, cs1, cs2)
            let p3 = RelationshipBuilder<'Parent, 'Child3>.combiner (p2, cs3)
            p3
        
        /// <summary>
        /// Combines a parent record with child record list.
        /// </summary>
        /// <param name="p">
        /// Parent record.
        /// </param>
        /// <param name="cs1">
        /// First child record list.
        /// </param>
        /// <param name="cs2">
        /// Second child record list.
        /// </param>
        /// <param name="cs3">
        /// Third child record list.
        /// </param>
        static member Right (cs1: 'Child1 list, cs2: 'Child2 list, cs3: 'Child3 list, p: 'Parent): 'Parent = 
            let p2 = Update<'Child1, 'Child2>.Left (p, cs1, cs2)
            let p3 = RelationshipBuilder<'Parent, 'Child3>.combiner (p2, cs3)
            p3

    /// <summary>
    /// Provides methods for building hierarchical result from flat one by grouping when code follows conventions.
    /// </summary>
    type Group<'Child>() = 
        
        /// <summary>
        /// Builds parent record list from list of parent * child tuples.
        /// </summary>
        /// <param name="pcs">
        /// List of parent * child tuples.
        /// </param>
        static member Left (pcs: ('Parent * 'Child option) list): 'Parent list = 
            pcs |> group (RelationshipBuilder<'Parent, 'Child>.combiner |> curry)
        
        /// <summary>
        /// Builds parent record list from list of child * parent tuples.
        /// </summary>
        /// <param name="pcs">
        /// List of parent * child tuples.
        /// </param>
        static member Right (pcs: ('Child option * 'Parent) list): 'Parent list = 
            pcs |> List.map (fun (x, y) -> y, x) |> group (RelationshipBuilder<'Parent, 'Child>.combiner |> curry)


    /// <summary>
    /// Allows to compose different result transformations.
    /// </summary>
    type Results<'Child1, 'Child2, 'Child3, 'Child4, 'Child5, 'Result1>(f: ('Child1 * 'Child2 * 'Child3 * 'Child4 * 'Child5) -> 'Result1) = 
    
        member this.Compose = f

    type Results<'Child1, 'Child2, 'Child3, 'Child4, 'Result1>(f: ('Child1 * 'Child2 * 'Child3 * 'Child4) -> 'Result1) = 
    
        member this.Compose = f

        /// <summary>
        /// Adds a transformation building one result from two source results.
        /// </summary>
        /// <param name="f">
        /// Function transforming results.
        /// </param>
        member this.Transform(f1: ('Result1 * 'Child5) -> 'Result2) = 
            let f2 (c1, c2, c3, c4, c5) = f1 (f(c1, c2, c3, c4), c5)
            Results<'Child1, 'Child2, 'Child3, 'Child4, 'Child5, 'Result2> (f2)

        static member (>-) (r: Results<'Child1, 'Child2, 'Child3, 'Child4, 'Result1>, f1: ('Result1 * 'Child5) -> 'Result2) =
            r.Transform f1



    /// <summary>
    /// Allows to compose different result transformations.
    /// </summary>
    type Results<'Child1, 'Child2, 'Child3, 'Result1>(f: ('Child1 * 'Child2 * 'Child3) -> 'Result1) = 
    
        member this.Compose = f

        /// <summary>
        /// Adds a transformation building one result from two source results.
        /// </summary>
        /// <param name="f">
        /// Function transforming results.
        /// </param>
        member this.Transform(f1: ('Result1 * 'Child4) -> 'Result2) = 
            let f2 (c1, c2, c3, c4) = f1 (f(c1, c2, c3), c4)
            Results<'Child1, 'Child2, 'Child3, 'Child4, 'Result2> (f2)

        static member (>-) (r: Results<'Child1, 'Child2, 'Child3, 'Result1>, f1: ('Result1 * 'Child4) -> 'Result2) =
            r.Transform f1

        /// <summary>
        /// Adds a transformation building one result from three source results.
        /// </summary>
        /// <param name="f">
        /// Function transforming results.
        /// </param>
        member this.Transform(f1: ('Result1 * 'Child4 * 'Child5) -> 'Result2) = 
            let f2 (c1, c2, c3, c4, c5) = f1 (f(c1, c2, c3), c4, c5)
            Results<'Child1, 'Child2, 'Child3, 'Child4, 'Child5, 'Result2> (f2)

        static member (>-) (r: Results<'Child1, 'Child2, 'Child3, 'Result1>, f1: ('Result1 * 'Child4 * 'Child5) -> 'Result2) =
            r.Transform f1


    /// <summary>
    /// Allows to compose different result transformations.
    /// </summary>
    type Results<'Child1, 'Child2, 'Result1>(f: ('Child1 * 'Child2) -> 'Result1) = 
    
        member this.Compose = f

        /// <summary>
        /// Adds a transformation building one result from two source results.
        /// </summary>
        /// <param name="f">
        /// Function transforming results.
        /// </param>
        member this.Transform(f1: ('Result1 * 'Child3) -> 'Result2) = 
            let f2 (c1, c2, c3) = f1 (f(c1, c2), c3)
            Results<'Child1, 'Child2, 'Child3, 'Result2> (f2)

        static member (>-) (r: Results<'Child1, 'Child2, 'Result1>, f1: ('Result1 * 'Child3) -> 'Result2) = 
            r.Transform f1

        /// <summary>
        /// Adds a transformation building one result from three source results.
        /// </summary>
        /// <param name="f">
        /// Function transforming results.
        /// </param>
        member this.Transform(f1: ('Result1 * 'Child3 * 'Child4) -> 'Result2) = 
            let f2 (c1, c2, c3, c4) = f1 (f(c1, c2), c3, c4)
            Results<'Child1, 'Child2, 'Child3, 'Child4, 'Result2> (f2)
            
        static member (>-) (r: Results<'Child1, 'Child2, 'Result1>, f1: ('Result1 * 'Child3 * 'Child4) -> 'Result2) = 
            r.Transform f1

        /// <summary>
        /// Adds a transformation building one result from four source results.
        /// </summary>
        /// <param name="f">
        /// Function transforming results.
        /// </param>
        member this.Transform(f1: ('Result1 * 'Child3 * 'Child4 * 'Child5) -> 'Result2) = 
            let f2 (c1, c2, c3, c4, c5) = f1 (f(c1, c2), c3, c4, c5)
            Results<'Child1, 'Child2, 'Child3, 'Child4, 'Child5, 'Result2> (f2)

        static member (>-) (r: Results<'Child1, 'Child2, 'Result1>, f1: ('Result1 * 'Child3 * 'Child4 * 'Child5) -> 'Result2) = 
            r.Transform f1



    /// <summary>
    /// Allows to compose different result transformations.
    /// </summary>
    type Results() = 
    
        /// <summary>
        /// Adds a transformation building one result from two source results.
        /// </summary>
        /// <param name="f">
        /// Function transforming results.
        /// </param>
        static member Transform(f: ('Child1 * 'Child2) -> 'Parent) = 
            Results<'Child1, 'Child2, 'Parent>(f)

        static member (>-) (r: Results, f: ('Child1 * 'Child2) -> 'Parent) = 
            Results.Transform(f)

        /// <summary>
        /// Adds a transformation building one result from three source results.
        /// </summary>
        /// <param name="f">
        /// Function transforming results.
        /// </param>
        static member Transform(f: ('Child1 * 'Child2 * 'Child3) -> 'Parent) = 
            Results<'Child1, 'Child2, 'Child3, 'Parent>(f)

        static member (>-) (r: Results, f: ('Child1 * 'Child2 * 'Child3) -> 'Parent) = 
            Results.Transform(f)

        /// <summary>
        /// Adds a transformation building one result from four source results.
        /// </summary>
        /// <param name="f">
        /// Function transforming results.
        /// </param>
        static member Transform(f: ('Child1 * 'Child2 * 'Child3 * 'Child4) -> 'Parent) = 
            Results<'Child1, 'Child2, 'Child3, 'Child4, 'Parent>(f)

        static member (>-) (r: Results, f: ('Child1 * 'Child2 * 'Child3 * 'Child4) -> 'Parent) = 
            Results.Transform(f)

        static member Compose (r: Results<'Child1, 'Child2, 'Result1>) = r.Compose

        static member Compose (r: Results<'Child1, 'Child2, 'Child3, 'Result1>) = r.Compose

        static member Compose (r: Results<'Child1, 'Child2, 'Child3, 'Child4, 'Result1>) = r.Compose

        static member Compose (r: Results<'Child1, 'Child2, 'Child3, 'Child4, 'Child5, 'Result1>) = r.Compose


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