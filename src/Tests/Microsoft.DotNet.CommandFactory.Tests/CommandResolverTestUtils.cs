// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tests
{
    public static class CommandResolverTestUtils
    {
        public static string CreateNonRunnableTestCommand(string directory, string filename, string extension = ".dll")
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
