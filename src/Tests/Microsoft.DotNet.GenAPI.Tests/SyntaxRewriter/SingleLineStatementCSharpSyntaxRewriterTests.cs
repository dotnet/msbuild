// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.DotNet.GenAPI.SyntaxRewriter;

namespace Microsoft.DotNet.GenAPI.Tests.SyntaxRewriter
{
    public class SingleLineStatementCSharpSyntaxRewriterTests : CSharpSyntaxRewriterTestBase
    {
        [Fact]
        public void TestEmptyMethodBody()
        {
            Compare(new SingleLineStatementCSharpSyntaxRewriter(),
                original: """
                namespace A
                {
                    class B
                    {
                        void Execute() {}
                    }
                }
                """,
                expected: """
                namespace A
                {
                    class B
                    {
                        void Execute() { }
                    }
                }
                """);
        }

        [Fact]
        public void TestMethodBodyWithSingleStatement()
        {
            Compare(new SingleLineStatementCSharpSyntaxRewriter(),
                original: """
                namespace A
                {
                    class B
                    {
                        int Execute() {
                            throw null;
                        }
                    }
                }
                """,
                expected: """
                namespace A
                {
                    class B
                    {
                        int Execute() { throw null; }
                    }
                }
                """);
        }

        [Fact]
        public void TestConstructorPostProcessing()
        {
            Compare(new SingleLineStatementCSharpSyntaxRewriter(),
                original: """
                namespace A
                {
                    class B
                    {
                        public B() {}
                    }
                }
                """,
                expected: """
                namespace A
                {
                    class B
                    {
                        public B() { }
                    }
                }
                """);
        }

        [Fact]
        public void TestMethodBodyWithSingleStatementInOneLine()
        {
            Compare(new SingleLineStatementCSharpSyntaxRewriter(),
                original: """
                namespace A
                {
                    class B
                    {
                        int Execute() { throw null; }
                    }
                }
                """,
                expected: """
                namespace A
                {
                    class B
                    {
                        int Execute() { throw null; }
                    }
                }
                """);
        }

        [Fact]
        public void TestPropertyPostProcessing()
        {
            Compare(new SingleLineStatementCSharpSyntaxRewriter(),
                original: """
                namespace A
                {
                    class B
                    {
                        int Property1;
                        int Property2 {    get;     set;    }
                        int Property3 {get;}
                        int Property4 {    get {}}
                    }
                }
                """,
                expected: """
                namespace A
                {
                    class B
                    {
                        int Property1;
                        int Property2 { get; set; }
                        int Property3 { get; }
                        int Property4 { get { } }
                    }
                }
                """);
        }

        [Fact]
        public void TestOperatorPostProcessing()
        {
            Compare(new SingleLineStatementCSharpSyntaxRewriter(),
                original: """
                namespace A
                {
                    class B
                    {
                        public static bool operator ==(ChildSyntaxList list1, ChildSyntaxList list2) {
                        throw null;
                        }
                    }
                }
                """,
                expected: """
                namespace A
                {
                    class B
                    {
                        public static bool operator ==(ChildSyntaxList list1, ChildSyntaxList list2) { throw null; }
                    }
                }
                """);
        }

        [Fact]
        public void TestConversionOperatorPostProcessing()
        {
            Compare(new SingleLineStatementCSharpSyntaxRewriter(),
                original: """
                    namespace Foo
                    {
                        public readonly struct Digit
                        {
                            public static implicit operator byte(Digit d) {
                            throw null;
                            }
                            public static explicit operator Digit(byte b) => throw null;
                        }
                    }
                    """,
                expected: """
                    namespace Foo
                    {
                        public readonly struct Digit
                        {
                            public static implicit operator byte(Digit d) { throw null; }
                            public static explicit operator Digit(byte b) => throw null;
                        }
                    }
                    """);
        }
    }
}
