// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public class RuleSettings
    {
        public RuleSettings(bool strictMode)
        {
            StrictMode = strictMode;
        }

        public bool StrictMode { get; }
    }
}
