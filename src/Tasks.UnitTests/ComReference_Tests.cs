// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Xunit;
using Xunit.NetCore.Extensions;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class ComReference_Tests
    {
        private static Dictionary<string, string> s_existingFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static Dictionary<string, string> ExistingFilesDictionary
        {
            get
            {
                if (s_existingFiles.Count != 0)
                {
                    return s_existingFiles;
                }

                s_existingFiles.Add(@"C:\test\typelib1.dll", string.Empty);
                s_existingFiles.Add(@"C:\test\typelib2\2.dll", string.Empty);
                s_existingFiles.Add(@"C:\test\typelib3.\3dll", string.Empty);
                s_existingFiles.Add(@"C:\test\typelib4.dll", string.Empty);
                s_existingFiles.Add(@"C:\test\typelib5.dll", string.Empty);

                return s_existingFiles;
            }
        }

        private static bool FileExistsMock(string filepath)
        {
            return ExistingFilesDictionary.ContainsKey(filepath);
        }

        [WindowsOnlyFact("COM is only found on Windows.")]
        public void TestStripTypeLibNumber()
        {
            Assert.Null(ComReference.StripTypeLibNumberFromPath(null, new FileExists(FileExistsMock)));
            Assert.Equal("", ComReference.StripTypeLibNumberFromPath("", new FileExists(FileExistsMock)));
            Assert.Equal(@"C:\test\typelib1.dll", ComReference.StripTypeLibNumberFromPath(@"C:\test\typelib1.dll", new FileExists(FileExistsMock)));
            Assert.Equal(@"C:\test\typelib2\2.dll", ComReference.StripTypeLibNumberFromPath(@"C:\test\typelib2\2.dll", new FileExists(FileExistsMock)));
            Assert.Equal(@"C:\test\typelib3.\3dll", ComReference.StripTypeLibNumberFromPath(@"C:\test\typelib3.\3dll", new FileExists(FileExistsMock)));
            Assert.Equal(@"C:\test\typelib4.dll", ComReference.StripTypeLibNumberFromPath(@"C:\test\typelib4.dll\4", new FileExists(FileExistsMock)));
            Assert.Equal(@"C:\test\typelib5.dll", ComReference.StripTypeLibNumberFromPath(@"C:\test\typelib5.dll\555", new FileExists(FileExistsMock)));
            Assert.Equal(@"", ComReference.StripTypeLibNumberFromPath(@"C:\test\typelib6.dll", new FileExists(FileExistsMock)));
            Assert.Equal(@"", ComReference.StripTypeLibNumberFromPath(@"C:\test\typelib7.dll\7", new FileExists(FileExistsMock)));
        }
    }
}
