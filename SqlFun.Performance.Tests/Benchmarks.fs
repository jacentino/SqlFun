namespace SqlFun.Performance.Tests

open SqlFun
open Dapper
open System.Linq
open Data
open SqlFunSuite
open BenchmarkDotNet.Attributes


type ReadMany() =

    let _ = SqlFunSuite.getPostsByBlogId

    [<Benchmark>]
    member __.SqlFun() = SqlFunSuite.readMany()

    [<Benchmark>]
    member __.Dapper() = DapperSuite.readMany()

    [<Benchmark>]
    member __.HandCoded() = HandCodedSuite.readMany()

    [<Benchmark>]
    member __.FSharpDataSqlClient() = FSharpDataSqlClientSuite.readMany()


type ReadOneManyTimes() = 

    let _ = SqlFunSuite.getPostById

    [<Benchmark>]
    member __.SqlFun() = SqlFunSuite.readOneManyTimes()

    [<Benchmark>]
    member __.Dapper() = DapperSuite.readOneManyTimes()

    [<Benchmark>]
    member __.HandCoded() = HandCodedSuite.readOneManyTimes()

    [<Benchmark>]
    member __.FSharpDataSqlClient() = FSharpDataSqlClientSuite.readOneManyTimes()


type ReadManyAsync() = 

    // Warm-up: code generation
    do SqlFunSuite.getPostsByBlogId |> ignore
    do DapperSuite.Async.readMany() |> Async.RunSynchronously


    [<Benchmark>]
    member __.SqlFun() = SqlFunSuite.Async.readMany() |> Async.RunSynchronously

    [<Benchmark>]
    member __.Dapper() = DapperSuite.Async.readMany() |> Async.RunSynchronously

    [<Benchmark>]
    member __.HandCoded() = HandCodedSuite.Async.readMany() |> Async.RunSynchronously

    [<Benchmark>]
    member __.FSharpDataSqlClient() = FSharpDataSqlClientSuite.Async.readMany() |> Async.RunSynchronously
    
type ReadOneManyTimesAsync() = 

    // Warm-up: code generation
    do SqlFunSuite.getPostById |> ignore
    do DapperSuite.Async.readOneManyTimes() |> Async.RunSynchronously

    [<Benchmark>]
    member __.SqlFun() = SqlFunSuite.Async.readOneManyTimes() |> Async.RunSynchronously

    [<Benchmark>]
    member __.Dapper() = DapperSuite.Async.readOneManyTimes() |> Async.RunSynchronously

    [<Benchmark>]
    member __.HandCoded() = HandCodedSuite.Async.readOneManyTimes() |> Async.RunSynchronously

    [<Benchmark>]
    member __.FSharpDataSqlClient() = FSharpDataSqlClientSuite.Async.readOneManyTimes() |> Async.RunSynchronously



