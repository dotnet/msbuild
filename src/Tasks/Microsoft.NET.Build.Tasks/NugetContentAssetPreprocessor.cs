// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.NET.Build.Tasks
{
    internal class NugetContentAssetPreprocessor : IContentAssetPreprocessor
    {
        private Dictionary<string, string> _preprocessorValues = new Dictionary<string, string>();
        private string _preprocessedOutputDirectory = null;

        public void ConfigurePreprocessor(string outputDirectoryBase, Dictionary<string, string> preprocessorValues)
        {
            _preprocessorValues = preprocessorValues ?? new Dictionary<string, string>();
            _preprocessedOutputDirectory = Path.Combine(outputDirectoryBase, BuildPreprocessedContentHash(_preprocessorValues));
        }

        public bool Process(string originalAssetPath, string relativeOutputPath, out string pathToFinalAsset)
        {
            if (_preprocessedOutputDirectory == null)
            {
                throw new InvalidOperationException(Strings.AssetPreprocessorMustBeConfigured);
            }

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
                            throw new BuildErrorException(Strings.UnrecognizedPreprocessorToken, $"${token}$", originalAssetPath);
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
        private static string BuildPreprocessedContentHash(IReadOnlyDictionary<string, string> values)
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

                using (var sha1 = SHA1.Create())
                {
                    return BitConverter.ToString(sha1.ComputeHash(stream)).Replace("-", "");
                }
            }
        }
    }
}
