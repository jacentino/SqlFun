namespace SqlFun

open System.Data

/// <summary>
/// Values and functions allowing to customize generation of query execution functions.
/// </summary>
type GeneratorConfig =
    {
        /// The function providing a database connection used in generation.
        createConnection: unit -> IDbConnection 
        /// The function creating commands.
        createCommand: IDbConnection -> IDbCommand
        /// The command timeout.
        commandTimeout: int option
        /// Function searching for parameter names in a command text.
        paramNameFinder: string -> string list
        /// Function searching for parameter names and directions in an information schema.
        procParamFinder: string -> (string * bool * obj) list
        /// Function generating code creating query parameters from function parameters.
        paramBuilder: ParamBuilder -> ParamBuilder
        /// Function generating code creating typed result from data reader.
        rowBuilder: RowBuilder -> RowBuilder
        /// Determines, whether queries, that don't return results, should be executed
        /// with CommandBehavior.SchemaOnly for diagnostic purposes.
        makeDiagnosticCalls: bool
        /// Determines, whether to add return value parameter to stored procedure commands.
        addReturnParameter: bool
    }


module GeneratorConfig =
    open System
    open System.Linq.Expressions
    open SqlFun.Types
    open SqlFun.ParamBuilder

   /// <summary>
    /// Parameter builder transforming list of values (intentionally of simple type)
    /// by adding SQL parameters for all elements.
    /// </summary>
    /// <param name="isAllowed">
    /// Function determining if list elements have valid type.
    /// </param>
    /// <param name="toString">
    /// Converts element to string representing SQL literal of element.
    /// </param>
    /// <param name="defaultPB">
    /// Next item in parameter building cycle.
    /// </param>
    /// <param name="prefix">
    /// Parameter name prefix.
    /// </param>
    /// <param name="name">
    /// Parameter name.
    /// </param>
    /// <param name="expr">
    /// Expression calculating parameter value from function parameter.
    /// </param>
    /// <param name="names">
    /// List of available parameter names extracted from SQL command.
    /// </param>
    let listParamBuilder isAllowed paramPrefix defaultPB prefix name (expr: Expression) names = 
        match expr.Type with 
        | CollectionOf itemType when isAllowed itemType ->
            [
                prefix + name,
                expr,
                fun (value: obj) (command: IDbCommand) ->
                    let first = command.Parameters.Count
                    for v in value :?> System.Collections.IEnumerable do
                        let param = command.CreateParameter()
                        param.ParameterName <- name + string(command.Parameters.Count - first)
                        param.Value <- v
                        command.Parameters.Add(param) |> ignore
                    let names = [| for i in 0..command.Parameters.Count - first - 1 -> paramPrefix + name + string(i) |] 
                    let newCommandText = command.CommandText.Replace(paramPrefix + name, names |> String.concat ",")
                    command.CommandText <- newCommandText
                    command.Parameters.Count
                ,
                [ getFakeValue itemType ] :> obj
            ]       
        | _ ->
            defaultPB prefix name expr names

    /// <summary>
    /// Parameter builder handling list of values (intentionally of simple type)
    /// by injecting them directly into SQL command.
    /// </summary>
    /// <param name="isAllowed">
    /// Function determining if list elements have valid type.
    /// </param>
    /// <param name="toString">
    /// Converts element to string representing SQL literal of element.
    /// </param>
    /// <param name="defaultPB">
    /// Next item in parameter building cycle.
    /// </param>
    /// <param name="prefix">
    /// Parameter name prefix.
    /// </param>
    /// <param name="name">
    /// Parameter name.
    /// </param>
    /// <param name="expr">
    /// Expression calculating parameter value from function parameter.
    /// </param>
    /// <param name="names">
    /// List of available parameter names extracted from SQL command.
    /// </param>
    let listDirectParamBuilder isAllowed toString defaultPB prefix name (expr: Expression) names = 
        match expr.Type with 
        | CollectionOf itemType when isAllowed itemType ->
            [
                prefix + name,
                expr,
                fun (value: obj) (command: IDbCommand) ->
                    let values = [| for v in value :?> System.Collections.IEnumerable do yield toString v |] 
                    let newCommandText = command.CommandText.Replace("@" + name, values |> String.concat ",")
                    command.CommandText <- newCommandText
                    command.Parameters.Count
                ,
                [ getFakeValue itemType ] :> obj
            ]       
        | _ ->
            defaultPB prefix name expr names

    /// <summary>
    /// Parameter builder performing simple type-to-type conversion.
    /// </summary>
    /// <param name="convert">
    /// Function converting between types.
    /// </param>
    /// <param name="defaultPB">
    /// Next item in parameter building cycle.
    /// </param>
    /// <param name="prefix">
    /// Parameter name prefix.
    /// </param>
    /// <param name="name">
    /// Parameter name.
    /// </param>
    /// <param name="expr">
    /// Expression calculating parameter value from function parameter.
    /// </param>
    /// <param name="names">
    /// List of available parameter names extracted from SQL command.
    /// </param>
    let simpleConversionParamBuilder (convert: 't -> 'c) defaultPB prefix name (expr: Expression) names =
        if expr.Type = typeof<'t> then
            let convertExpr = Expression.Invoke(Expression.Constant(Func<'t, 'c>(convert)), expr) 
            defaultPB prefix name (convertExpr :> Expression) names
        elif expr.Type = typeof<'t option> then
            let convertExpr = Expression.Invoke(Expression.Constant(Func<'t option, 'c option>(Option.map convert)), expr) 
            defaultPB prefix name (convertExpr :> Expression) names
        else
            defaultPB prefix name expr names        

    /// <summary>
    /// Row builder performing simple type-to-type conversion.
    /// </summary>
    /// <param name="convert">
    /// Function converting between types.
    /// </param>
    /// <param name="nextRB">
    /// Next item in row building cycle.
    /// </param>
    /// <param name="reader">
    /// The data reader.
    /// </param>
    /// <param name="metadata">
    /// The result set field information.
    /// </param>
    /// <param name="prefix">
    /// The current prefix of a field.
    /// </param>
    /// <param name="fieldName">
    /// The current field name.
    /// </param>
    /// <param name="returnType">
    /// The data type of current field.
    /// </param>
    let simpleConversionRowBuilder (convert: 't -> 'u) (nextRB: RowBuilder) reader metadata (prefix: string) (fieldName: string) returnType = 
        if returnType = typeof<'u> then
            let fromDb = nextRB reader metadata prefix fieldName typeof<'t>
            let convertExpr = Expression.Constant(Func<'t, 'u>(convert))
            Expression.Invoke(convertExpr, fromDb) :> Expression
        elif returnType = typeof<'u option> then
            let fromDb = nextRB reader metadata prefix fieldName typeof<'t option>
            let convertExpr = Expression.Constant(Func<'t option, 'u option>(Option.map convert))
            Expression.Invoke(convertExpr, fromDb) :> Expression
        else
            nextRB reader metadata prefix fieldName returnType


    /// <summary>
    /// Composes two builders.
    /// </summary>
    /// <param name="pb1">
    /// First builder.
    /// </param>
    /// <param name="pb2">
    /// Second builder.
    /// </param>
    /// <param name="next">
    /// Next item in building cycle.
    /// </param>
    let (<+>) (b1: 'b -> 'b) (b2: 'b-> 'b) (next: 'b): 'b = 
        b1 <| b2 next

    /// <summary>
    /// Provides default configuration.
    /// </summary>
    /// <param name="connectionBuilder">
    /// Function creating database connection.
    /// </param>
    let createDefaultConfig (connectionBuilder: unit -> #IDbConnection) =
        let createConnection = (connectionBuilder >> unbox<IDbConnection>)
        let createCommand = fun (c: IDbConnection) -> c.CreateCommand()
        {
            createConnection = createConnection
            createCommand = createCommand
            commandTimeout = None
            paramNameFinder = ParamBuilder.extractParameterNames "@"
            procParamFinder = ParamBuilder.extractProcParamNames createConnection createCommand
            paramBuilder = ParamBuilder.getParamExpressions
            rowBuilder = ResultBuilder.getRowBuilderExpression
            makeDiagnosticCalls = true
            addReturnParameter = true
        }

    /// <summary>
    /// Specifies command timeout.
    /// </summary>
    /// <param name="tm">The timeout value.</param>
    /// <param name="config">The initial config.</param>
    let addCommandTimeout tm config = 
        { config with commandTimeout = Some tm }

    /// <summary>
    /// Adds support for collections of basic types as query parameters.
    /// Subsequent collection items are injected as comma separated parameters in a command text.
    /// </summary>
    /// <param name="config">
    /// The initial config.
    /// </param>
    let useCollectionParameters config = 
        { config with 
            paramBuilder = (listParamBuilder Types.isSimpleType "@") <+> config.paramBuilder
        }

    /// <summary>
    /// Adds support for collections of int values as query parameters.
    /// Subsequent collection items are injected as literal values in a command text.
    /// </summary>
    /// <param name="config">
    /// The initial config.
    /// </param>
    let intCollectionsAsLiterals config = 
        { config with 
            paramBuilder = (listDirectParamBuilder ((=) typeof<int>) string) <+> config.paramBuilder
        }
        
    /// <summary>
    /// Adds simple type-to-type conversion.
    /// </summary>
    /// <param name="convert">
    /// The conversion function.
    /// </param>
    /// <param name="config">
    /// The initial config.
    /// </param>
    let addParameterConversion (convert: 't -> 'c) config = 
        { config with 
            paramBuilder = (simpleConversionParamBuilder convert <+> config.paramBuilder)
        }        
        
    /// <summary>
    /// Adds simple type-to-type conversion.
    /// </summary>
    /// <param name="convert">
    /// The conversion function.
    /// </param>
    /// <param name="config">
    /// The initial config.
    /// </param>
    let addColumnConversion (convert: 't -> 'c) config = 
        { config with 
            rowBuilder = (simpleConversionRowBuilder convert <+> config.rowBuilder)
        }
        
    
