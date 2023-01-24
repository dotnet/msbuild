// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.



#nullable disable

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.Debugging;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
namespace Microsoft.Build.UnitTests
{
    public class DebugUtils_Tests
    {
        private readonly ITestOutputHelper _testOutput;

        public DebugUtils_Tests(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }

        [Fact]
        public void DumpExceptionToFileShouldWriteInTempPathByDefault()
        {
            var exceptionFilesBefore = Directory.GetFiles(ExceptionHandling.DebugDumpPath, "MSBuild_*failure.txt");

            string[] exceptionFiles = null;

            try
            {
                ExceptionHandling.DumpExceptionToFile(new Exception("hello world"));
                exceptionFiles = Directory.GetFiles(ExceptionHandling.DebugDumpPath, "MSBuild_*failure.txt");
            }
            finally
            {
                exceptionFilesBefore.ShouldNotBeNull();
                exceptionFiles.ShouldNotBeNull();
                (exceptionFiles.Length - exceptionFilesBefore.Length).ShouldBe(1);

                var exceptionFile = exceptionFiles.Except(exceptionFilesBefore).Single();
                File.ReadAllText(exceptionFile).ShouldContain("hello world");
                File.Delete(exceptionFile);
            }
        }
    }
}
