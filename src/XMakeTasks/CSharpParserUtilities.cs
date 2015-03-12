// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using System.Resources;
using System.Reflection;

using System.Collections;
using Microsoft.Build.Shared.LanguageParser;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Specific-purpose utility functions for parsing C#.
    /// </summary>
    internal static class CSharpParserUtilities
    {
        /// <summary>
        /// Parse a C# file and get the first class name, fully qualified with namespace.
        /// </summary>
        /// <param name="binaryStream"></param>
        /// <returns></returns>
        static internal ExtractedClassName GetFirstClassNameFullyQualified(Stream binaryStream)
        {
            try
            {
                CSharpTokenizer tokens = new CSharpTokenizer(binaryStream, /* forceANSI */ false);
                return Extract(tokens);
            }
            catch (DecoderFallbackException)
            {
                // There was no BOM and there are non UTF8 sequences. Fall back to ANSI.
                CSharpTokenizer tokens = new CSharpTokenizer(binaryStream, /* forceANSI */ true);
                return Extract(tokens);
            }
        }


        /// <summary>
        /// Extract the class name.
        /// </summary>
        /// <param name="tokens"></param>
        /// <returns></returns>
        private static ExtractedClassName Extract(CSharpTokenizer tokens)
        {
            ParseState state = new ParseState();
            ExtractedClassName result = new ExtractedClassName();

            foreach (Token t in tokens)
            {
                // Search first for the namespace keyword
                if (t is KeywordToken)
                {
                    state.Reset();

                    if (t.InnerText == "namespace")
                    {
                        state.ResolvingNamespace = true;
                        if (state.InsideConditionalDirective)
                        {
                            result.IsInsideConditionalBlock = true;
                        }
                    }
                    else if (t.InnerText == "class")
                    {
                        state.ResolvingClass = true;
                        if (state.InsideConditionalDirective)
                        {
                            result.IsInsideConditionalBlock = true;
                        }
                    }
                }
                else if (t is CSharpTokenizer.OpenScopeToken)
                {
                    state.PushNamespacePart(state.Namespace);
                    state.Reset();
                }
                else if (t is CSharpTokenizer.CloseScopeToken)
                {
                    state.Reset();
                    state.PopNamespacePart();
                }
                else if (t is OperatorOrPunctuatorToken)
                {
                    if (state.ResolvingNamespace)
                    {
                        if (t.InnerText == ".")
                        {
                            state.Namespace += ".";
                        }
                    }
                }
                else if (t is IdentifierToken)
                {
                    // If we're resolving a namespace, then this is part of the namespace.
                    if (state.ResolvingNamespace)
                    {
                        state.Namespace += t.InnerText;
                    }
                    // If we're resolving a class, then we're done. We found the class name.
                    else if (state.ResolvingClass)
                    {
                        // We're done.
                        result.Name = state.ComposeQualifiedClassName(t.InnerText);
                        return result;
                    }
                }
                else if (t is OpenConditionalDirectiveToken)
                {
                    state.OpenConditionalDirective();
                }
                else if (t is CloseConditionalDirectiveToken)
                {
                    state.CloseConditionalDirective();
                }
            }

            return result;
        }
    }
}
