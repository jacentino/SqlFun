namespace SqlFun

module Transforms =
    
    open Future
    open Queries

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


    let combineJoins (join1: ('t1 list * 't2 list) -> 'r1 list) (join2: ('r1 list * 't3 list) -> 'r2 list) (l1: 't1 list, l2: 't2 list, l3: 't3 list) = 
        (join1 (l1, l2), l3) |> join2
                            
    let combineJoins3 (l1: 't1 list, l2: 't2 list, l3: 't3 list, l4: 't4 list) 
                      (join1: ('t1 list * 't2 list) -> 'r1 list) 
                      (join2: ('r1 list * 't3 list) -> 'r2 list) 
                      (join3: ('r2 list * 't4 list) -> 'r3 list) = 
        (combineJoins join1 join2 (l1, l2, l3), l4) |> join3
                            
    /// <summary>
    /// Transforms a value wrapped in Async object using a given function.
    /// </summary>
    let mapAsync (f: 't -> 'v) (v: Async<'t>) =
        async {
            let! v1 = v
            return f(v1)
        }


    let mapAsyncList (f: 't -> 'v) (x: Async<List<'t>>)  =
        mapAsync (List.map f) x

    let mapAsyncDb (f: 't -> 't1) (v: AsyncDbAction<'t>)(*: AsyncDbAction<'t1>*) = asyncdb {
        let! v'= v
        return f v'
    }

    let mapAsyncDbList (f: 't -> 'v) (x: AsyncDbAction<List<'t>>)  =
        mapAsyncDb (List.map f) x


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
