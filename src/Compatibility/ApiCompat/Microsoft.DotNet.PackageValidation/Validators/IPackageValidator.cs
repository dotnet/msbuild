// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.PackageValidation.Validators
{
    /// <summary>
    /// A package validator type that validates packages.
    /// </summary>
    public interface IPackageValidator
    {
        /// <summary>
        /// Validates a package with the passed in options.
        /// </summary>
        /// <param name="options">Options to configure the validator</param>
        void Validate(PackageValidatorOption options);
    }
}
