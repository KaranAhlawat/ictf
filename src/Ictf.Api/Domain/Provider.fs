module Ictf.Api.Domain.Provider

open Ictf.Api.Domain.Types

let ofString =
        function
        | "form" -> Some Form
        | "github" -> Some Github
        | "google" -> Some Google
        | _ -> None
