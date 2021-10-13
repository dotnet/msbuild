// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class DebugUtils_Tests
    {
        [Fact]
        public void DumpExceptionToFileShouldWriteInTempPathByDefault()
        {
            Directory.GetFiles(Path.GetTempPath(), "MSBuild_*failure.txt").ShouldBeEmpty();

            string[] exceptionFiles = null;

            try
            {
                ExceptionHandling.DumpExceptionToFile(new Exception("hello world"));
                exceptionFiles = Directory.GetFiles(Path.GetTempPath(), "MSBuild_*failure.txt");
            }
            finally
            {
                exceptionFiles.ShouldNotBeNull();
                exceptionFiles.ShouldHaveSingleItem();

                var exceptionFile = exceptionFiles.First();
                File.ReadAllText(exceptionFile).ShouldContain("hello world");
                File.Delete(exceptionFile);
            }
        }
    }
}
