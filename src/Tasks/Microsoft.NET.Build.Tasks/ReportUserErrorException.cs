// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.NET.Build.Tasks
{
    internal class ReportUserErrorException : Exception
    {
        public ReportUserErrorException()
        {
        }

        public ReportUserErrorException(string message) : base(message)
        {
        }

        public ReportUserErrorException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
