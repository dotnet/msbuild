// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.Build.Engine.UnitTests.Globbing
{
    
    public static class GlobbingTestData
    {
        public static IEnumerable<object[]> GlobbingConesTestData
        {
            get
            {
                // recursive globbing cone is at the root
                yield return new object[]
                {
                    // glob
                    "**/*.cs",
                    // string to match
                    "a/a.cs",
                    // glob root
                    // nonempty root is necessary if .. appears in any of the other test paths.
                    // This is to ensure that Path.GetFullpath has enough path fragments to eat into
                    "",
                    // should GetItemProvenance find a match
                    true
                };
                yield return new object[]
                {
                    "**/*.cs",
                    "../a/a.cs",
                    "ProjectDirectory",
                    false
                };
                // recursive globbing cone is a superset of the globbing root via relative path
                yield return new object[]
                {
                    "../**/*.cs",
                    "../a/a.cs",
                    "ProjectDirectory",
                    true
                };
                yield return new object[]
                {
                    "../**/*.cs",
                    "a/a.cs",
                    "ProjectDirectory",
                    true
                };
                yield return new object[]
                {
                    "../**/*.cs",
                    "../../a/a.cs",
                    "dir/ProjectDirectory",
                    false
                };
                // recursive globbing cone is a subset of the globbing root via relative path
                yield return new object[]
                {
                    "a/**/*.cs",
                    "b/a.cs",
                    "",
                    false
                };
                yield return new object[]
                {
                    "a/**/*.cs",
                    "a/b/a.cs",
                    "",
                    true
                };
                yield return new object[]
                {
                    "a/**/*.cs",
                    "../a.cs",
                    "dir/ProjectDirectory",
                    false
                };
                // recursive globbing cone is disjoint of the globbing root via relative path
                yield return new object[]
                {
                    "../a/**/*.cs",
                    "../a/b/a.cs",
                    "dir/ProjectDirectory",
                    true
                };
                yield return new object[]
                {
                    "../a/**/*.cs",
                    "../b/c/a.cs",
                    "dir/ProjectDirectory",
                    false
                };
                yield return new object[]
                {
                    "../a/**/*.cs",
                    "a/a.cs",
                    "dir/ProjectDirectory",
                    false
                };

                // directory name glob is at the root
                yield return new object[]
                {
                    "a*a/*.cs",
                    "aba/a.cs",
                    "",
                    true
                };
                yield return new object[]
                {
                    "a*a/*.cs",
                    "aba/aba/a.cs",
                    "",
                    false
                };
                yield return new object[]
                {
                    "a*a/*.cs",
                    "../aba/a.cs",
                    "dir/ProjectDirectory",
                    false
                };
                // directory name glob is inside the glob root
                yield return new object[]
                {
                    "a/a*a/*.cs",
                    "a/aba/a.cs",
                    "",
                    true
                };
                yield return new object[]
                {
                    "a/a*a/*.cs",
                    "a/aba/a/a.cs",
                    "",
                    false
                };
                yield return new object[]
                {
                    "a/a*a/*.cs",
                    "../a/aba/a.cs",
                    "dir/ProjectDirectory",
                    false
                };
                // directory name glob is disjoint to the glob root
                yield return new object[]
                {
                    "../a/a*a/*.cs",
                    ".././a/aba/a.cs",
                    "dir/ProjectDirectory",
                    true
                };
                yield return new object[]
                {
                    "../a/a*a/*.cs",
                    "../a/a/aba/a.cs",
                    "dir/ProjectDirectory",
                    false
                };
                yield return new object[]
                {
                    "../a/a*a/*.cs",
                    "../../a/aba/a.cs",
                    "dir/ProjectDirectory",
                    false
                };

                // filename glob is at the root
                yield return new object[]
                {
                    "*.cs",
                    "a.cs",
                    "",
                    true
                };
                yield return new object[]
                {
                    "*.cs",
                    "a/a.cs",
                    "",
                    false
                };
                yield return new object[]
                {
                    "*.cs",
                    "../a.cs",
                    "dir/ProjectDirectory",
                    false
                };
                // filename glob is under the glob root
                yield return new object[]
                {
                    "a/*.cs",
                    "a/a.cs",
                    "",
                    true
                };
                yield return new object[]
                {
                    "a/*.cs",
                    "a/b/a.cs",
                    "",
                    false
                };
                yield return new object[]
                {
                    "a/*.cs",
                    "b/a.cs",
                    "",
                    false
                };
                yield return new object[]
                {
                    "a/*.cs",
                    "../a.cs",
                    "dir/ProjectDirectory",
                    false
                };
                // filename glob is disjoint to the glob root
                yield return new object[]
                {
                    ".././a/*.cs",
                    "../a/a.cs",
                    "dir/ProjectDirectory",
                    true
                };
                yield return new object[]
                {
                    "../a/*.cs",
                    "a.cs",
                    "dir/ProjectDirectory",
                    false
                };
                yield return new object[]
                {
                    "../a/*.cs",
                    "../a/a/a.cs",
                    "dir/ProjectDirectory",
                    false
                };

                // literal glob is at the root
                yield return new object[]
                {
                    "a.cs",
                    "a.cs",
                    "",
                    true
                };
                yield return new object[]
                {
                    "a.cs",
                    "a/a.cs",
                    "",
                    false
                };
                yield return new object[]
                {
                    "a.cs",
                    "../a.cs",
                    "dir/ProjectDirectory",
                    false
                };
                // literal glob is under the glob root
                yield return new object[]
                {
                    "a/a.cs",
                    "a/a.cs",
                    "",
                    true
                };
                yield return new object[]
                {
                    "a/a.cs",
                    "a/b/a.cs",
                    "",
                    false
                };
                yield return new object[]
                {
                    "a/a.cs",
                    "b/a.cs",
                    "",
                    false
                };
                yield return new object[]
                {
                    "a/a.cs",
                    "../a.cs",
                    "dir/ProjectDirectory",
                    false
                };
                // literal glob is disjoint to the glob root
                yield return new object[]
                {
                    ".././a/a.cs",
                    "../a/a.cs",
                    "dir/ProjectDirectory",
                    true
                };
                yield return new object[]
                {
                    "../a/a.cs",
                    "a.cs",
                    "dir/ProjectDirectory",
                    false
                };
                yield return new object[]
                {
                    "../a/a.cs",
                    "../a/a/a.cs",
                    "dir/ProjectDirectory",
                    false
                };
            }
        }
    }
}