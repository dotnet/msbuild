// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.IO;

#nullable disable

namespace Microsoft.Build.Shared.LanguageParser
{
    /*
     * Class:   VisualBasicTokenizer
     *
     * Given vb sources, return an enumerator that will provide tokens one at a time.
     *
     */
    internal sealed class VisualBasicTokenizer : IEnumerable
    {
        /*
            These are the tokens that are specific to the VB tokenizer.
            Tokens that should be shared with other tokenizers should go
            into Token.cs.
        */
        internal class LineTerminatorToken : Token { }
        internal class SeparatorToken : Token { }

        internal class LineContinuationToken : WhitespaceToken { }

        internal class OctalIntegerLiteralToken : IntegerLiteralToken { }

        internal class ExpectedValidOctalDigitToken : SyntaxErrorToken { }

        // The source lines
        private Stream _binaryStream = null;

        // Whether or not to force ANSI reading.
        private bool _forceANSI;

        /*
         * Method:  VisualBasicTokenizer
         *
         * Construct
         */
        internal VisualBasicTokenizer(Stream binaryStream, bool forceANSI)
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
            return new VisualBasicTokenEnumerator(_binaryStream, _forceANSI);
        }
    }
}
