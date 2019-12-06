# PostgreSQL Bulk Copy

The PostgreSQL provider has excellent mechanism for bulk operations, although it's rather low-level.
SqlFun defines a wrepper around it, making its usage more comfortable. The idea is to allow to use collections of records as an input data:

```fsharp
let posts: Post list = ...
BulkCopy.WriteToServer posts|> run
```

Input data can contain enums, options, tuples and subrecords.
