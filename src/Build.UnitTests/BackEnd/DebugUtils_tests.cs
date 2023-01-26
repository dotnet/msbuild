// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

#nullable disable

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
                exceptionFiles = Directory.GetFiles(FileUtilities.TempFileDirectory, "MSBuild_*failure.txt");
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
