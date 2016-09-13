// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.NETCore.Build.Tasks
{
    /// <summary>
    /// Resource for reading, processing and writing content assets
    /// </summary>
    public interface IContentAssetPreprocessor
    {
        /// <summary>
        /// Read and process a content asset from originalAssetPath and write
        /// result to specified output path
        /// </summary>
        /// <returns>true if an asset is written, false otherwise</returns>
        bool Process(string originalAssetPath, string relativeOutputPath, out string pathToFinalAsset);
    }
}
