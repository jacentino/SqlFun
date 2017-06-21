namespace SqlFun

open System
open System.Linq.Expressions
open System.Data

[<AttributeUsage(AttributeTargets.Property)>]
type PrefixedAttribute(name: string) = 
    inherit Attribute()
    new() = PrefixedAttribute("")
    member this.Name = name

[<AttributeUsage(AttributeTargets.Field)>]
type EnumValueAttribute(value: obj) =
    inherit Attribute()
    member this.Value = value

type ParamBuilder = string -> string -> Expression -> string list -> (string * Expression * (obj -> IDbCommand -> int) * obj) list

[<AutoOpen>]
module Pervasives =

    /// <summary>
    /// The parameter builder function passing control to internal parameter builder.
    /// </summary>
    /// <param name="defaultPB">
    /// The internal parameter builder.
    /// </param>
    let defaultParamBuilder (defaultPB: ParamBuilder): ParamBuilder = 
        defaultPB

    /// <summary>
    /// Does nothing. Use it for writing attribute forcing module initialization.
    /// </summary>
    let test () = ()
        


    

