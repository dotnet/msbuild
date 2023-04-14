// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    internal static class Utilities
    {
        private static readonly NamedMonitor Locker = new NamedMonitor();

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

        //Provides thread safe Dictionary for creating critical section
        internal class NamedMonitor
        {
            private readonly ConcurrentDictionary<string, object> _dictionary = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            public object this[string name] => _dictionary.GetOrAdd(name, _ => new object());
        }
    }
}
