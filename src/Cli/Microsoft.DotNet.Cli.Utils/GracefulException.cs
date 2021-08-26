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
            : this(string.Join(Environment.NewLine, messages), isUserError: isUserError)
        {
            if (verboseMessages != null)
            {
                VerboseMessage = string.Join(Environment.NewLine, verboseMessages);
            }
        }

        public GracefulException(string format, params string[] args) : this(string.Format(format, args))
        {
        }

        public GracefulException(string message, Exception innerException = null, bool isUserError = true) : base(message, innerException)
        {
            IsUserError = isUserError;
            Data.Add(ExceptionExtensions.CLI_User_Displayed_Exception, isUserError);
        }
    }
}
