// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tests
{
    public static class CommandResolverTestUtils
    {
        public static string CreateNonRunnableTestCommand(string directory, string filename, string extension=".dll")
        {
            Directory.CreateDirectory(directory);
            
            var filePath = Path.Combine(directory, filename + extension);

            File.WriteAllText(filePath, "test command that does nothing.");

            return filePath;
        }

        public static IEnvironmentProvider SetupEnvironmentProviderWhichFindsExtensions(params string[] extensions)
        {
            return new EnvironmentProvider(extensions);
        }
    }
}
