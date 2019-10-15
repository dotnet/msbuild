// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class MockContentAssetPreprocessor : IContentAssetPreprocessor
    {
        private Dictionary<string, string> _preprocessorValues = new Dictionary<string, string>();
        private string _preprocessedOutputDirectory = null;
        private readonly Func<string, bool> _exists;

        public string MockReadContent { get; set; }

        public string MockWrittenContent { get; set; }

        public MockContentAssetPreprocessor(Func<string,bool> exists)
        {
            _exists = exists;
        }

        public void ConfigurePreprocessor(string outputDirectoryBase, Dictionary<string, string> preprocessorValues)
        {
            _preprocessorValues = preprocessorValues ?? new Dictionary<string, string>();
            _preprocessedOutputDirectory = Path.Combine(outputDirectoryBase, "test");
        }

        public bool Process(string originalAssetPath, string relativeOutputPath, out string pathToFinalAsset)
        {
            bool fileWritten = false;

            pathToFinalAsset = Path.Combine(_preprocessedOutputDirectory, relativeOutputPath);

            if (!_exists(pathToFinalAsset))
            {
                // Mock reading, processing and writing content
                var inputBytes = Encoding.ASCII.GetBytes(MockReadContent);
                using (MemoryStream input = new MemoryStream(inputBytes))
                {
                    string result = Preprocessor.Process(input, (token) =>
                    {
                        string value;
                        if (!_preprocessorValues.TryGetValue(token, out value))
                        {
                            throw new InvalidDataException($"The token '${token}$' is unrecognized");
                        }
                        return value;
                    });

                    MockWrittenContent = result;
                    fileWritten = true;
                }
            }

            return fileWritten;
        }
    }
}