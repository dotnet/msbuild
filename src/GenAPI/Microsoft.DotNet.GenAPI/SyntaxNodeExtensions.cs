// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI
{
    internal static class SyntaxNodeExtensions
    {
        public static SyntaxNode Rewrite(this SyntaxNode node, CodeAnalysis.CSharp.CSharpSyntaxRewriter rewriter) => rewriter.Visit(node);
    }
}
