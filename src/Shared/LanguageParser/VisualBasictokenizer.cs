// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using System.Resources;
using System.Reflection;
using System.Collections;
using System.Globalization;

namespace Microsoft.Build.Shared.LanguageParser
{
    /*
     * Class:   VisualBasicTokenizer
     *
     * Given vb sources, return an enumerator that will provide tokens one at a time.
     *
     */
    sealed internal class VisualBasicTokenizer : IEnumerable
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
