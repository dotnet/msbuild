// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Compiler;

namespace Microsoft.DotNet.Tools.Build
{
    internal class BuilderCommandApp : CompilerCommandApp
    {
        private const string BuildProfileFlag = "--build-profile";

        public bool BuildProfileValue => OptionHasValue(BuildProfileFlag);

        public BuilderCommandApp(string name, string fullName, string description) : base(name, fullName, description)
        {
            AddNoValueOption(BuildProfileFlag, "Set this flag to print the incremental safety checks that prevent incremental compilation");
        }
    }
}