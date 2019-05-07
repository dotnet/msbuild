// Learn more about F# at http://fsharp.org

open System


// Define a function to print a message
let from who =
    sprintf "from %s" message

[<EntryPoint>]
let main argv =
    let nmessage = from "F#" // Call the function
    printMessage "Hello world %s" message
    0 // return an integer exit code
