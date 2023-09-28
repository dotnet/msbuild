// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    internal static class Utilities
    {
        private static readonly NamedMonitor Locker = new();

        /// <summary>
        /// Gets a folder that dotnet-new.IntegrationTests tests can use for temp files.
        /// </summary>
        internal static string GetTestExecutionTempFolder()
        {
            return Path.Combine(TestContext.Current.TestExecutionDirectory, "dotnet-new.IntegrationTests");
        }

        /// <summary>
        /// Creates a temp folder in location dedicated for dotnet-new.IntegrationTests.
        /// Format: artifacts\tmp\Debug\dotnet-new.IntegrationTests\<paramref name="caller"/>\<paramref name="customName"/>\date-time-utc-now[optional counter].
        /// </summary>
        internal static string CreateTemporaryFolder([CallerMemberName] string caller = "Unnamed", string customName = "")
        {
            string baseDir = Path.Combine(GetTestExecutionTempFolder(), caller, customName);

            lock (Locker[baseDir])
            {
                string workingDir = Path.Combine(baseDir, DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
                if (Directory.Exists(workingDir))
                {
                    //simple logic for counts if DateTime.UtcNow is not unique
                    int counter = 1;
                    while (Directory.Exists(workingDir + "_" + counter) && counter < 100)
                    {
                        counter++;
                    }
                    if (counter == 100)
                    {
                        throw new Exception("Failed to create temp directory after 100 attempts");
                    }
                    workingDir = workingDir + "_" + counter;
                }
                Directory.CreateDirectory(workingDir);
                return workingDir;
            }
        }

        // Provides a thread safe Dictionary for creating critical section
        internal class NamedMonitor
        {
            private readonly ConcurrentDictionary<string, object> _dictionary = new(StringComparer.OrdinalIgnoreCase);

            public object this[string name] => _dictionary.GetOrAdd(name, _ => new object());
        }
    }
}
