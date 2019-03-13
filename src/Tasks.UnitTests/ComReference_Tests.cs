// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class ComReference_Tests
    {
        private static Dictionary<string, string> s_existingFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static Dictionary<string, string> ExistingFilesDictionary
        {
            get
            {
                if (s_existingFiles.Count != 0)
                    return s_existingFiles;

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

        [Fact]
        public void TestStripTypeLibNumber()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                return; // "COM is only found on Windows"
            }

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
