// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Razor.Tool
{
    internal static class MutexName
    {
        public static string GetClientMutexName(string pipeName)
        {
            return $"{pipeName}.client";
        }

        public static string GetServerMutexName(string pipeName)
        {
            // We want to prefix this with Global\ because we want this mutex to be visible
            // across terminal sessions which is useful for cases like shutdown.
            // https://msdn.microsoft.com/en-us/library/system.threading.mutex(v=vs.110).aspx#Remarks
            // This still wouldn't allow other users to access the server because the pipe will fail to connect.
            return $"Global\\{pipeName}.server";
        }
    }
}
