// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Build.Engine.UnitTests.Globbing
{
    public static class GlobbingTestData
    {
        public static IEnumerable<object[]> IncludesAndExcludesWithWildcardsTestData
        {
            get
            {
                yield return new object[]
                {
                    "a.*", // include string
                    "*.1", // exclude string
                    new[] {"a.1", "a.2", "a.1"}, // files
                    new[] {"a.2"}, // expected include
                    false // whether to append the project directory to the expected include items
                };

                yield return new object[]
                {
                    @"**\*.cs",
                    @"a\**",
                    new[] {"1.cs", @"a\2.cs", @"a\b\3.cs", @"a\b\c\4.cs"},
                    new[] {"1.cs"},
                    false
                };

                yield return new object[]
                {
                    @"**\*",
                    @"**\b\**",
                    new[] {"1.cs", @"a\2.cs", @"a\b\3.cs", @"a\b\c\4.cs"},
                    new[] {"1.cs", @"a\2.cs", "build.proj"},
                    false
                };

                yield return new object[]
                {
                    @"**\*",
                    @"**\b\**\*.cs",
                    new[] {"1.cs", @"a\2.cs", @"a\b\3.cs", @"a\b\c\4.cs", @"a\b\c\5.txt"},
                    new[] {"1.cs", @"a\2.cs", @"a\b\c\5.txt", "build.proj"},
                    false
                };

                yield return new object[]
                {
                    @"src\**\proj\**\*.cs",
                    @"src\**\proj\**\none\**\*",
                    new[]
                    {
                        "1.cs",
                        @"src\2.cs",
                        @"src\a\3.cs",
                        @"src\proj\4.cs",
                        @"src\proj\a\5.cs",
                        @"src\a\proj\6.cs",
                        @"src\a\proj\a\7.cs",
                        @"src\proj\none\8.cs",
                        @"src\proj\a\none\9.cs",
                        @"src\proj\a\none\a\10.cs",
                        @"src\a\proj\a\none\11.cs",
                        @"src\a\proj\a\none\a\12.cs"
                    },
                    new[]
                    {
                        @"src\a\proj\6.cs",
                        @"src\a\proj\a\7.cs",
                        @"src\proj\4.cs",
                        @"src\proj\a\5.cs"
                    },
                    false
                };

                yield return new object[]
                {
                    @"**\*",
                    "foo",
                    new[]
                    {
                        "foo",
                        @"a\foo",
                        @"a\a\foo",
                        @"a\b\foo"
                    },
                    new[]
                    {
                        @"a\a\foo",
                        @"a\b\foo",
                        @"a\foo",
                        "build.proj"
                    },
                    false
                };

                yield return new object[]
                {
                    @"**\*",
                    @"a\af*\*",
                    new[]
                    {
                        @"a\foo",
                        @"a\a\foo",
                        @"a\b\foo"
                    },
                    new[]
                    {
                        @"a\a\foo",
                        @"a\b\foo",
                        @"a\foo",
                        "build.proj"
                    },
                    false
                };

                yield return new object[]
                {
                    @"$(MSBuildThisFileDirectory)\**\*",
                    @"$(MSBuildThisFileDirectory)\a\foo.txt",
                    new[]
                    {
                        @"a\foo",
                        @"a\foo.txt"
                    },
                    new[]
                    {
                        @"a\foo",
                        "build.proj"
                    },
                    true
                };

                yield return new object[]
                {
                    @"$(MSBuildThisFileDirectory)\**\*",
                    @"$(MSBuildThisFileDirectory)\a\**\*;build.proj",
                    new[]
                    {
                        @"a\a",
                        @"a\b\ab",
                        @"b\b",
                        @"c\c",
                        @"c\d\cd"
                    },
                    new[]
                    {
                        @"b\b",
                        @"c\c",
                        @"c\d\cd"
                    },
                    true
                };
            }
        }

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
