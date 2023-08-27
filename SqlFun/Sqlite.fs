namespace SqlFun

module Sqlite = 

    open System
    open SqlFun
    open SqlFun.GeneratorConfig

    /// <summary>
    /// Parameter builder converting dates to UNIX time integers.
    /// </summary>
    /// <param name="defaultPB">
    /// Next parameter builder in chain.
    /// </param>
    let dateTimeToIntParamBuilder defaultPB = 
        simpleConversionParamBuilder (fun (v: DateTime) -> v.Subtract(DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds |> int64) defaultPB

    /// <summary>
    /// Parameter builder converting dates to strings in yyyy-MM-dd HH:mm:ss.ffffff format.
    /// </summary>
    /// <param name="defaultPB">
    /// Next parameter builder in chain.
    /// </param>
    let dateTimeToStrParamBuilder defaultPB = 
        simpleConversionParamBuilder (fun (v: DateTime) -> v.ToString("yyyy-MM-dd HH:mm:ss.ffffff")) defaultPB

    /// <summary>
    /// Row builder converting int representing UNIX time to datetime.
    /// </summary>
    /// <param name="nextRB">
    /// The next row builder in chain.
    /// </param>
    let dateTimeFromIntRowBuilder nextRB = 
        simpleConversionRowBuilder (fun (ts: int64) -> DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(float ts)) nextRB

    /// <summary>
    /// Row builder converting string in yyyy-MM-dd HH:mm:ss.ffffff format to datetime.
    /// </summary>
    /// <param name="nextRB">
    /// The next row builder in chain.
    /// </param>
    let dateTimeFromStrRowBuilder nextRB = 
        simpleConversionRowBuilder (fun str -> DateTime.ParseExact(str, "yyyy-MM-dd HH:mm:ss.ffffff", null)) nextRB

    /// <summary>
    /// Adds dateime support based on integer representation (UNIX time).
    /// </summary>
    /// <param name="config">
    /// The initial config.
    /// </param>
    let representDatesAsInts (config: GeneratorConfig) = 
        { config with
            paramBuilder = dateTimeToIntParamBuilder <+> config.paramBuilder
            rowBuilder = dateTimeFromIntRowBuilder <+> config.rowBuilder
        }
         
    /// <summary>
    /// Adds dateime support based on string representation (yyyy-MM-dd hh:mm:ss.ffffff format).
    /// </summary>
    /// <param name="config">
    /// The initial config.
    /// </param>
    let representDatesAsStrings (config: GeneratorConfig) = 
        { config with
            paramBuilder = dateTimeToStrParamBuilder <+> config.paramBuilder
            rowBuilder = dateTimeFromStrRowBuilder <+> config.rowBuilder
        }

    /// <summary>
    /// Adds Miscrosoft.Data.Sqlite -specific config entries.
    /// </summary>
    /// <param name="config">
    /// The initial config.
    /// </param>
    let useMicrosoftDataSqlite (config: GeneratorConfig) = 
        { config with makeDiagnosticCalls = false }
        