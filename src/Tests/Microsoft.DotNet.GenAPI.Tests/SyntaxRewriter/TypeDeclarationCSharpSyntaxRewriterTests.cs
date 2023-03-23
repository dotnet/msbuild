// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using Microsoft.DotNet.GenAPI.SyntaxRewriter;
using Xunit;

namespace Microsoft.DotNet.GenAPI.Tests.SyntaxRewriter
{
    public class TypeDeclarationCSharpSyntaxRewriterTests : CSharpSyntaxRewriterTestBase
    {
        [Fact]
        public void TestRemoveSystemObjectAsBaseClass()
        {
            CompareSyntaxTree(new TypeDeclarationCSharpSyntaxRewriter(),
                original: """
                namespace A
                {
                    class B : global::System.Object
                    {
                    }
                }
                """,
                expected: """
                namespace A
                {
                    partial class B
                    {
                    }
                }
                """);
        }

        [Fact]
        public void TestAddPartialKeyword()
        {
            CompareSyntaxTree(new TypeDeclarationCSharpSyntaxRewriter(),
                original: """
                namespace A
                {
                    class B { }
                    struct C { }
                    interface D { }
                }
                """,
                expected: """
                namespace A
                {
                    partial class B { }
                    partial struct C { }
                    partial interface D { }
                }
                """);
        }

        [Fact]
        public void TestPartialTypeDeclaration()
        {
            CompareSyntaxTree(new TypeDeclarationCSharpSyntaxRewriter(),
                original: """
                namespace A
                {
                    partial class B { }
                    partial struct C { }
                    partial interface D { }
                }
                """,
                expected: """
                namespace A
                {
                    partial class B { }
                    partial struct C { }
                    partial interface D { }
                }
                """);
        }
    }
}
