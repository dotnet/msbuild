// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Cli.Utils
{
    public class GracefulException : Exception
    {
        public bool IsUserError { get; } = true;
        public string VerboseMessage { get; } = string.Empty;

        public GracefulException(string message) : base(message)
        {
            Data.Add(ExceptionExtensions.CLI_User_Displayed_Exception, true);
        }


        public GracefulException(IEnumerable<string> messages, IEnumerable<string> verboseMessages = null,
            bool isUserError = true)
            : base(string.Join(Environment.NewLine, messages))
        {
            IsUserError = isUserError;
            if (verboseMessages != null)
            {
                VerboseMessage = string.Join(Environment.NewLine, verboseMessages);
            }

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
