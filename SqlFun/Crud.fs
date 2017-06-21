namespace SqlFun

open System
open System.Reflection
open Microsoft.FSharp.Reflection

open Future
open Queries

/// <summary>
/// Generating insert/update/select/delete operations from a record structure.
/// </summary>
type Crud() =

    static member private isCollectionType(t: Type) =
        typeof<System.Collections.IEnumerable>.IsAssignableFrom(t) && t <> typeof<string>

    static member private getCurrentPrefix (field: PropertyInfo) = 
        Seq.append
            (field.GetCustomAttributes<PrefixedAttribute>()
            |> Seq.map (fun a -> if a.Name <> "" then a.Name else field.Name))
            [""]
        |> Seq.head

    static member getRecFields (recordType: Type) (prefix: string) = 
        FSharpType.GetRecordFields recordType
        |> Seq.collect (fun f -> if FSharpType.IsRecord f.PropertyType 
                                 then Crud.getRecFields f.PropertyType (prefix + (Crud.getCurrentPrefix f))
                                 else seq { yield f.Name, (prefix + f.Name), f.PropertyType }) 

    static member Insert<'t> (tableName: string, ignoredColNames: string list) =
        let fields = Crud.getRecFields typeof<'t> ""
                        |> Seq.filter (fun (_, alias, _) -> not (ignoredColNames |> List.exists ((=) alias)))
                        |> Seq.toList
        let cols = fields |> Seq.map (fun (name, _, _) -> name)
        let parameters = fields |> Seq.map (fun (_, alias, _) -> "@" + alias)
        sprintf "insert into %s (%s) values (%s)" tableName (String.concat ", " cols) (String.concat ", " parameters) 

    static member Insert<'t> (ignoredColNames: string list) =
        Crud.Insert<'t> (typeof<'t>.Name, ignoredColNames)

    static member Update<'t> (tableName: string, keyColNames: string list, ?ignoredColNames: string list) =        
        let ignored = match ignoredColNames with
                        | Some names -> names
                        | None -> []
        let f = Crud.getRecFields typeof<'t> ""
        let fields = Crud.getRecFields typeof<'t> ""
                        |> Seq.filter (fun (_, alias, _) -> not (keyColNames |> List.exists ((=) alias)))
                        |> Seq.filter (fun (_, alias, _) -> not (ignored |> List.exists ((=) alias)))
                        |> Seq.map (fun (name, alias, _) -> name + " = @" + alias)
                        |> Seq.toList
        let parameters = keyColNames |> List.map (fun name -> name + " = @" + name)
        sprintf "update %s set %s where %s" tableName (String.concat ", " fields) (String.concat " and " parameters)

    static member Update<'t> (keyColNames: string list, ?ignoredColNames: string list) =
        let ignored = match ignoredColNames with
                        | Some names -> names
                        | None -> []
        Crud.Update<'t> (typeof<'t>.Name, keyColNames, ignored)

    static member SelectByKey<'t> (tableName: string, keyColNames: string list, ?ignoredColNames: string list) =        
        let ignored = match ignoredColNames with
                        | Some names -> names
                        | None -> []
        let fields = Crud.getRecFields typeof<'t> ""
                        |> Seq.filter (fun (_, _, dataType) -> not (Crud.isCollectionType(dataType)))
                        |> Seq.filter (fun (_, alias, _) -> not (ignored |> List.exists ((=) alias)))
                        |> Seq.map (fun (name, alias, _) -> name + " as " + alias)
                        |> Seq.toList
        let parameters = keyColNames |> List.map (fun name -> name + " = @" + name)
        sprintf "select %s from %s where (%s)" (String.concat ", " fields) tableName (String.concat " and " parameters)
        
    static member SelectByKey<'t> (keyColNames: string list, ?ignoredColNames: string list) =
        let ignored = match ignoredColNames with
                                | Some names -> names
                                | None -> []
        Crud.SelectByKey<'t> (typeof<'t>.Name, keyColNames, ignored)
    
    static member DeleteByKey (tableName: string, keyColNames: string list) =
         let parameters = keyColNames |> List.map (fun name -> name + " = @" + name)
         sprintf "delete from %s where %s" tableName (String.concat " and " parameters)

    static member DeleteByKey<'t> (keyColNames: string list) =
        Crud.DeleteByKey (typeof<'t>.Name, keyColNames)
        