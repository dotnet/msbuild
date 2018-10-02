// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System;
using System.Diagnostics;

using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Class:       Scanner
    /// This class does the scanning of the input and returns tokens.
    /// The usage pattern is:
    ///    Scanner s = new Scanner(expression, CultureInfo)
    ///    do {
    ///      s.Advance();
    ///    while (s.IsNext(Token.EndOfInput));
    /// 
    ///  After Advance() is called, you can get the current token (s.CurrentToken),
    ///  check it's type (s.IsNext()), get the string for it (s.NextString()).
    /// </summary>
    internal sealed class Scanner
    {
        private string expression;
        private int parsePoint;
        private Token lookahead;
        private bool errorState;
        private int errorPosition;
        // What we found instead of what we were looking for
        private string unexpectedlyFound = null; 
        private ParserOptions options;
        private string errorResource = null;
        
        // Shared instances of "hardcoded" token strings. These are only used 
        // in error messages.
        private const string comma = ",";
        private const string leftParenthesis = "(";
        private const string rightParenthesis = ")";
        private const string lessThan = "<";
        private const string greaterThan = ">";
        private const string lessThanOrEqualTo = "<=";
        private const string greaterThanOrEqualTo = ">=";
        private const string equalTo = "==";
        private const string notEqualTo = "!=";
        private const string not = "!";
        private static string endOfInput = null;

        /// <summary>
        /// Lazily format resource string to help avoid (in some perf critical cases) even loading
        /// resources at all.
        /// </summary>
        private string EndOfInput
        {
            get
            {
                if (endOfInput == null)
                {
                    endOfInput = ResourceUtilities.FormatResourceString("EndOfInputTokenName");
                }

                return endOfInput;
            }
        }

        private Scanner() { }
        //
        // Constructor takes the string to parse and the culture.
        //
        internal Scanner(string expressionToParse, ParserOptions options)
        {
            // We currently have no support (and no scenarios) for disallowing property references
            // in Conditions.
            ErrorUtilities.VerifyThrow(0 != (options & ParserOptions.AllowProperties),
                "Properties should always be allowed.");

            this.expression = expressionToParse;
            this.parsePoint = 0;
            this.errorState = false;
            this.errorPosition = -1; // invalid
            this.options = options;
        }

        /// <summary>
        /// If the lexer errors, it has the best knowledge of the error message to show. For example,
        /// 'unexpected character' or 'illformed operator'. This method returns the name of the resource
        /// string that the parser should display.
        /// </summary>
        /// <remarks>Intentionally not a property getter to avoid the debugger triggering the Assert dialog</remarks>
        /// <returns></returns>
        internal string GetErrorResource()
        {
            if (errorResource == null)
            {
                // I do not believe this is reachable, but provide a reasonable default.
                Debug.Assert(false, "What code path did not set an appropriate error resource? Expression: " + expression);
                unexpectedlyFound = EndOfInput;
                return "UnexpectedCharacterInCondition";
            }
            else
            {
                return errorResource;
            }
        }

        internal bool IsNext( Token.TokenType type )
        {
            return lookahead.IsToken(type);
        }

        internal string IsNextString()
        {
            return lookahead.String;
        }

        internal Token CurrentToken
        {
            get { return lookahead; }
        }

        internal int GetErrorPosition()
        {
            Debug.Assert(-1 != errorPosition); // We should have set it
            return errorPosition;
        }

        // The string (usually a single character) we found unexpectedly. 
        // We might want to show it in the error message, to help the user spot the error.
        internal string UnexpectedlyFound
        {
            get
            {
                return unexpectedlyFound;
            }
        }

        /// <summary>
        /// Advance
        /// returns true on successful advance
        ///     and false on an erroneous token
        ///
        /// Doesn't return error until the bogus input is encountered.
        /// Advance() returns true even after EndOfInput is encountered.
        /// </summary>
        internal bool Advance()
        {
            if (errorState)
                return false;

            if (lookahead != null && lookahead.IsToken(Token.TokenType.EndOfInput))
                return true;          

            SkipWhiteSpace();

            // Update error position after skipping whitespace
            errorPosition = parsePoint + 1;

            if (parsePoint >= expression.Length)
            {
                lookahead = new Token(Token.TokenType.EndOfInput, null /* end of input */);
            }
            else
            {
                switch (expression[parsePoint])
                {
                    case ',':
                        lookahead = new Token(Token.TokenType.Comma, comma);
                        parsePoint++;
                        break;
                    case '(':
                        lookahead = new Token(Token.TokenType.LeftParenthesis, leftParenthesis);
                        parsePoint++;
                        break;
                    case ')':
                        lookahead = new Token(Token.TokenType.RightParenthesis, rightParenthesis);
                        parsePoint++;
                        break;
                    case '$':
                        if (!ParseProperty())
                            return false;
                        break;
                    case '%':
                        // If the caller specified that he DOESN'T want to allow item metadata ...
                        if ((this.options & ParserOptions.AllowItemMetadata) == 0)
                        {
                            errorPosition = this.parsePoint;
                            errorState = true;
                            errorResource = "UnexpectedCharacterInCondition";
                            unexpectedlyFound = "%";
                            return false;
                        }
                        if (!ParseItemMetadata())
                            return false;
                        break;
                    case '@':
                        int start = this.parsePoint;
                        // If the caller specified that he DOESN'T want to allow item lists ...
                        if ((this.options & ParserOptions.AllowItemLists) == 0)
                        {
                            if ((parsePoint + 1) < expression.Length && expression[parsePoint + 1] == '(')
                            {
                                errorPosition = start + 1;
                                errorState = true;
                                errorResource = "ItemListNotAllowedInThisConditional";
                                return false;
                            }
                        }
                        if (!ParseItemList())
                            return false;
                        break;
                    case '!':
                        // negation and not-equal
                        if ((parsePoint + 1) < expression.Length && expression[parsePoint + 1] == '=')
                        {
                            lookahead = new Token(Token.TokenType.NotEqualTo, notEqualTo);
                            parsePoint += 2;
                        }
                        else
                        {
                            lookahead = new Token(Token.TokenType.Not, not);
                            parsePoint++;
                        }
                        break;
                    case '>':
                        // gt and gte
                        if ((parsePoint + 1) < expression.Length && expression[parsePoint + 1] == '=')
                        {
                            lookahead = new Token(Token.TokenType.GreaterThanOrEqualTo, greaterThanOrEqualTo);
                            parsePoint += 2;
                        }
                        else
                        {
                            lookahead = new Token(Token.TokenType.GreaterThan, greaterThan);
                            parsePoint++;
                        }
                        break;
                    case '<':
                        // lt and lte
                        if ((parsePoint + 1) < expression.Length && expression[parsePoint + 1] == '=')
                        {
                            lookahead = new Token(Token.TokenType.LessThanOrEqualTo, lessThanOrEqualTo);
                            parsePoint += 2;
                        }
                        else
                        {
                            lookahead = new Token(Token.TokenType.LessThan, lessThan);
                            parsePoint++;
                        }
                        break;
                    case '=':
                        if ((parsePoint + 1) < expression.Length && expression[parsePoint + 1] == '=')
                        {
                            lookahead = new Token(Token.TokenType.EqualTo, equalTo);
                            parsePoint += 2;
                        }
                        else
                        {
                            errorPosition = parsePoint + 2; // expression[parsePoint + 1], counting from 1
                            errorResource = "IllFormedEqualsInCondition";
                            if ((parsePoint + 1) < expression.Length)
                            {
                                // store the char we found instead
                                unexpectedlyFound = Convert.ToString(expression[parsePoint + 1], CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                unexpectedlyFound = EndOfInput;
                            }
                            parsePoint++;
                            errorState = true;
                            return false;
                        }
                        break;
                    case '\'':
                        if (!ParseQuotedString())
                            return false;
                        break;
                    default:
                        // Simple strings, function calls, decimal numbers, hex numbers
                        if (!ParseRemaining())
                            return false;
                        break;
                }
            }
            return true;
        }

        /// <summary>
        /// Parses either the $(propertyname) syntax or the %(metadataname) syntax, 
        /// and returns the parsed string beginning with the '$' or '%', and ending with the
        /// closing parenthesis.
        /// </summary>
        /// <returns></returns>
        /// <owner>RGoel, DavidLe</owner>
        private string ParsePropertyOrItemMetadata()
        {
            int start = parsePoint; // set start so that we include "$(" or "%("
            parsePoint++;

            if (parsePoint < expression.Length && expression[parsePoint] != '(')
            {
                errorState = true;
                errorPosition = start + 1;
                errorResource = "IllFormedPropertyOpenParenthesisInCondition";
                unexpectedlyFound = Convert.ToString(expression[parsePoint], CultureInfo.InvariantCulture);
                return null;
            }

            parsePoint = ScanForPropertyExpressionEnd(expression, parsePoint++);

            // Maybe we need to generate an error for invalid characters in property/metadata name?
            // For now, just wait and let the property/metadata evaluation handle the error case.

            if (parsePoint >= expression.Length)
            {
                errorState = true;
                errorPosition = start + 1;
                errorResource = "IllFormedPropertyCloseParenthesisInCondition";
                unexpectedlyFound = EndOfInput;
                return null;
            }

            parsePoint++;
            return expression.Substring(start, parsePoint - start);
        }

        /// <summary>
        /// Scan for the end of the property expression
        /// </summary>
        private static int ScanForPropertyExpressionEnd(string expression, int index)
        {
            int nestLevel = 0;

            while (index < expression.Length)
            {
                if (expression[index] == '(')
                {
                    nestLevel++;
                }
                else if (expression[index] == ')')
                {
                    nestLevel--;
                }

                // We have reached the end of the parenthesis nesting
                // this should be the end of the property expression
                // If it is not then the calling code will determine that
                if (nestLevel == 0)
                {
                    return index;
                }
                else
                {
                    index++;
                }
            }

            return index;
        }

        /// <summary>
        /// Parses a string of the form $(propertyname).
        /// </summary>
        /// <returns></returns>
        /// <owner>RGoel, DavidLe</owner>
        private bool ParseProperty()
        {
            string propertyExpression = this.ParsePropertyOrItemMetadata();

            if (propertyExpression == null)
            {
                return false;
            }
            else
            {
                this.lookahead = new Token(Token.TokenType.Property, propertyExpression);
                return true;
            }
        }

        /// <summary>
        /// Parses a string of the form %(itemmetadataname).
        /// </summary>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        private bool ParseItemMetadata()
        {
            string itemMetadataExpression = this.ParsePropertyOrItemMetadata();

            if (itemMetadataExpression == null)
            {
                // The ParsePropertyOrItemMetadata method returns the correct error resources
                // for parsing properties such as $(propertyname).  At this stage in the Whidbey
                // cycle, we're not allowed to add new string resources, so I can't add a new
                // resource specific to item metadata, so here, we just change the error to
                // the generic "UnexpectedCharacter".
                errorResource = "UnexpectedCharacterInCondition";
                return false;
            }
            else
            {
                this.lookahead = new Token(Token.TokenType.ItemMetadata, itemMetadataExpression);
                return true;
            }
        }

        private bool ParseInternalItemList()
        {
            int start = parsePoint;
            parsePoint++;

            if (parsePoint < expression.Length && expression[parsePoint] != '(')
            {
                // @ was not followed by (
                errorPosition = start + 1;
                errorResource = "IllFormedItemListOpenParenthesisInCondition";
                // Not useful to set unexpectedlyFound here. The message is going to be detailed enough.
                errorState = true;
                return false;
            }
            parsePoint++;
            // Maybe we need to generate an error for invalid characters in itemgroup name?
            // For now, just let item evaluation handle the error.
            bool fInReplacement = false;
            while (parsePoint < expression.Length)
            {
                if (expression[parsePoint] == '\'')
                {
                    fInReplacement = !fInReplacement;
                }
                else if (expression[parsePoint] == ')' && !fInReplacement)
                {
                    break;
                }
                parsePoint++;
            }
            if (parsePoint >= expression.Length)
            {
                
                errorPosition = start + 1;
                if (fInReplacement)
                {
                    // @( ... ' was never followed by a closing quote before the closing parenthesis
                    errorResource = "IllFormedItemListQuoteInCondition";
                }
                else
                {
                    // @( was never followed by a )
                    errorResource = "IllFormedItemListCloseParenthesisInCondition";
                }
                // Not useful to set unexpectedlyFound here. The message is going to be detailed enough.
                errorState = true;
                return false;
            }
            parsePoint++;
            return true;
        }

        private bool ParseItemList()
        {
            int start = parsePoint;
            if (!ParseInternalItemList())
            {
                return false;
            }
            lookahead = new Token(Token.TokenType.ItemList, expression.Substring(start, parsePoint - start));
            return true;
        }

        private bool ParseQuotedString()
        {
            parsePoint++;
            int start = parsePoint;
            while (parsePoint < expression.Length && expression[parsePoint] != '\'')
            {
                // Standalone percent-sign must be allowed within a condition because it's
                // needed to escape special characters.  However, percent-sign followed
                // by open-parenthesis is an indication of an item metadata reference, and
                // that is only allowed in certain contexts.
                if ((expression[parsePoint] == '%') && ((parsePoint + 1) < expression.Length) && (expression[parsePoint + 1] == '('))
                {
                    // If the caller specified that he DOESN'T want to allow item metadata...
                    if ((this.options & ParserOptions.AllowItemMetadata) == 0)
                    {
                        errorPosition = start + 1;
                        errorState = true;
                        errorResource = "UnexpectedCharacterInCondition";
                        unexpectedlyFound = "%";
                        return false;
                    }
                }
                else if (expression[parsePoint] == '@' && ((parsePoint + 1) < expression.Length) && (expression[parsePoint + 1] == '('))
                {
                    // If the caller specified that he DOESN'T want to allow item lists ...
                    if ((this.options & ParserOptions.AllowItemLists) == 0)
                    {
                        errorPosition = start + 1;
                        errorState = true;
                        errorResource = "ItemListNotAllowedInThisConditional";
                        return false;
                    }

                    // Item lists have to be parsed because of the replacement syntax e.g. @(Foo,'_').
                    // I have to know how to parse those so I can skip over the tic marks.  I don't
                    // have to do that with other things like propertygroups, hence itemlists are
                    // treated specially.

                    ParseInternalItemList();
                    continue;
                }
                parsePoint++;
            }
            if (parsePoint >= expression.Length)
            {
                // Quoted string wasn't closed
                errorState = true;
                errorPosition = start; // The message is going to say "expected after position n" so don't add 1 here.
                errorResource = "IllFormedQuotedStringInCondition";
                // Not useful to set unexpectedlyFound here. By definition it got to the end of the string.
                return false;
            }
            string originalTokenString = expression.Substring(start, parsePoint - start);

            lookahead = new Token(Token.TokenType.String, originalTokenString);
            parsePoint++;
            return true;
        }

        private bool ParseRemaining()
        {
            int start = parsePoint;
            if (CharacterUtilities.IsNumberStart(expression[parsePoint])) // numeric
            {
                if (!ParseNumeric(start))
                    return false;
            }
            else if (CharacterUtilities.IsSimpleStringStart(expression[parsePoint])) // simple string (handle 'and' and 'or')
            {
                if (!ParseSimpleStringOrFunction(start))
                    return false;
            }
            else
            {
                // Something that wasn't a number or a letter, like a newline (%0a)
                errorState = true;
                errorPosition = start + 1;
                errorResource = "UnexpectedCharacterInCondition";
                unexpectedlyFound = Convert.ToString(expression[parsePoint], CultureInfo.InvariantCulture);
                return false;
            }
            return true;
        }
        private bool ParseSimpleStringOrFunction( int start )
        {
            SkipSimpleStringChars();
            if (0 == string.Compare(expression.Substring(start, parsePoint - start), "and", StringComparison.OrdinalIgnoreCase))
            {
                lookahead = new Token(Token.TokenType.And, expression.Substring(start, parsePoint - start));
            }
            else if (0 == string.Compare(expression.Substring(start, parsePoint - start), "or", StringComparison.OrdinalIgnoreCase))
            {
                lookahead = new Token(Token.TokenType.Or, expression.Substring(start, parsePoint - start));
            }
            else
            {
                int end = parsePoint;
                SkipWhiteSpace();
                if (parsePoint < expression.Length && expression[parsePoint] == '(')
                {
                    lookahead = new Token(Token.TokenType.Function, expression.Substring(start, end - start));
                }
                else
                {
                    string tokenValue = expression.Substring(start, end - start);
                    lookahead = new Token(Token.TokenType.String, tokenValue);
                }
            }
            return true;
        }
        private bool ParseNumeric( int start )
        {
            if ((expression.Length-parsePoint) > 2 && expression[parsePoint] == '0' && (expression[parsePoint + 1] == 'x' || expression[parsePoint + 1] == 'X'))
            {
                // Hex number
                parsePoint += 2;
                SkipHexDigits();
                lookahead = new Token(Token.TokenType.Numeric, expression.Substring(start, parsePoint - start));
            }
            else if ( CharacterUtilities.IsNumberStart(expression[parsePoint]))
            {
                // Decimal number
                if (expression[parsePoint] == '+')
                {
                    parsePoint++;
                }
                else if (expression[parsePoint] == '-')
                {
                    parsePoint++;
                }
                SkipDigits();
                if (parsePoint < expression.Length && expression[parsePoint] == '.')
                {
                    parsePoint++;
                }
                if (parsePoint < expression.Length)
                {
                    SkipDigits();
                }
                // Do we need to error on malformed input like 0.00.00)? or will the conversion handle it?
                // For now, let the conversion generate the error.
                lookahead = new Token(Token.TokenType.Numeric, expression.Substring(start, parsePoint - start));
            }
            else
            {
                // Unreachable
                errorState = true;
                errorPosition = start + 1;
                return false;
            }
            return true;
        }
        private void SkipWhiteSpace()
        {
            while (parsePoint < expression.Length && char.IsWhiteSpace(expression[parsePoint]))
                parsePoint++;
            return;
        }
        private void SkipDigits()
        {
            while (parsePoint < expression.Length && char.IsDigit(expression[parsePoint]))
                parsePoint++;
            return;
        }
        private void SkipHexDigits()
        {
            while (parsePoint < expression.Length && CharacterUtilities.IsHexDigit(expression[parsePoint]))
                parsePoint++;
            return;
        }
        private void SkipSimpleStringChars()
        {
            while (parsePoint < expression.Length && CharacterUtilities.IsSimpleStringChar(expression[parsePoint]))
                parsePoint++;
            return;
        }
    }
}
