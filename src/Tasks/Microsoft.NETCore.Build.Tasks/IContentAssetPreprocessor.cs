// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.NETCore.Build.Tasks
{
    /// <summary>
    /// Resource for reading, processing and writing content assets
    /// </summary>
    public interface IContentAssetPreprocessor
    {
        /// <summary>
        /// Configure the preprocessor with a base outputDirectory and the tokens/value 
        /// pairs used during preprocessing
        /// </summary>
        void ConfigurePreprocessor(string outputDirectoryBase, Dictionary<string, string> preprocessorValues);

        /// <summary>
        /// Read and process a content asset from originalAssetPath and write
        /// result to specified output path
        /// </summary>
        /// <returns>true if an asset is written, false otherwise</returns>
        bool Process(string originalAssetPath, string relativeOutputPath, out string pathToFinalAsset);
    }
}
