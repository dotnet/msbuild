// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Compiler;

namespace Microsoft.DotNet.Tools.Build
{
    internal class BuilderCommandApp : CompilerCommandApp
    {
        public const string BuildProfileFlag = "--build-profile";
        public const string ForceUnsafeFlag = "--force-incremental-unsafe";

        public bool BuildProfileValue => OptionHasValue(BuildProfileFlag);
        public bool ForceUnsafeValue => OptionHasValue(ForceUnsafeFlag);

        public BuilderCommandApp(string name, string fullName, string description) : base(name, fullName, description)
        {
            AddNoValueOption(BuildProfileFlag, "Set this flag to print the incremental safety checks that prevent incremental compilation");
            AddNoValueOption(ForceUnsafeFlag, "Set this flag to mark the entire build as not safe for incrementality");
        }
    }
}