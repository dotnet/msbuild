// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.NET.Sdk.Razor.Tool {
    public enum ExitCodes {
        ServerMutexFailure = -1,
        Success = 0,
        Failure = 1,
        FailureRazorError = 2
    }
}

