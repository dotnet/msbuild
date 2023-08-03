// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.DotNet.GenAPI.SyntaxRewriter;

namespace Microsoft.DotNet.GenAPI.Tests.SyntaxRewriter
{
    public class BodyBlockCSharpSyntaxRewriterTests : CSharpSyntaxRewriterTestBase
    {
        [Fact]
        public void TestMethodDeclaration()
        {
            CompareSyntaxTree(new BodyBlockCSharpSyntaxRewriter(null),
                original: """
                namespace A
                {
                    class B
                    {
                        void M1() {}
                        int M2() { return 1; }
                        abstract int M3;
                    }
                }
                """,
                expected: """
                namespace A
                {
                    class B
                    {
                        void M1() {}
                        int M2() { throw null; }
                        abstract int M3;
                    }
                }
                """);
        }

        [Fact]
        public void TestMethodDeclarationWithExceptionMessage()
        {
            CompareSyntaxTree(new BodyBlockCSharpSyntaxRewriter("Not implemented"),
                original: """
                namespace A
                {
                    class B
                    {
                        void M1() {}
                        int M2() { return 1; }
                        abstract int M3;
                    }
                }
                """,
                expected: """
                namespace A
                {
                    class B
                    {
                        void M1() {}
                        int M2() { throw new PlatformNotSupportedException("Not implemented"); }
                        abstract int M3;
                    }
                }
                """);
        }

        [Fact]
        public void TestPropertyDeclaration()
        {
            CompareSyntaxTree(new BodyBlockCSharpSyntaxRewriter(null),
                original: """
                namespace A
                {
                    class B
                    {
                        int P1 { get; set; }
                        int P2 { get; }
                        int P3 { set; }
                    }
                }
                """,
                expected: """
                namespace A
                {
                    class B
                    {
                        int P1 { get { throw null; } set { } }
                        int P2 { get { throw null; } }
                        int P3 { set { } }
                    }
                }
                """);
        }

        [Fact]
        public void TestPropertyDeclarationWithExceptionMessage()
        {
            CompareSyntaxTree(new BodyBlockCSharpSyntaxRewriter("Not implemented"),
                original: """
                namespace A
                {
                    class B
                    {
                        int P1 { get; set; }
                        int P2 { get; }
                        int P3 { set; }
                    }
                }
                """,
                expected: """
                namespace A
                {
                    class B
                    {
                        int P1 { get { throw new PlatformNotSupportedException("Not implemented"); } set { } }
                        int P2 { get { throw new PlatformNotSupportedException("Not implemented"); } }
                        int P3 { set { } }
                    }
                }
                """);
        }

        [Fact]
        public void TestCustomOperatorDeclaration()
        {
            CompareSyntaxTree(new BodyBlockCSharpSyntaxRewriter(null),
                original: """
                namespace A
                {
                    class B
                    {
                        public static bool operator ==(B lhs, B rhs) { return true; }
                    }
                }
                """,
                expected: """
                namespace A
                {
                    class B
                    {
                        public static bool operator ==(B lhs, B rhs) { throw null; }
                    }
                }
                """);
        }

        [Fact]
        public void TestCustomOperatorDeclarationWithExceptionMessage()
        {
            CompareSyntaxTree(new BodyBlockCSharpSyntaxRewriter("Not implemented"),
                original: """
                namespace A
                {
                    class B
                    {
                        public static bool operator ==(B lhs, B rhs) { return true; }
                    }
                }
                """,
                expected: """
                namespace A
                {
                    class B
                    {
                        public static bool operator ==(B lhs, B rhs) { throw new PlatformNotSupportedException("Not implemented"); }
                    }
                }
                """);
        }

        [Fact]
        public void TestConstructorWithReferenceParameters()
        {
            CompareSyntaxTree(new BodyBlockCSharpSyntaxRewriter("Not implemented"),
                original: """
                namespace A
                {
                    public class Foo
                    {
                        public Foo(int a) { }
                        public Foo(int a, out int b) { b = 1; }
                    }
                }
                """,
                expected: """
                namespace A
                {
                    public class Foo
                    {
                        public Foo(int a) { }
                        public Foo(int a, out int b) { throw new PlatformNotSupportedException("Not implemented"); }
                    }
                }
                """);
        }
    }
}
