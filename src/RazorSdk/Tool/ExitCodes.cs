// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Razor.Tool {
    public enum ExitCodes {
        ServerMutexFailure = -1,
        Success = 0,
        Failure = 1,
        FailureRazorError = 2
    }
}

