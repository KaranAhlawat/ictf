module Ictf.Api.Http.UserRoutes

open System
open Giraffe
open Ictf.Api.Domain.Types
open Ictf.Api.Http.Dto
open Ictf.Api.Repos.UserRepo

let registerUserHandler (dto: RegisterUserRequest) : HttpHandler =
    fun next ctx ->
        task {
            let userRepo = ctx.GetService<IUserRepo>()

            let! result =
                userRepo.InsertUser {
                    Id = -1L
                    ProviderId = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString()
                    Username = dto.Username
                    UserEmail = Some dto.Email
                    Provider = Form
                }

            return!
                match result with
                | Ok() -> (json {| |}) |> Handlers.created next ctx
                | Error err ->
                    match err with
                    | EmailRegistered ->
                        Handlers.sendDomainError (EmailRegistered.ToString()) "Email already registered"
                        |> Handlers.badRequest next ctx
        }

let routes: HttpHandler =
    choose [
        POST >=> route "/register" >=> bindJson<RegisterUserRequest> registerUserHandler
        GET >=> route "/info" >=> Handlers.TODO
    ]
