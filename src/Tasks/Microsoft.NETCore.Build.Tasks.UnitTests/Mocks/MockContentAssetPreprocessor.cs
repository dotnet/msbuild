// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.NETCore.Build.Tasks.UnitTests
{
    public class MockContentAssetPreprocessor : IContentAssetPreprocessor
    {
        private readonly Dictionary<string, string> _preprocessorValues;
        private readonly string _preprocessedOutputDirectory;
        private readonly Func<string, bool> _exists;

        public string MockReadContent { get; set; }

        public string MockWrittenContent { get; set; }

        public MockContentAssetPreprocessor(string outputDirectory, 
            Dictionary<string, string> preprocessorValues, 
            Func<string,bool> exists)
        {
            _preprocessorValues = preprocessorValues;
            _preprocessedOutputDirectory = Path.Combine(outputDirectory, "test");
            _exists = exists;
        }

        public bool Process(string originalAssetPath, string relativeOutputPath, out string pathToFinalAsset)
        {
            bool fileWritten = false;

            pathToFinalAsset = Path.Combine(_preprocessedOutputDirectory, relativeOutputPath);

            if (!_exists(pathToFinalAsset))
            {
                // Mock reading, processsing and writing content
                var inputBytes = Encoding.ASCII.GetBytes(MockReadContent);
                using (MemoryStream input = new MemoryStream(inputBytes))
                {
                    string result = Preprocessor.Process(input, (token) =>
                    {
                        string value;
                        if (!_preprocessorValues.TryGetValue(token, out value))
                        {
                            throw new Exception($"The token &apos;${token}$&apos; is unrecognized");
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