# Non-standard parameter conversions

There are also built-in parameter conversions:

### Simple collection parameters
`listParamBuilder` modifies sql statement by replacing one parameter with many parameters representing elements of the 
  list. E.g. the query:
  ```sql 
  select * from post where id in (@postIds)
  ``` 
  executed with ```[1, 2, 3]``` as a parameter, would be transformed to:
   ```sql 
   select * from post where id in (@postIds1, @postIds2, @postIds3)
   ```
  To use this this feature, the configuration must be modified as follows:
  ```fsharp
  let generatorConfig = 
      createDefaultConfig createConnection
      |> useCollectionParameters
  ```
### Inlining parameter values
`listDirectParamBuilder` modifies sql statement by replacing parameter with list of comma-separated values. E.g.
  ```sql 
  select * from post where id in (@postIds)
  ```  
  executed with ```[1, 2, 3]``` as a parameter, is transformed to 
  ```sql 
  select * from post where id in (1, 2, 3)
  ```  
  Queries with literals in an `in` clause are very efficient, but they shouldn't be used with collections of strings.
  
  To leverage it, the configuration must be modified with `intCollectionsAsLiterals` function:
```fsharp
let generatorConfig = 
    createDefaultConfig createConnection
    |> intCollectionsAsLiterals
```
### TVP parameters
`tableValueParamBuilder` allows to use MS SQL table valued parameters
 To use it, either MsSql version of createDefaultConfig must be used, or standard configuration modified as follows:
```fsharp
let generatorConfig = 
    createDefaultConfig createConnection
    |> useTableValuedParameters
```
### Array parameters
`arrayParamBuilder` for PostgreSQ: and Oracle, allows to use array parameters 
The feature is available when Oracle or PostgreSQL specific `createDefaultConfig` must be used, or standard configuration modified as follows:
```fsharp
let generatorConfig = 
    createDefaultConfig createConnection
    |> useArrayParameters
```

In general, to leverage non-standard conversion, the configuration of code generator must be changed by chaining suitable
parameter builders.
Assume, that we'd like to use `listDirectParamBuilder` for `int` parameters,
`listParamBuilder` for all other basic types. We have to chain them using `<+>` operator adding also default parameter builder:
```fsharp
let generatorConfig = 
    let defaultConfig = createDefaultConfig createConnection
    { defaultConfig with
        paramBuilder = 
            (listDirectParamBuilder ((=) typeof<int>) string) <+> 
            (listParamBuilder isSimpleType "@") <+> 
            defaultConfig.paramBuilder
    }
```

### SQLite datetime conversions
There is no date or datetime data type in SQLite. To store date and time values, one of INTEGER, TEXT or REAL data types should be used. To allow user work with datetime values comfortably, SQlFun provides conversions for string:

```fsharp
    let generatorConfig = 
        createDefaultConfig createConnection
        |> representDatesAsStrings
```

and for int data type:

```fsharp
    let generatorConfig = 
        createDefaultConfig createConnection
        |> representDatesAsInts
```
