// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Class representing a reference to a project import path with property fall-back
    /// This class is immutable.
    /// If mutability would be needed in the future, it should be implemented via copy-on-write or
    ///  a DeepClone would need to be added (and called from DeepClone methods of owning types)
    /// </summary>
    internal class ProjectImportPathMatch : ITranslatable
    {
        /// <summary>
        /// ProjectImportPathMatch instance representing no fall-back
        /// </summary>
        public static readonly ProjectImportPathMatch None = new ProjectImportPathMatch(string.Empty, new List<string>());

        // Those are effectively readonly and should stay so. Cannot be marked readonly due to ITranslatable
        private string _propertyName;
        private string _msBuildPropertyFormat;
        private List<string> _searchPaths;

        internal ProjectImportPathMatch(string propertyName, List<string> searchPaths)
        {
            ErrorUtilities.VerifyThrowArgumentNull(propertyName, nameof(propertyName));
            ErrorUtilities.VerifyThrowArgumentNull(searchPaths, nameof(searchPaths));

            _propertyName = propertyName;
            _searchPaths = searchPaths;
            _msBuildPropertyFormat = $"$({PropertyName})";
        }

        public ProjectImportPathMatch(ITranslator translator)
        {
            ((ITranslatable)this).Translate(translator);
        }

        /// <summary>
        /// String representation of the property reference - eg. "MSBuildExtensionsPath32"
        /// </summary>
        public string PropertyName => _propertyName;

        /// <summary>
        /// Returns the corresponding property name - eg. "$(MSBuildExtensionsPath32)"
        /// </summary>
        public string MsBuildPropertyFormat => _msBuildPropertyFormat;

        /// <summary>
        /// Enumeration of the search paths for the property.
        /// </summary>
        public List<string> SearchPaths => _searchPaths;

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _propertyName);
            translator.Translate(ref _msBuildPropertyFormat);
            translator.Translate(ref _searchPaths);
        }

        /// <summary>
        /// Factory for serialization.
        /// </summary>
        internal static ProjectImportPathMatch FactoryForDeserialization(ITranslator translator)
        {
            return new ProjectImportPathMatch(translator);
        }
    }
}
