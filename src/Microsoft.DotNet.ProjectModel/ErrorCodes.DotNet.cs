// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ProjectModel
{
    public static partial class ErrorCodes
    {
        // Target framework not installed
        public static readonly string DOTNET1011 = nameof(DOTNET1011);

        // Reference assemblies location not specified
        public static readonly string DOTNET1012 = nameof(DOTNET1012);

        // Multiple libraries marked as "platform"
        public static readonly string DOTNET1013 = nameof(DOTNET1013);

        // Failed to read lock file
        public static readonly string DOTNET1014 = nameof(DOTNET1014);

        // The '{0}' option is deprecated. Use '{1}' instead.
        public static readonly string DOTNET1015 = nameof(DOTNET1015);

        // The '{0}' option in the root is deprecated. Use it in '{1}' instead.
        public static readonly string DOTNET1016 = nameof(DOTNET1016);
    }
}
