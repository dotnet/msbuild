// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;

namespace Microsoft.Build.Shared
{
    internal static class ExceptionUtilities
    {
        internal static string GetInnerExceptionMessageString(Exception exception)
        {
            var flattenedMessage = new StringBuilder(exception.Message);

            while (exception.InnerException is not null)
            {
                exception = exception.InnerException;
                flattenedMessage.Append(" ---> ").Append(exception.Message);
            }

            return flattenedMessage.ToString();
        }
    }
}
