using System;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli
{
    /// 
    /// <summary>Allows control flow to be interrupted in order to display help in the console.</summary>
    ///
    [Obsolete("This is intended to facilitate refactoring during parser replacement and should not be used after that work is done.")]
    public class HelpException : Exception
    {
        public HelpException(string message) : base(message)
        {
            Data.Add(ExceptionExtensions.CLI_User_Displayed_Exception, true);
        }
    }
}