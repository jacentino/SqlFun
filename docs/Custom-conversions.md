# Custom conversions

SqlFun allows to intercept parameter creation. That is the moment conversions are applied.  
We can either convert particuler value to another type (i.e. generate code for it) 
and then pass it to default parameter builder, or even create command parameter and add it to a command.
E.g. parameter builder converting list of int values to comma separated string could look like this:

```fsharp
let listToStringParamBuilder defaultPB prefix name (expr: Expression) names = 
    match expr.Type with 
    | CollectionOf itemType when itemType = typeof<int> ->
        let convert (list: int list) = list |> List.map string |> String.concat ","
        let convertExpr = Expression.Invoke(Expression.Constant(Func<int list, string>(convert)), expr) 
        defaultPB prefix name (convertExpr :> Expression) names
    | _ ->
        defaultPB prefix name expr names
```
More complex case is when a value can not be transform to one parameter. For example, list of values can be transformed to many command parameters:
```fsharp 
    let listParamBuilder isAllowed defaultPB prefix name (expr: Expression) names = 
        match expr.Type with 
        | CollectionOf itemType when isAllowed itemType ->
            [
                prefix + name,
                expr,
                fun (value: obj) (command: IDbCommand) ->
                    let first = command.Parameters.Count
                    for v in value :?> System.Collections.IEnumerable do
                        let param = command.CreateParameter()
                        param.ParameterName <- "@" + name + string(command.Parameters.Count - first)
                        param.Value <- v
                        command.Parameters.Add(param) |> ignore
                    let names = [| for i in 0..command.Parameters.Count - first - 1 do
                                      yield "@" + name + string(i) 
                                |] |> String.concat ","
                    let newCommandText = command.CommandText.Replace("@" + name, names)
                    command.CommandText <- newCommandText
                    command.Parameters.Count
                ,
                [ getFakeValue itemType ] :> obj
            ]       
        | _ ->
            defaultPB prefix name expr names
```
Of course, when more parameters are needed, the original SQL command must be modified, which is implemented in the
example above, in lines 14-16.

To make everything work, instead of default paramBuilder, the composition with custom one should be used in configuration code:
```fsharp 
    let createConnection () = new SqlConnection(connectionString)

    let generatorConfig = 
        let defaultConfig = GeneratorConfig.Default createConnection
        { defaultConfig with paramBuilder = myParamBuilder <+> defaultConfig.paramBuilder }
    
    let sql commandText = sql generatorConfig commandText

    let proc name = proc generatorConfig name

    let buildQuery ctx = FinalQueryPart(ctx, generatorConfig, string)
```
