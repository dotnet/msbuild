// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using LocalizableStrings = Microsoft.DotNet.Tools.New.LocalizableStrings;

namespace Microsoft.TemplateEngine.MSBuildEvaluation
{
    /// <remarks>
    /// As implemented in: https://docs.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.shell.interop.vsprojectcapabilityexpressionmatcher?
    /// </remarks>
    internal class CapabilityExpressionEvaluator
    {
        /// <summary>
        /// The set of terms that are present.
        /// </summary>
        private readonly IReadOnlyList<string> _presentTerms;

        /// <summary>
        /// The tokenizer that reads the expression.
        /// </summary>
        private readonly Tokenizer _tokenizer;

        /// <summary>
        /// The set of disallowed characters in terms.
        /// </summary>
        /// <remarks>
        /// We restrict many symbols, especially mathematical symbols, because we may eventually want to 
        /// support arithmetic expressions.
        /// </remarks>
        internal static readonly char[] DisallowedCharacters = "\"'`:;,+-*/\\!~|&%$@^()={}[]<>? \t\b\n\r".ToCharArray();

        /// <summary>
        /// Initializes a new instance of the <see cref="CapabilityExpressionEvaluator"/> class.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="presentTerms">The present terms.</param>
        private CapabilityExpressionEvaluator(string expression, IReadOnlyList<string> presentTerms)
        {
            _tokenizer = new Tokenizer(expression);
            _presentTerms = presentTerms ?? throw new ArgumentNullException(nameof(presentTerms));
        }

        /// <summary>
        /// Evaluates the given expression against the given set of true terms. Missing terms are assumed to be false.
        /// </summary>
        /// <param name="expression">
        /// The expression, such as "(VisualC | CSharp) + (MSTest | NUnit)".  
        /// The '|' is the OR operator.
        /// The '&' and '+' characters are both AND operators.
        /// The '!' character is the NOT operator.
        /// Parentheses force evaluation precedence order.
        /// A null or empty expression is evaluated as true.
        /// </param>
        /// <param name="presentTerms">The terms that are currently defined.</param>
        /// <returns>The result of evaluating the Boolean expression.</returns>
        public static bool Evaluate(string expression, IReadOnlyList<string> presentTerms)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                // An empty expression evaluates to true.
                return true;
            }

            var eval = new CapabilityExpressionEvaluator(expression, presentTerms);
            return eval.Top();
        }

        /// <summary>
        /// Checks whether a given character is an allowed member of a term.
        /// </summary>
        /// <param name="ch">The character to test.</param>
        /// <returns>true if the character would be an allowed member of a term; false otherwise.</returns>
        private static bool IsSymbolCharacter(char ch)
        {
            return !DisallowedCharacters.Contains(ch);
        }

        /// <summary>
        /// Processes | operators.
        /// </summary>
        /// <returns>The result of evaluating the current sub-expression.</returns>
        private bool OrTerm()
        {
            bool lhs = AndTerm();
            while (_tokenizer.Peek() == "|")
            {
                _tokenizer.Next();
                bool rhs = AndTerm();
                lhs = lhs || rhs;
            }

            return lhs;
        }

        /// <summary>
        /// Processes &amp; operators.
        /// </summary>
        /// <returns>The result of evaluating the current sub-expression.</returns>
        private bool AndTerm()
        {
            bool lhs = Term();
            while (_tokenizer.Peek() == "&")
            {
                _tokenizer.Next();
                bool rhs = Term();
                lhs = lhs && rhs;
            }

            return lhs;
        }

        /// <summary>
        /// Processes terms.
        /// </summary>
        /// <returns>The result of evaluating the current sub-expression.</returns>
        private bool Term()
        {
            int notCount = 0;
            while (_tokenizer.Peek() == "!")
            {
                _tokenizer.Next();
                notCount++;
            }

            if (_tokenizer.Peek() == "(")
            {
                _tokenizer.Next();
                bool r = OrTerm();
                if (_tokenizer.Peek() != ")")
                {
                    throw _tokenizer.CreateInvalidExpressionException();
                }
                _tokenizer.Next();
                return (notCount % 2 == 0) ? r : !r;
            }
            else if (_tokenizer.Peek() != null && IsSymbolCharacter(_tokenizer.Peek()![0]))
            {
                string? ident = _tokenizer.Next();
                bool isPresent = _presentTerms.Contains(ident, StringComparer.OrdinalIgnoreCase);
                return (notCount % 2 == 0) ? isPresent : !isPresent;
            }
            else
            {
                throw _tokenizer.CreateInvalidExpressionException();
            }
        }

        /// <summary>
        /// Processes the entire expression.
        /// </summary>
        /// <returns>The result of evaluating the expression.</returns>
        private bool Top()
        {
            bool r = OrTerm();
            if (_tokenizer.Peek() != null)
            {
                throw _tokenizer.CreateInvalidExpressionException(_tokenizer.Input.Length);
            }

            return r;
        }

        /// <summary>
        /// The expression tokenizer.
        /// </summary>
        /// <devremarks>
        /// This is a struct rather than a class to avoid allocating memory unnecessarily.
        /// </devremarks>
        private class Tokenizer
        {
            /// <summary>
            /// The most recently previewed token.
            /// </summary>
            private string? _peeked;

            /// <summary>
            /// Initializes a new instance of the <see cref="Tokenizer"/> class.
            /// </summary>
            /// <param name="input">The expression to parse.</param>
            internal Tokenizer(string input)
            {
                if (string.IsNullOrEmpty(input))
                {
                    throw new ArgumentException($"'{nameof(input)}' cannot be null or empty.", nameof(input));
                }
                Input = input;
            }

            /// <summary>
            /// Gets the entire expression being tokenized.
            /// </summary>
            internal string Input { get; }

            /// <summary>
            /// Gets the position of the next token.
            /// </summary>
            internal int Position { get; private set; }

            /// <summary>
            /// Gets the next token in the expression.
            /// </summary>
            internal string? Next()
            {
                // If the last call to Next() was within a Peek() method call,
                // we need to return the same value again this time so that
                // the Peek() doesn't impact the token stream.
                if (_peeked != null)
                {
                    string token = _peeked;
                    _peeked = null;
                    return token;
                }

                // Skip whitespace.
                while (Position < Input.Length && char.IsWhiteSpace(Input[Position]))
                {
                    Position++;
                }

                if (Position == Input.Length)
                {
                    return null;
                }

                switch (Input[Position])
                {
                    case char sym when IsSymbolCharacter(sym):
                        int begin = Position;
                        while (Position < Input.Length && IsSymbolCharacter(Input[Position]))
                        {
                            Position++;
                        }
                        int end = Position;
                        return Input.Substring(begin, end - begin);
                    // we prefer & but also accept + so that XML manifest files don't have to write the &amp; escape sequence.
                    case '&':
                    case '+':
                        Position++;
                        return "&";  // always return '&' to simplify the parser logic by consolidating on only one of the two possible operators.
                    case '|':
                        Position++;
                        return "|";
                    case '(':
                        Position++;
                        return "(";
                    case ')':
                        Position++;
                        return ")";
                    case '!':
                        Position++;
                        return "!";
                    default: throw CreateInvalidExpressionException(Position);
                }
            }

            /// <summary>
            /// Peeks at the next token in the stream without skipping it on
            /// the next invocation of <see cref="Next"/>.
            /// </summary>
            internal string? Peek()
            {
                return _peeked = Next();
            }

            /// <summary>
            /// Create an exception indicating that the expression is invalid and reporting the current position.
            /// </summary>
            /// <param name="position">The position in the expression where the error was detected.</param>
            /// <returns>An exception for diagnosing the invalid expression.</returns>
            internal Exception CreateInvalidExpressionException()
            {
                return CreateInvalidExpressionException(Position);
            }

            /// <summary>
            /// Create an exception indicating that the expression is invalid and reporting the given position.
            /// </summary>
            /// <param name="position">The position in the expression where the error was detected.</param>
            /// <returns>An exception for diagnosing the invalid expression.</returns>
            internal Exception CreateInvalidExpressionException(int position)
            {
                return new ArgumentException(
                    string.Format(LocalizableStrings.CapabilityExpressionEvaluator_Exception_InvalidExpression, position),
                    "expression");
            }
        }
    }
}
