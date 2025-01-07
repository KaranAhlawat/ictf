open System
open System.Reflection
open DbUp

let main =
    let connString =
        "Server=localhost;Port=5432;Database=ictdb;User Id=ict;Password=ictpwd;"

    let upgrader =
        DeployChanges.To
            .PostgresqlDatabase(connString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .LogToConsole()
            .Build()

    let result = upgrader.PerformUpgrade()

    if result.Successful then
        Console.ForegroundColor <- ConsoleColor.Red
        printfn $"{result.Error}"
        Console.ResetColor()
    else
        Console.ForegroundColor <- ConsoleColor.Green
        printfn "Success!"
        Console.ResetColor()
