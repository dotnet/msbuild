// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.BuildServer
{
    internal enum ResultKind
    {
        Success,
        Failure,
        Skipped
    }

    internal struct Result
    {
        public Result(ResultKind kind, string message = null)
        {
            Kind = kind;
            Message = message;
            Exception = null;
        }

        public Result(Exception exception)
        {
            Kind = ResultKind.Failure;
            Message = exception.Message;
            Exception = exception;
        }

        public ResultKind Kind { get; private set; }

        public string Message { get; private set; }

        public Exception Exception { get; private set; }
    }
}
