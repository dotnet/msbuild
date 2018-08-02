// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Text;

using Microsoft.Build.Shared.LanguageParser;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Specific-purpose utility functions for parsing VB.
    /// </summary>
    internal static class VisualBasicParserUtilities
    {
        /// <summary>
        /// Parse a VB file and get the first class name, fully qualified with namespace.
        /// </summary>
        /// <param name="binaryStream"></param>
        /// <returns></returns>
        internal static ExtractedClassName GetFirstClassNameFullyQualified(Stream binaryStream)
        {
            try
            {
                VisualBasicTokenizer tokens = new VisualBasicTokenizer(binaryStream, /* forceANSI */ false);
                return Extract(tokens);
            }
            catch (DecoderFallbackException)
            {
                // There was no BOM and there are non UTF8 sequences. Fall back to ANSI.
                VisualBasicTokenizer tokens = new VisualBasicTokenizer(binaryStream, /* forceANSI */ true);
                return Extract(tokens);
            }
        }

        /// <summary>
        /// Extract the class name.
        /// </summary>
        /// <param name="tokens"></param>
        /// <returns></returns>
        private static ExtractedClassName Extract(VisualBasicTokenizer tokens)
        {
            var state = new ParseState();
            var result = new ExtractedClassName();

            foreach (Token t in tokens)
            {
                // Search first for keywords that we care about.
                if (t is KeywordToken)
                {
                    state.Reset();

                    if (t.EqualsIgnoreCase("namespace"))
                    {
                        state.ResolvingNamespace = true;
                        if (state.InsideConditionalDirective)
                        {
                            result.IsInsideConditionalBlock = true;
                        }
                    }
                    else if (t.EqualsIgnoreCase("class"))
                    {
                        state.ResolvingClass = true;
                        if (state.InsideConditionalDirective)
                        {
                            result.IsInsideConditionalBlock = true;
                        }
                    }
                    else if (t.EqualsIgnoreCase("end"))
                    {
                        state.PopNamespacePart();
                    }
                }
                else if (t is VisualBasicTokenizer.LineTerminatorToken)
                {
                    if (state.ResolvingNamespace)
                    {
                        state.PushNamespacePart(state.Namespace);
                    }
                    state.Reset();
                }
                else if (t is VisualBasicTokenizer.SeparatorToken)
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
