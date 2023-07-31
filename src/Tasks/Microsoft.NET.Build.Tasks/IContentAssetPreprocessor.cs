// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Resource for reading, processing and writing content assets
    /// </summary>
    internal interface IContentAssetPreprocessor
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
        /// <exception cref="InvalidOperationException"><see cref="ConfigurePreprocessor"/> was not called.</exception>
        bool Process(string originalAssetPath, string relativeOutputPath, out string pathToFinalAsset);
    }
}
