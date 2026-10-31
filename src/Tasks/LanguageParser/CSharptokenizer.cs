// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.IO;

#nullable disable

namespace Microsoft.Build.Shared.LanguageParser
{
    /*
     * Class:   CSharpTokenizer
     *
     * Given C# sources, return an enumerator that will provide tokens one at a time.
     *
     */
    internal sealed class CSharpTokenizer : IEnumerable
    {
        /*
            These are the tokens that are specific to the C# tokenizer.
            Tokens that should be shared with other tokenizers should go
            into Token.cs.
        */
        internal class CharLiteralToken : Token { } // i.e. A char value.
        internal class NullLiteralToken : Token { } // i.e. A literal Null.

        internal class UnrecognizedStringEscapeToken : SyntaxErrorToken { } // An unrecognized string escape character was used.
        internal class EndOfFileInsideStringToken : SyntaxErrorToken { } // The file ended inside a string.
        internal class NewlineInsideStringToken : SyntaxErrorToken { } // The string has a newline embedded.
        internal class EndOfFileInsideCommentToken : SyntaxErrorToken { } // The file ended inside a multi-line comment.

        internal class OpenScopeToken : OperatorOrPunctuatorToken { } // i.e. "{"
        internal class CloseScopeToken : OperatorOrPunctuatorToken { } // i.e. "}"

        // The source lines
        private Stream _binaryStream = null;

        // Whether to force ANSI or not.
        private bool _forceANSI = false;

        /// <summary>
        /// Construct.
        /// </summary>
        /// <param name="binaryStream"></param>
        /// <param name="forceANSI"></param>
        internal CSharpTokenizer(Stream binaryStream, bool forceANSI)
        {
            _binaryStream = binaryStream;
            _forceANSI = forceANSI;
        }

        /*
         * Method:  GetEnumerator
         *
         * Return a new token enumerator.
         */
        public IEnumerator GetEnumerator()
        {
            return new CSharpTokenEnumerator(_binaryStream, _forceANSI);
        }
    }
}
