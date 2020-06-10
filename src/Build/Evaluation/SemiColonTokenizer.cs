// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Splits an expression into fragments at semicolons, except where the
    /// semicolons are in a macro or separator expression.
    /// Fragments are trimmed and empty fragments discarded.
    /// </summary>
    /// <remarks>
    /// These complex cases prevent us from doing a simple split on ';':
    ///  (1) Macro expression: @(foo->'xxx;xxx')
    ///  (2) Separator expression: @(foo, 'xxx;xxx')
    ///  (3) Combination: @(foo->'xxx;xxx', 'xxx;xxx')
    ///  We must not split on semicolons in macro or separator expressions like these.
    /// </remarks>
    internal struct SemiColonTokenizer : IEnumerable<string>
    {
        private readonly string _expression;

        public SemiColonTokenizer(string expression)
        {
            _expression = expression;
        }

        public Enumerator GetEnumerator() => new Enumerator(_expression);

        IEnumerator<string> IEnumerable<string>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        internal struct Enumerator : IEnumerator<string>
        {
            private readonly string _expression;
            private string _current;
            private int _index;

            public Enumerator(string expression)
            {
                _expression = expression;
                _index = 0;
                _current = default(string);
            }

            public string Current
            {
                get { return _current; }
            }

            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                int segmentStart = _index;
                bool insideItemList = false;
                bool insideQuotedPart = false;
                string segment;

                // Walk along the string, keeping track of whether we are in an item list expression.
                // If we hit a semi-colon or the end of the string and we aren't in an item list, 
                // add the segment to the list.
                for (; _index < _expression.Length; _index++)
                {
                    switch (_expression[_index])
                    {
                        case ';':
                            if (!insideItemList)
                            {
                                // End of segment, so add it to the list
                                segment = GetExpressionSubstring(segmentStart, _index - segmentStart);
                                if (segment != null)
                                {
                                    _current = segment;
                                    return true;
                                }

                                // Move past this semicolon
                                segmentStart = _index + 1;
                            }

                            break;
                        case '@':
                            // An '@' immediately followed by a '(' is the start of an item list
                            if (_expression.Length > _index + 1 && _expression[_index + 1] == '(')
                            {
                                // Start of item expression
                                insideItemList = true;
                            }

                            break;
                        case ')':
                            if (insideItemList && !insideQuotedPart)
                            {
                                // End of item expression
                                insideItemList = false;
                            }

                            break;
                        case '\'':
                            if (insideItemList)
                            {
                                // Start or end of quoted expression in item expression
                                insideQuotedPart = !insideQuotedPart;
                            }

                            break;
                    }
                }

                // Reached the end of the string: what's left is another segment
                _current = GetExpressionSubstring(segmentStart, _expression.Length - segmentStart);
                return _current != null;
            }

            public void Reset()
            {
                _current = default(string);
                _index = 0;
            }

            /// <summary>
            /// Returns a whitespace-trimmed and possibly interned substring of the expression.
            /// </summary>
            /// <param name="startIndex">Start index of the substring.</param>
            /// <param name="length">Length of the substring.</param>
            /// <returns>Equivalent to _expression.Substring(startIndex, length).Trim() or null if the trimmed substring is empty.</returns>
            private string GetExpressionSubstring(int startIndex, int length)
            {
                int endIndex = startIndex + length;
                while (startIndex < endIndex && char.IsWhiteSpace(_expression[startIndex]))
                {
                    startIndex++;
                }
                while (startIndex < endIndex && char.IsWhiteSpace(_expression[endIndex - 1]))
                {
                    endIndex--;
                }
                if (startIndex < endIndex)
                {
                    var target = new SubstringInternTarget(_expression, startIndex, endIndex - startIndex);
                    return OpportunisticIntern.InternableToString(target);
                }
                return null;
            }
        }
    }
}
