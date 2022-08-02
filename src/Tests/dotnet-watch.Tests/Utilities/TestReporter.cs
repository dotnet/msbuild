// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit.Abstractions;

namespace Microsoft.Extensions.Tools.Internal
{
    internal class TestReporter : IReporter
    {
        private readonly ITestOutputHelper _output;

        public TestReporter(ITestOutputHelper output)
        {
            _output = output;
        }

        public void Verbose(string message, string emoji = "⌚")
        {
            _output.WriteLine($"verbose {emoji} " + message);
        }

        public void Output(string message, string emoji = "⌚")
        {
            _output.WriteLine($"output {emoji} " + message);
        }

        public void Warn(string message, string emoji = "⌚")
        {
            _output.WriteLine($"warn {emoji} " + message);
        }

        public void Error(string message, string emoji = "❌")
        {
            _output.WriteLine($"error {emoji} " + message);
        }
    }
}
