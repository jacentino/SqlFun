namespace SqlFun

open System

[<AttributeUsage(AttributeTargets.Property)>]
type PrefixedAttribute(name: string) = 
    inherit Attribute()
    new() = PrefixedAttribute("")
    member this.Name = name

[<AttributeUsage(AttributeTargets.Field)>]
type EnumValueAttribute(value: obj) =
    inherit Attribute()
    member this.Value = value

[<AttributeUsage(AttributeTargets.Property)>]
type IdAttribute() =
    inherit Attribute()

[<AttributeUsage(AttributeTargets.Property)>]
type ParentIdAttribute() =
    inherit Attribute()




    

