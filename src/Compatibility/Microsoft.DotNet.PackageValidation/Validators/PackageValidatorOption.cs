// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.PackageValidation.Validators
{
    /// <summary>
    /// A package validator option bag that is passed into package validators for configuration.
    /// </summary>
    public readonly struct PackageValidatorOption
    {
        /// <summary>
        /// The latest package that should be validated
        /// </summary>
        public Package Package { get; }

        /// <summary>
        /// If true, comparison is performed in strict mode
        /// </summary>
        public bool EnableStrictMode { get; }

        /// <summary>
        /// If true, ApiCompat comparison is performed in addition to other package checks
        /// </summary>
        public bool RunApiCompat { get; }

        /// <summary>
        /// Assembly reference assemblies grouped per target framework
        /// </summary>
        public Dictionary<string, HashSet<string>>? FrameworkReferences { get; }

        /// <summary>
        /// The baseline package to validate the latest package
        /// </summary>
        public Package? BaselinePackage { get; }

        /// <summary>
        /// Intantiates a new PackageValidatorOption type to be passed into a validator
        /// </summary>
        public PackageValidatorOption(Package package,
            bool enableStrictMode = false,
            bool runApiCompat = false,
            Dictionary<string, HashSet<string>>? frameworkReferences = null,
            Package? baselinePackage = null)
        {
            Package = package;
            EnableStrictMode = enableStrictMode;
            RunApiCompat = runApiCompat;
            FrameworkReferences = frameworkReferences;
            BaselinePackage = baselinePackage;
        }
    }
}
