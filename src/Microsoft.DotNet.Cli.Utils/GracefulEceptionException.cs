using System;

namespace Microsoft.DotNet.Cli.Utils
{
    public class GracefulException : Exception
    {
        public GracefulException()
        {
        }

        public GracefulException(string message) : base(message)
        {
        }

        public GracefulException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}