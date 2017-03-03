using System;

namespace Microsoft.DotNet.Cli.Utils
{
    public class GracefulException : Exception
    {
        public GracefulException(string message) : base(message)
        {
            Data.Add(ExceptionExtensions.CLI_User_Displayed_Exception, true);
        }

        public GracefulException(string format, params string[] args) : this(string.Format(format, args))
        {
        }

        public GracefulException(string message, Exception innerException) : base(message, innerException)
        {
            Data.Add(ExceptionExtensions.CLI_User_Displayed_Exception, true);
        }
    }
}
