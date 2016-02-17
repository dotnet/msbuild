// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace Microsoft.DotNet.Tools.Compiler
{
    public class PPFileTokenizer
    {
        private readonly string _text;
        private int _index;

        public PPFileTokenizer(string text)
        {
            _text = text;
            _index = 0;
        }

        /// <summary>
        /// Gets the next token.
        /// </summary>
        /// <returns>The parsed token. Or null if no more tokens are available.</returns>
        public Token Read()
        {
            if (_index >= _text.Length)
            {
                return null;
            }

            if (_text[_index] == '$')
            {
                _index++;
                return ParseTokenAfterDollarSign();
            }
            return ParseText();
        }

        private static bool IsWordChar(char ch)
        {
            // See http://msdn.microsoft.com/en-us/library/20bw873z.aspx#WordCharacter
            var c = CharUnicodeInfo.GetUnicodeCategory(ch);
            return c == UnicodeCategory.LowercaseLetter ||
                   c == UnicodeCategory.UppercaseLetter ||
                   c == UnicodeCategory.TitlecaseLetter ||
                   c == UnicodeCategory.OtherLetter ||
                   c == UnicodeCategory.ModifierLetter ||
                   c == UnicodeCategory.DecimalDigitNumber ||
                   c == UnicodeCategory.ConnectorPunctuation;
        }

        // Parses and returns the next token after a $ is just read.
        // _index is one char after the $.
        private Token ParseTokenAfterDollarSign()
        {
            var sb = new StringBuilder();
            while (_index < _text.Length)
            {
                var ch = _text[_index];
                if (ch == '$')
                {
                    ++_index;
                    if (sb.Length == 0)
                    {
                        // escape sequence "$$" is encountered
                        return new Token(TokenCategory.Text, "$");
                    }
                    // matching $ is read. So the token is a variable.
                    return new Token(TokenCategory.Variable, sb.ToString());
                }
                if (IsWordChar(ch))
                {
                    sb.Append(ch);
                    ++_index;
                }
                else
                {
                    // non word char encountered. So the current token
                    // is not a variable after all.
                    sb.Insert(0, '$');
                    sb.Append(ch);
                    ++_index;
                    return new Token(TokenCategory.Text, sb.ToString());
                }
            }

            // no matching $ is found and the end of text is reached.
            // So the current token is a text.
            sb.Insert(0, '$');
            return new Token(TokenCategory.Text, sb.ToString());
        }

        private Token ParseText()
        {
            var sb = new StringBuilder();
            while (_index < _text.Length
                   && _text[_index] != '$')
            {
                sb.Append(_text[_index]);
                _index++;
            }

            return new Token(TokenCategory.Text, sb.ToString());
        }

        public class Token
        {
            public string Value { get; private set; }
            public TokenCategory Category { get; private set; }

            public Token(TokenCategory category, string value)
            {
                Category = category;
                Value = value;
            }
        }

        public enum TokenCategory
        {
            Text,
            Variable
        }
    }
}
