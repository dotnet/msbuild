// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace TestApp

open System
open System.Diagnostics

module Program =

    [<EntryPoint>]
    let Main args =
        printfn "Hello World!"

        printfn "I was passed %d args:" args.Length

        args |> Array.iter (printfn "arg: [%s]")

        0
