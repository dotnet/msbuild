// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.PackageValidation.Validators
{
    /// <summary>
    /// A package validator option bag that is passed into package validators for configuration.
    /// </summary>
    public readonly struct PackageValidatorOption
    {
        /// <summary>
        /// The latest package that should be validated.
        /// </summary>
        public Package Package { get; }

        /// <summary>
        /// If true, comparision is performed in strict mode.
        /// </summary>
        public bool EnableStrictMode { get; }

        /// <summary>
        /// If true, work items for api compatibility checks are enqueued.
        /// </summary>
        public bool EnqueueApiCompatWorkItems { get; }

        /// <summary>
        /// If true, executes enqueued api compatibility work items.
        /// </summary>
        public bool ExecuteApiCompatWorkItems { get; }

        /// <summary>
        /// The baseline package to validate the latest package.
        /// </summary>
        public Package? BaselinePackage { get; }

        /// <summary>
        /// Intantiates a new PackageValidatorOption type to be passed into a validator.
        /// </summary>
        public PackageValidatorOption(Package package,
            bool enableStrictMode = false,
            bool enqueueApiCompatWorkItems = true,
            bool executeApiCompatWorkItems = true,
            Package? baselinePackage = null)
        {
            Package = package;
            EnableStrictMode = enableStrictMode;
            EnqueueApiCompatWorkItems = enqueueApiCompatWorkItems;
            ExecuteApiCompatWorkItems = executeApiCompatWorkItems;
            BaselinePackage = baselinePackage;
        }
    }
}
