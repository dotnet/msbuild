// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using Microsoft.DotNet.Tools.Test.Utilities;
using FluentAssertions;
using Xunit;
using Microsoft.NET.TestFramework;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Utilities;

namespace Microsoft.DotNet.Tests
{
    /// <summary>
    /// Static analysis of assemblies to make sure that they are crossgened.
    /// </summary>
    public class CrossgenTests : SdkTest
    {
        public CrossgenTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact(Skip = "This coverage needs to be in core-sdk, which is where crossgen is applied")]
        public void CLI_SDK_assemblies_must_be_crossgened()
        {
            //  TODO: Update method of finding cliPath (right now it's finding a ref path in stage 0
            string dotnetDir = FindDotnetDirInPath();
            string cliPath = Directory.EnumerateFiles(dotnetDir, "dotnet.dll", SearchOption.AllDirectories).First();
            cliPath = Path.GetDirectoryName(cliPath);
            CheckDirectoryIsCrossgened(cliPath);
        }

        [Fact(Skip ="This coverage needs to be in core-sdk, which is where crossgen is applied")]
        public void Shared_Fx_assemblies_must_be_crossgened()
        {
            //  TODO: Update method of finding sharedFxPath
            string dotnetDir = FindDotnetDirInPath();
            string sharedFxPath = Directory.EnumerateFiles(dotnetDir, "mscorlib*.dll", SearchOption.AllDirectories).First();
            sharedFxPath = Path.GetDirectoryName(sharedFxPath);
            CheckDirectoryIsCrossgened(sharedFxPath);
        }

        private static void CheckDirectoryIsCrossgened(string pathToAssemblies)
        {
            Console.WriteLine($"Checking directory '{pathToAssemblies}' for crossgened assemblies");

            var dlls = Directory.EnumerateFiles(pathToAssemblies, "*.dll", SearchOption.TopDirectoryOnly);
            var exes = Directory.EnumerateFiles(pathToAssemblies, "*.exe", SearchOption.TopDirectoryOnly);
            var assemblies = dlls.Concat(exes);
            assemblies.Count().Should().NotBe(0, $"No assemblies found at directory '{pathToAssemblies}'");

            foreach (var assembly in assemblies)
            {
                using (var asmStream = File.OpenRead(assembly))
                {
                    using (var peReader = new PEReader(asmStream))
                    {
                        if (peReader.HasMetadata)
                        {
                            peReader.IsCrossgened().Should().BeTrue($"Managed assembly '{assembly}' is not crossgened.");
                        }
                    }
                }
            }
        }

        private static string FindDotnetDirInPath()
        {
            string dotnetExecutable = $"dotnet{Microsoft.DotNet.Cli.Utils.FileNameSuffixes.CurrentPlatform.Exe}";
            foreach (string path in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                string dotnetPath = Path.Combine(path, dotnetExecutable);
                if (File.Exists(dotnetPath))
                {
                    return Path.GetDirectoryName(dotnetPath);
                }
            }

            throw new FileNotFoundException($"Unable to find '{dotnetExecutable}' in the $PATH");
        }
    }
}
