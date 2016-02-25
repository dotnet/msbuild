// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public static class CommandResolverTestUtils
    {
        public static void CreateNonRunnableTestCommand(string directory, string filename, string extension=".dll")
        {
            Directory.CreateDirectory(directory);
            
            var filePath = Path.Combine(directory, filename + extension);

            File.WriteAllText(filePath, "test command that does nothing.");
        }

        public static IEnvironmentProvider SetupEnvironmentProviderWhichFindsExtensions(params string[] extensions)
        {
            return new EnvironmentProvider(extensions);
        }
    }
}
