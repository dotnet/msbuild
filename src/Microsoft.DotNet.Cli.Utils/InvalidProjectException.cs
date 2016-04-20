using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Cli.Utils
{
    public class InvalidProjectException : Exception
    {
        public InvalidProjectException() { }
        public InvalidProjectException(string message) : base(message) { }
        public InvalidProjectException(string message, Exception innerException) : base(message, innerException) { }
    }
}
