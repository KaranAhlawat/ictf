module Ictf.Api.Repos.ConnFactory

open Npgsql

type NpgsqlConnFactory(connectionString: string) =
    member _.CreateConnectionAsync =
        task {
            let conn = new NpgsqlConnection(connectionString)
            do! conn.OpenAsync()
            return conn
        }
