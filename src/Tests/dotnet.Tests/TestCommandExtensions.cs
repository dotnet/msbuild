// Copyright (c) .NET Foundation and contributors. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

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
