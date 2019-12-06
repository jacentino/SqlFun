The first step is to define parameterless function creating a database connection:
```fsharp 
let createConnection () = new SqlConnection(connectionString)
let generatorConfig = createDefaultConfig createConnection
```
Connections are used in two contexts - when query code is generated, and when query functions are executed.

In the first case the connection creation function is wired-up by defining some functions:
```fsharp 
let sql commandText = sql generatorConfig commandText
```
and, if explicit timeouts must be specified:
```fsharp 
let sqlWithTimeout timeout commandText = 
    sql (generatorConfig |> addCommandTimeout timeout) commandText
```
that generate code executing inline sql, and:
```fsharp 
let proc name = 
    proc generatorConfig name
```
and 
```fsharp 
let procWithTimeout timeout name = 
    proc (generatorConfig |> addCommandTimeout timeout) name
```
generating code for stored procedure execution.  

The query execution configuration code depends on query mode (i.e. whether they are synchornous or asynchronous).

For synchronous execution the `run` function should be defined:
```fsharp 
let run f = DbAction.run createConnection f
```
and, additionally, when composite queries are used:
```fsharp 
let buildQuery ctx = FinalQueryPart(ctx, generatorConfig, cleanUpTemplate)
```
or with timeout:
```fsharp 
let buildQueryWithTimeout timeout ctx = 
    FinalQueryPart(
        ctx, 
        (generatorConfig |> addCommandTimeout timeout), 
        cleanUpTemplate)
```
For asynchronous execution these function should be defined as follows:
```fsharp 
let run f = AsyncDb.run createConnection f
```
and 
```fsharp 
let buildQuery ctx = async {
    return FinalQueryPart(ctx, generatorConfig, cleanUpTemplate)
}
```
or
```fsharp 
let buildQueryWithTimeout timeout ctx = async {
    return FinalQueryPart(
              ctx, 
              (generatorConfig |> addCommandTimeout timeout), 
              cleanUpTemplate)
}
```
