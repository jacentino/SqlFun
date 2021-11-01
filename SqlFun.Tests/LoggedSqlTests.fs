namespace SqlFun.Tests

open System
open NUnit.Framework
open SqlFun
open Data
open Common

module InvalidQueries = 

    let compilationErrors = ref []

    let sql commandText = Diagnostics.logged compilationErrors sql commandText

    let incorrect: int -> AsyncDb<Blog> = 
        sql "some totally incorrect sql with @id parameter"

    let getBlogInvalidType: int -> AsyncDb<BlogWithInvalidType> = 
        sql "select id, name, title, description, owner from Blog where id = @id"

[<TestFixture>]
type LoggedSqlTests() = 

    [<Test>]
    member this.``Invalid queries doesn't break whole module when logging is enabled``() = 
        Assert.DoesNotThrow(fun () -> InvalidQueries.incorrect |> ignore)

    [<Test>]
    member this.``Invalid queries are logged with source line information``() = 
        let report = Diagnostics.buildReport InvalidQueries.compilationErrors
        Assert.True(report.Contains("\\SqlFun\\SqlFun.Tests\\LoggedSqlTests.fs, line: 15"))
        Assert.True(report.Contains("\\SqlFun\\SqlFun.Tests\\LoggedSqlTests.fs, line: 18"))

    