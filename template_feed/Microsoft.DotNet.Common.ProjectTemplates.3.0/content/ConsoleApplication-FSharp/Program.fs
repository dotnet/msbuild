// Learn more about F# at http://fsharp.org

open System

// Define a function to print a message
// Functions are defined before they are used in F#
let printMessage message =
    printfn "%s" message

[<EntryPoint>]
let main argv =
    // Call the previously-defined function
    printMessage "Hello world from F#!"
    0 // return an integer exit code
