// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class represents a token in the Complex Conditionals grammar.  It's
    /// really just a bag that contains the type of the token and the string that
    /// was parsed into the token.  This isn't very useful for operators, but
    /// is useful for strings and such.
    /// </summary>
    internal sealed class Token
    {
        /// <summary>
        /// Valid tokens
        /// </summary>
        internal enum TokenType 
        {
            Comma, LeftParenthesis, RightParenthesis,
            LessThan, GreaterThan, LessThanOrEqualTo, GreaterThanOrEqualTo,
            And, Or,
            EqualTo, NotEqualTo, Not,
            Property, String, Numeric, ItemList, ItemMetadata, Function,
            EndOfInput
        };

        private TokenType tokenType;
        private string tokenString;

        /// <summary>
        /// Constructor takes the token type and the string that
        /// represents the token
        /// </summary>
        /// <param name="type"></param>
        /// <param name="tokenString"></param>
        internal Token( TokenType type, string tokenString )
        {
            this.tokenType = type;
            this.tokenString = tokenString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        internal bool IsToken( TokenType type )
        {
            return tokenType == type;
        }

        internal TokenType Type
        {
            get { return tokenType; }
        }

        internal string String
        {
            get { return tokenString; }
        }
    }
}
