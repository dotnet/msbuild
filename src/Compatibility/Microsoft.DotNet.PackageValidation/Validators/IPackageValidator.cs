// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.PackageValidation.Validators
{
    /// <summary>
    /// The interface that represents a package validator
    /// </summary>
    public interface IPackageValidator
    {
        /// <summary>
        /// Validates a package with the passed in options
        /// </summary>
        /// <param name="option">Options to configure the validator</param>
        public void Validate(PackageValidatorOption option);
    }
}
