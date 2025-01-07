open Feliz
open Elmish
open Elmish.HMR

Fable.Core.JsInterop.importAll "./style.css"

type Model = { Count: int }

type Message =
    | Increment
    | Decrement

let init () = { Count = 0 }, Cmd.none

let update msg model =
    match msg with
    | Increment -> { model with Count = model.Count + 1 }, Cmd.none
    | Decrement -> { model with Count = model.Count - 1 }, Cmd.none

let render (state: Model) (dispatch: Message -> unit) =
    Html.div [
        prop.className "flex flex-di"
        prop.children [
            Html.button [
                prop.className "btn btn-primary"
                prop.onClick (fun _ -> dispatch Increment)
                prop.text "Increment"
            ]
            Html.button [
                prop.className "btn btn-primary"
                prop.onClick (fun _ -> dispatch Decrement)
                prop.text "Decrement"
            ]
            Html.p state.Count
        ]
    ]

Program.mkProgram init update render
|> Program.withReactSynchronous "root"
|> Program.run
