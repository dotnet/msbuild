// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Xunit;
using System;

namespace Templates.UnitTests
{
    public class EncodeTests
    {
       [Fact]
        public void AllFilesInTemplatesShouldBeUTF8Encoded()
        {
            var dirPath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "Templates");
            CheckAllFilesUTF8Encoded(dirPath);
        }

        private void CheckAllFilesUTF8Encoded(string dirPath)
        {
            foreach (var filePath in Directory.GetFiles(dirPath))
            {
                if (Directory.Exists(filePath))
                {
                    CheckAllFilesUTF8Encoded(filePath);
                }
                else if (File.Exists(filePath))
                {
                    Assert.True(IsUTF8EncodedFile(filePath), $"{filePath} should be UTF-8 encoded");
                }
            }
        }

        /// <summary>
        /// Check first three bytes to check the file is UFT-8 or not.
        /// First three bytes should be EF BB BF for UTF-8
        /// Ref: https://msdn.microsoft.com/en-us/library/windows/desktop/dd374101(v=vs.85).aspx
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>true; If file encoding is UTF-8</returns>
        private bool IsUTF8EncodedFile(string filePath)
        {
            var bom = new byte[3];
            using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                file.Read(bom, 0, 3);
            }

            return bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF;
        }
    }
}