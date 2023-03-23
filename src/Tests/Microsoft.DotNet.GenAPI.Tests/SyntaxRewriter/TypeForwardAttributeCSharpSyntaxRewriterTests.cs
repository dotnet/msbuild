// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using Microsoft.DotNet.GenAPI.SyntaxRewriter;
using Xunit;

namespace Microsoft.DotNet.GenAPI.Tests.SyntaxRewriter
{
    public class TypeForwardAttributeCSharpSyntaxRewriterTests : CSharpSyntaxRewriterTestBase
    {
        [Fact]
        public void TypeForwardAttributeDeclaration()
        {
            CompareSyntaxTree(new TypeForwardAttributeCSharpSyntaxRewriter(),
                original: """
                [assembly:System.Runtime.CompilerServices.TypeForwardedToAttribute(typeof(global::System.Collections.Generic.IAsyncEnumerable<T>))]
                [assembly:System.Runtime.CompilerServices.TypeForwardedToAttribute(typeof(global::System.Collections.Generic.IAsyncEnumerable<A, B, C>))]
                """,
                expected: """
                [assembly:System.Runtime.CompilerServices.TypeForwardedToAttribute(typeof(global::System.Collections.Generic.IAsyncEnumerable<>))]
                [assembly:System.Runtime.CompilerServices.TypeForwardedToAttribute(typeof(global::System.Collections.Generic.IAsyncEnumerable<,,>))]
                """);
        }
    }
}
