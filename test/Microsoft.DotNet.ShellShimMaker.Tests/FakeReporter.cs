// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.ShellShimMaker.Tests
{
    internal class FakeReporter : IReporter
    {
        public string Message { get; private set; } = "";

        public void WriteLine(string message)
        {
            Message = message;
        }

        public void WriteLine()
        {
            throw new NotImplementedException();
        }

        public void Write(string message)
        {
            throw new NotImplementedException();
        }
    }
}
