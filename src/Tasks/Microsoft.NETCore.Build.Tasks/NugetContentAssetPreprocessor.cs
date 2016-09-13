// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.NETCore.Build.Tasks
{
    public class NugetContentAssetPreprocessor : IContentAssetPreprocessor
    {
        private readonly Dictionary<string, string> _preprocessorValues;
        private readonly string _preprocessedOutputDirectory;

        public NugetContentAssetPreprocessor(string outputDirectory, Dictionary<string, string> preprocessorValues)
        {
            _preprocessorValues = preprocessorValues;
            _preprocessedOutputDirectory = Path.Combine(outputDirectory, BuildPreprocessedContentHash(preprocessorValues));
        }

        public bool Process(string originalAssetPath, string relativeOutputPath, out string pathToFinalAsset)
        {
            bool fileWritten = false;

            pathToFinalAsset = Path.Combine(_preprocessedOutputDirectory, relativeOutputPath);

            if (!File.Exists(pathToFinalAsset))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(pathToFinalAsset));

                using (FileStream input = File.OpenRead(originalAssetPath))
                {
                    string result = Preprocessor.Process(input, (token) => {
                        string value;
                        if (!_preprocessorValues.TryGetValue(token, out value))
                        {
                            throw new Exception($"The token &apos;${token}$&apos; is unrecognized");
                        }
                        return value;
                    });
                    File.WriteAllText(pathToFinalAsset, result);
                    fileWritten = true;
                }
            }

            return fileWritten;
        }

        /// <summary>
        /// Produces a string hash of the key/values in the dictionary. This hash is used to put all the
        /// preprocessed files into a folder with the name so we know to regenerate when any of the
        /// inputs change.
        /// </summary>
        public static string BuildPreprocessedContentHash(IReadOnlyDictionary<string, string> values)
        {
            using (var stream = new MemoryStream())
            {
                using (var streamWriter = new StreamWriter(stream, Encoding.UTF8, bufferSize: 4096, leaveOpen: true))
                {
                    foreach (var pair in values.OrderBy(v => v.Key))
                    {
                        streamWriter.Write(pair.Key);
                        streamWriter.Write('\0');
                        streamWriter.Write(pair.Value);
                        streamWriter.Write('\0');
                    }
                }

                stream.Position = 0;

                return SHA1.Create().ComputeHash(stream).Aggregate("", (s, b) => s + b.ToString("x2"));
            }
        }
    }
}
