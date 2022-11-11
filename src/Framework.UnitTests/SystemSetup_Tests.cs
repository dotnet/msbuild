// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;


namespace Microsoft.Build.Framework
{
    public sealed class SystemSetup_Tests
    {
        [Fact]
        public void VerifyLongPaths()
        {
            NativeMethodsShared.MaxPath.ShouldBeGreaterThan(10000, "Long paths are not enabled. Enable long paths via the registry.");
        }

#if NETCOREAPP
        [Fact]
        public void VerifySymLinksEnabled()
        {
            using TestEnvironment env = TestEnvironment.Create();
            TransientTestFile file = env.CreateFile("fileName.tmp", "fileContents");
            string path = Path.Combine(Path.GetTempPath(), "symLink");
            try
            {
                string symLink = File.CreateSymbolicLink(path, file.Path).FullName;
                string contents = File.ReadAllText(path);
                contents.ShouldBe("fileContents", "You do not have permissions to create symbolic links.");
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
#endif
    }
}
