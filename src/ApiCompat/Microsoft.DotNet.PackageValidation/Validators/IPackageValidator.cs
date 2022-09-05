// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
