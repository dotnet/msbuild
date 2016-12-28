// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Xunit;
using System;
using System.Collections.Generic;
using System.Text;

namespace Templates.UnitTests
{
    public class EncodeTests
    {
        private static readonly HashSet<string> IgnoreFileExtensionSet = new HashSet<string> {".json", ".props", ".target", ".png"};

        [Fact]
        public void AllFilesInTemplatesShouldBeUTF8Encoded()
        {
            var dirPath = Path.Combine(AppContext.BaseDirectory, @"..\..\..\src\Templates");
            this.CheckAllFilesUTF8Encoded(dirPath);
        }

        private void CheckAllFilesUTF8Encoded(string dirPath)
        {
            foreach (var filePath in Directory.GetFiles(dirPath))
            {
                var fileExtension = Path.GetExtension(filePath);
                if (EncodeTests.IgnoreFileExtensionSet.Contains(fileExtension))
                {
                    continue;
                }
                Assert.True(this.IsUTF8EncodedWithBOM(filePath), $"{filePath} should be UTF-8 encoded with BOM");
            }

            foreach (var childDirPath in Directory.GetDirectories(dirPath))
            {
                this.CheckAllFilesUTF8Encoded(childDirPath);
            }
        }

        /// <summary>
        /// Check file is UTF-8 encoded with BOM.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>true; If file encoding is UTF-8 with BOM</returns>
        private bool IsUTF8EncodedWithBOM(string filePath)
        {
            var strictUTF8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true);
            using (var reader = new StreamReader(File.OpenRead(filePath), strictUTF8, detectEncodingFromByteOrderMarks: false))
            {
                try
                {
                    reader.ReadToEnd();
                }
                catch (DecoderFallbackException)
                {
                    return false;
                }
            }

            var bom = new byte[3];
            using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                file.Read(bom, 0, 3);
            }

            return bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF;
        }
    }
}