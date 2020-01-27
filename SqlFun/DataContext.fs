namespace SqlFun

open System
open System.Data

/// <summary>
/// Manages open connection.
/// </summary>
type DataContext = 
    {
        connection: IDbConnection
        transaction: IDbTransaction option
    }
    interface IDisposable with
        member this.Dispose() =
            match this.transaction with
            | Some t -> t.Dispose()
            | None -> this.connection.Dispose()
            

    /// <summary>
    /// Creates a new DataContext object.
    /// </summary>
    /// <param name="connection">
    /// The database connection.
    /// </param>
    static member create connection = { connection = connection; transaction = None }

    /// <summary>
    /// Starts a transaction on the connection. 
    /// Returns new data context object with a transaction field assigned to current transaction.
    /// </summary>
    /// <param name="isolationLevel">
    /// Transaction isolation level.
    /// </param>
    /// <param name="dc">
    /// The data context object.
    /// </param>
    static member beginTransaction isolationLevel dc = 
        match dc.transaction with
        | Some _ -> failwith "Connection has already active transaction."
        | None -> { dc with transaction = Some (match isolationLevel with
                                                | Some l -> dc.connection.BeginTransaction l
                                                | None -> dc.connection.BeginTransaction()) }

    /// <summary>
    /// Commits a transaction.
    /// </summary>
    /// <param name="dc">
    /// The data context object.
    /// </param>
    static member commit dc =
        match dc.transaction with
        | Some t -> t.Commit()
        | None -> failwith "Connection has no active transaction."

    /// <summary>
    /// Rollbacks current transaction.
    /// </summary>
    /// <param name="dc">
    /// The data context object.
    /// </param>
    static member rollback dc =
        match dc.transaction with
        | Some t -> t.Rollback()
        | None -> failwith "Connection has no active transaction."

    /// <summary>
    /// Determines if the data context is in transaction.
    /// </summary>
    /// <param name="dc">
    /// The data context object.
    /// </param>
    static member isInTransaction dc =
        dc.transaction |> Option.isSome



