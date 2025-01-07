module Ictf.Api.Repos.SqlExtensions

open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Npgsql.FSharp

module Sql =
    let build query parameters conn =
        conn |> Sql.existingConnection |> Sql.query query |> Sql.parameters parameters
        
    let buildNoParams query conn =
        conn |> Sql.existingConnection |> Sql.query query

    let tryExecuteRow read props =
        try
            props
            |> Sql.execute read
            |> function
                | [] -> Ok None
                | (fst :: rest) -> Ok <| Some fst
        with ex ->
            Error ex

    let tryExecuteRowAsync read props =
        try
            task {
                let! result = Sql.executeAsync read props

                return
                    match result with
                    | [] -> Ok None
                    | (fst :: _) -> Ok <| Some fst
            }
        with ex ->
            Task.FromResult <| Error ex

    let toOptionWithLog (result: Result<'a option, exn>) (method: string) (log: ILogger) =
        match result with
        | Ok opt -> opt
        | Error err ->
            log.LogError(err, "Error in {Method}", method)
            None
