using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.Cli
{
    internal class CommandCreationException : Exception
    {
        public int ExitCode { get; private set; }

        public CommandCreationException(int exitCode)
        {
            ExitCode = exitCode;
        }
    }
}
