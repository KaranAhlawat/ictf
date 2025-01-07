module Ictf.Api.Repos.UserRepo

open System.Threading.Tasks
open Ictf.Api.Domain
open Ictf.Api.Domain.Types
open Ictf.Api.Repos.ConnFactory
open Ictf.Api.Repos.SqlExtensions
open Microsoft.Extensions.Logging
open Microsoft.FSharp.Control
open Npgsql
open Npgsql.FSharp

type IUserRepo =
    abstract member GetUserById: int64 -> Task<User option>
    abstract member GetUserByProvider: string -> Provider -> Task<User option>
    abstract member GetUserByUsernameOrEmail: string -> Task<User option>
    abstract member InsertUser: User -> Task<Result<unit, UserErrors>>

module Reader =
    let toUser (read: RowReader) = {
        Id = read.int64 "id"
        ProviderId = read.text "provider_id"
        Username = read.text "username"
        UserEmail = read.textOrNone "user_email"
        Provider = read.string "provider" |> Provider.ofString |> Option.get
    }

module Queries =
    [<Literal>]
    let selectById =
        """
    SELECT id, provider_id, username, user_email, provider::text
    FROM users
    WHERE id = @Id
    LIMIT 1
    """

    [<Literal>]
    let selectByProvider =
        """
    SELECT id, provider_id, username, user_email, provider::text
    FROM users
    WHERE provider_id = @ProviderId AND provider = @Provider :: user_origin
    LIMIT 1
    """

    [<Literal>]
    let selectByUsernameOrEmail =
        """
    SELECT id, provider_id, username, user_email, provider::text
    FROM users
    WHERE (username = @UsernameOrEmail OR user_email = @UsernameOrEmail)
    LIMIT 1
    """

    [<Literal>]
    let insertUser =
        """
    INSERT INTO users (provider_id, username, user_email, provider)
    VALUES (@ProviderId, @Username, @UserEmail, @Provider::user_origin)
    """

type UserRepo(cf: NpgsqlConnFactory, log: ILogger<UserRepo>) =

    interface IUserRepo with
        member _.GetUserById id =
            task {
                use! conn = cf.CreateConnectionAsync

                let! user =
                    conn
                    |> Sql.build Queries.selectById [ "Id", Sql.int64 id ]
                    |> Sql.tryExecuteRowAsync Reader.toUser

                return Sql.toOptionWithLog user "GetUserById" log
            }

        member _.GetUserByProvider pid provider =
            task {
                use! conn = cf.CreateConnectionAsync

                let! user =
                    conn
                    |> Sql.build Queries.selectByProvider [
                        "ProviderId", Sql.string pid
                        "Provider", Sql.string <| provider.ToString().ToLower()
                    ]
                    |> Sql.tryExecuteRowAsync Reader.toUser

                return Sql.toOptionWithLog user "GetUserByProvider" log
            }

        member _.GetUserByUsernameOrEmail usernameOrEmail =
            task {
                use! conn = cf.CreateConnectionAsync

                let! user =
                    conn
                    |> Sql.build Queries.selectByUsernameOrEmail [ "UsernameOrEmail", Sql.text usernameOrEmail ]
                    |> Sql.tryExecuteRowAsync Reader.toUser

                return Sql.toOptionWithLog user "GetUserByUsernameOrEmail" log
            }

        member _.InsertUser user =
            task {
                try
                    use! conn = cf.CreateConnectionAsync

                    let! _ =
                        conn
                        |> Sql.build Queries.insertUser [
                            "ProviderId", Sql.string user.ProviderId
                            "Username", Sql.text user.Username
                            "UserEmail", Sql.textOrNone user.UserEmail
                            "Provider", Sql.string (user.Provider.ToString().ToLower())
                        ]
                        |> Sql.executeNonQueryAsync

                    return Ok()
                with :? PostgresException as ex when ex.SqlState = PostgresErrorCodes.UniqueViolation ->
                    log.LogError(
                        "Error while inserting User: {User}, Constraint: {Constraint}, Error: {Msg}",
                        user,
                        ex.ColumnName,
                        ex.ConstraintName,
                        ex.MessageText
                    )

                    let err =
                        match ex.ConstraintName with
                        | name when name.Contains("email") -> Error EmailRegistered
                        | _ -> raise ex

                    return err
            }
