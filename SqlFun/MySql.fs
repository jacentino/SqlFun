namespace SqlFun

module MySql = 
    
    /// <summary>
    /// Adds settings needed by generator to work with MySql databases.
    /// </summary>
    /// <remarks>
    /// * turned off return parameter adding
    /// * turned off execution of schema only commands 
    ///   for queries, that doesn't create results
    /// <param name="connectionBuilder">
    /// Function creating database connection.
    /// </param>
    let createDefaultConfig connectionBuilder = 
        let defaultConfig = SqlFun.GeneratorConfig.createDefaultConfig connectionBuilder
        { defaultConfig with 
            makeDiagnosticCalls = false 
            addReturnParameter = false
        }

