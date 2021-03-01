// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.IO;

namespace Microsoft.DotNet.Cli.Utils
{
    public interface IReporter : IStandardStreamWriter
    {
        void WriteLine(string message);

        void WriteLine();

    }

    internal class ReportingConsole : IConsole
    {
        private IReporter _reporter;

        public bool IsOutputRedirected => false;

        public IStandardStreamWriter Error => _reporter;

        public bool IsErrorRedirected => false;

        public bool IsInputRedirected => false;

        public IStandardStreamWriter Out => _reporter;

        public ReportingConsole(IReporter reporter)
        {
            _reporter = reporter;
        }
    }
}
