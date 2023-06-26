// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.NET.TestFramework.Commands;

namespace Microsoft.DotNet.Tests
{
    public static class TestCommandExtensions
    {
        public static TestCommand WithUserProfileRoot(this TestCommand testCommand, string path)
        {
            var userProfileEnvironmentVariableName = GetUserProfileEnvironmentVariableName();
            return testCommand.WithEnvironmentVariable(userProfileEnvironmentVariableName, path);
        }
        
        private static string GetUserProfileEnvironmentVariableName()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "LocalAppData"
                : "HOME";
        }
    }
}
