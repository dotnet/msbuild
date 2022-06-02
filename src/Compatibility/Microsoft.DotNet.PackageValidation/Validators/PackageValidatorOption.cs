// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.PackageValidation.Validators
{
    public struct PackageValidatorOption
    {
        public Package Package { get; set; }

        public Package BaselinePackage { get; set; }

        public bool EnableStrictMode { get; set; }

        public bool RunApiCompat { get; set; }

        public Dictionary<string, HashSet<string>> FrameworkReferences { get; set; }
    }
}
