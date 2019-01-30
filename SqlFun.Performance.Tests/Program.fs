open SqlFun.Performance.Tests
open BenchmarkDotNet.Running


// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

[<EntryPoint>]
let main argv = 

    //let summary1 = BenchmarkRunner.Run<ReadMany>()
    //let summary2 = BenchmarkRunner.Run<ReadOneManyTimes>()

    //let summary3 = BenchmarkRunner.Run<ReadManyAsync>()
    let summary4 = BenchmarkRunner.Run<ReadOneManyTimesAsync>()

    0 // return an integer exit code
