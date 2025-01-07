module Ictf.Api.Http.Handlers

open Giraffe

let TODO: HttpHandler = text "TODO"

let sendDomainError name msg : HttpHandler =
    setHttpHeader "X-Error" name >=> json {| Message = msg |}

// These two are only here to swap the orgder of the arguments around, so I can use a |>
let created next ctx handler = Successful.created handler next ctx

let badRequest next ctx handler =
    RequestErrors.badRequest handler next ctx
