// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using Xunit;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI.Tests.SyntaxRewriter
{
    public class CSharpSyntaxRewriterTestBase
    {
        protected static void Compare(CSharpSyntaxRewriter rewriter, string original, string expected)
        {
            StringWriter _stringWriter = new();
            SyntaxNode root = CSharpSyntaxTree.ParseText(original).GetRoot();
            rewriter.Visit(root)
                .WriteTo(_stringWriter);

            StringBuilder stringBuilder = _stringWriter.GetStringBuilder();
            string resulted = stringBuilder.ToString();

            Assert.True(resulted.Equals(expected),
                $"Expected:\n{expected}\nResulted:\n{resulted}");
        }

        protected static void CompareSyntaxTree(CSharpSyntaxRewriter rewriter, string original, string expected)
        {
            StringWriter _stringWriter = new();
            SyntaxNode root = CSharpSyntaxTree.ParseText(original).GetRoot();
            rewriter.Visit(root)
                .WriteTo(_stringWriter);

            StringBuilder stringBuilder = _stringWriter.GetStringBuilder();
            string resulted = stringBuilder.ToString();

            SyntaxTree resultedSyntaxTree = CSharpSyntaxTree.ParseText(resulted);
            SyntaxTree expectedSyntaxTree = CSharpSyntaxTree.ParseText(expected);

            // compare SyntaxTree and not string representation
            Assert.True(resultedSyntaxTree.IsEquivalentTo(expectedSyntaxTree),
                $"Expected:\n{expected}\nResulted:\n{resulted}");
        }
    }
}
