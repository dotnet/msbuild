// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ProjectModel
{
    public static partial class ErrorCodes
    {
        // The dependency A could not be resolved.
        public static readonly string NU1001 = nameof(NU1001);

        // The dependency A in project B does not support framework C."
        public static readonly string NU1002 = nameof(NU1002);

        // Invalid A section. The target B is invalid, targets must either be a file name or a directory suffixed with '/'. The root directory of the package can be specified by using a single '/' character.
        public static readonly string NU1003 = nameof(NU1003);

        // Invalid A section. The target B contains path-traversal characters ('.' or '..'). These characters are not permitted in target paths.
        public static readonly string NU1004 = nameof(NU1004);

        // Invalid A section. The target B refers to a single file, but the pattern C produces multiple files. To mark the target as a directory, suffix it with '/'.
        public static readonly string NU1005 = nameof(NU1005);

        // A. Please run \"dotnet restore\" to generate a new lock file.
        public static readonly string NU1006 = nameof(NU1006);

        // Dependency specified was A but ended up with B.
        public static readonly string NU1007 = nameof(NU1007);

        // A is an unsupported framework.
        public static readonly string NU1008 = nameof(NU1008);

        // The expected lock file doesn't exist. Please run \"dotnet restore\" to generate a new lock file.
        public static readonly string NU1009 = nameof(NU1009);

        // The dependency type was changed
        public static readonly string NU1010 = nameof(NU1010);

        // The dependency target '{0}' is unsupported.
        public static readonly string NU1011 = nameof(NU1011);

        // Dependency conflict.
        public static readonly string NU1012 = nameof(NU1012);
    }
}
