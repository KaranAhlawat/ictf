module Ictf.Api.Domain.Types

type Provider =
    | Form
    | Google
    | Github

[<CLIMutable>]
type User = {
    Id: int64
    ProviderId: string
    Username: string
    UserEmail: string option
    Provider: Provider
}

type UserErrors = | EmailRegistered

type OidcUser = {
    sub: string
    name: string
    email: string
}
