## Enum support

By default enums are represented as integer values.

```fsharp
type PostStatus = 
     | New = 0
     | Published = 1
     | Archived = 2
```

If you prefer more descriptive values in a database, you can override them using `EnumValueAttribute`:

```fsharp
type PostStatus = 
     | [<EnumValue("N")>] New = 0
     | [<EnumValue("P")>] Published = 1
     | [<EnumValue("A")>] Archived = 2
```
Arguments of `EnumValueAttribute` can be any values, that can be written do a database (integers, strings, etc.).
