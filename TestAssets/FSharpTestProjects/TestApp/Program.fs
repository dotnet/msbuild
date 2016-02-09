// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace TestApp

open System
open System.Diagnostics

module Program =

    open TestLibrary

    [<EntryPoint>]
    let Main (args: string array) =
        printfn "%s" (TestLibrary.Helper.GetMessage())
        0
