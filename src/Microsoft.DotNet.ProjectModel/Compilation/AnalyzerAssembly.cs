// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel.Compilation
{
    public class AnalyzerReference
    {
        /// <summary>
        /// The fully-qualified path to the analyzer assembly.
        /// </summary>
        public string AssemblyPath { get; }
        
        /// <summary>
        /// The supported language of the analyzer assembly.
        /// </summary>
        public string AnalyzerLanguage { get; }
        
        /// <summary>
        /// The required framework for hosting the analyzer assembly.
        /// </summary>
        public NuGetFramework RequiredFramework { get; }
        
        /// <summary>
        /// The required runtime for hosting the analyzer assembly.
        /// </summary>
        public string RuntimeIdentifier { get; }
        
        public AnalyzerReference(
            string assembly,
            NuGetFramework framework,
            string language,
            string runtimeIdentifier)
        {
            AnalyzerLanguage = language;
            AssemblyPath = assembly;
            RequiredFramework = framework;
            RuntimeIdentifier = runtimeIdentifier;
        }
    }
}