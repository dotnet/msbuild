using System;

namespace Microsoft.DotNet.Cli.Build
{
    public class BuildFailureException : Exception
    {
        public BuildFailureException()
        {
        }

        public BuildFailureException(string message) : base(message)
        {
        }
        
        public BuildFailureException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}