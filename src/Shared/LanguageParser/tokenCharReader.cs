// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

namespace Microsoft.Build.Shared.LanguageParser
{
    /*
     * Class:   TokenCharReader
     *
     * Reads over the contents of a source file (in the form of a string). 
     * Provides utility functions for skipping and checking the value of characters.
     *
     */
    internal class TokenCharReader
    {
        // The sources 
        private StreamMappedString _sources;
        // Current character offset within sources.
        private int _position;
        // The current line. One-relative.
        private int _currentLine;    // One-relative

        /*
         * Method:  TokenCharReader
         * 
         * Construct
         */
        internal TokenCharReader(Stream binaryStream, bool forceANSI)
        {
            Reset();
            _sources = new StreamMappedString(binaryStream, forceANSI);
        }

        /*
         * Method:  Reset
         * 
         * Reset to the top of the sources.
         */
        internal void Reset()
        {
            _position = 0;
            _currentLine = 1;    // One-relative
        }

        /*
         * Method:  CurrentLine
         * 
         * The current line number   
         */
        internal int CurrentLine
        {
            get { return _currentLine; }
        }

        /*
         * Method:  Position
         * 
         * The character offset within the sources.
         */
        internal int Position
        {
            get { return _position; }
            // Having a set operator makes this class not forward-only.
            // If this becomes necessary later, then implement a push-pop
            // scheme for saving current positions and get rid of this.
            // This will force the caller to declare ahead of time whether
            // they may want to return here.
            set { _position = value; }
        }

        /*
         * Method:  Skip
         * 
         * Skip to the next character.
         */
        protected void Skip()
        {
            if (TokenChar.IsNewLine(CurrentCharacter))
            {
                ++_currentLine;
            }
            ++_position;
        }

        /* 
         * Method:  Skip (overload)
         * 
         * Skip the next n characters.
         */
        protected void Skip(int n)
        {
            for (int i = 0; i < n; ++i)
            {
                Skip();
            }
        }

        /*
         * Method:  CurrentCharacter
         * 
         * Get the current character.
         */
        internal char CurrentCharacter
        {
            get { return _sources.GetAt(_position); }
        }

        /*
         * Method:  EndOfLines
         * 
         * Return true if we've reached the end of sources.
         */
        internal bool EndOfLines
        {
            get { return _sources.IsPastEnd(_position); }
        }

        /*
         * Method:  GetCurrentMatchedString
         * 
         * Get the string that starts with the given start position and ends with this.position.
         */
        internal string GetCurrentMatchedString(int startPosition)
        {
            return _sources.Substring(startPosition, _position - startPosition);
        }

        /*
         * Method:  Sink
         * 
         * See if the next characters match the given string. If they do,
         * sink this string.
         */
        internal bool Sink(string match)
        {
            return Sink(match, false);
        }

        /// <summary>
        /// See if the next characters match the given string. If they do, sink this string.
        /// </summary>
        /// <param name="match"></param>
        /// <param name="ignoreCase"></param>
        /// <returns></returns>
        private bool Sink(string match, bool ignoreCase)
        {
            // Is there enough left for this match?
            if (_sources.IsPastEnd(_position + match.Length - 1))
            {
                return false;
            }

            string compare = _sources.Substring(_position, match.Length);

            if
            (
                String.Equals
                (
                    match,
                    compare,
                    (ignoreCase /* ignore case */) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal
                )
            )
            {
                Skip(match.Length);
                return true;
            }

            return false;
        }

        /*
         * Method:  SinkCharacter
         * 
         * Sink and return one character.
         */
        internal char SinkCharacter()
        {
            char c = CurrentCharacter;
            Skip();
            return c;
        }

        /*
         * Method:  SinkIgnoreCase
         * 
         * See if the next characters match the given string without case.
         */
        internal bool SinkIgnoreCase(string match)
        {
            return Sink(match, true);
        }

        /*
         * Method:  MatchNextIdentifierStart
         * 
         * Determine whether a given character is a C# or VB identifier start character.
         * Both languages agree on this format.
         */
        internal bool MatchNextIdentifierStart()
        {
            // From 2.4.2 of the C# Language Specification
            // identifier-start-letter-character:
            if (CurrentCharacter == '_' || TokenChar.IsLetter(CurrentCharacter))
            {
                return true;
            }
            return false;
        }

        /*
         * Method:  SinkIdentifierStart
         * 
         * Determine whether a given character is a C# or VB identifier start character.
         * Both languages agree on this format.
         */
        internal bool SinkIdentifierStart()
        {
            if (MatchNextIdentifierStart())
            {
                Skip();
                return true;
            }
            return false;
        }

        /*
         * Method:  SinkIdentifierPart
         * 
         * Determine whether a given character is a C# or VB identifier part character
         * Both languages agree on this format.
         */
        internal bool SinkIdentifierPart()
        {
            // From 2.4.2 of the C# Language Specification
            // identifier-part-letter-character:
            if (
                    TokenChar.IsLetter(CurrentCharacter)
                    || TokenChar.IsDecimalDigit(CurrentCharacter)
                    || TokenChar.IsConnecting(CurrentCharacter)
                    || TokenChar.IsCombining(CurrentCharacter)
                    || TokenChar.IsFormatting(CurrentCharacter)
                )
            {
                Skip();
                return true;
            }
            return false;
        }

        /*
         * Method:  SinkNewLine
         * 
         * Sink a newline.
         */
        internal bool SinkNewLine()
        {
            if (EndOfLines)
            {
                return false;
            }

            int originalPosition = _position;

            if (Sink("\xd\xa")) // This sequence is treated as a single new line.
            {
                ++_currentLine;
                ErrorUtilities.VerifyThrow(originalPosition != _position, "Expected position to be incremented.");
                return true;
            }

            if (TokenChar.IsNewLine(CurrentCharacter))
            {
                Skip();
                ErrorUtilities.VerifyThrow(originalPosition != _position, "Expected position to be incremented.");
                return true;
            }

            return false;
        }

        /*
         * Method:  SinkToEndOfLine
         * 
         * Sink from the current position to the first end-of-line.
         */
        internal bool SinkToEndOfLine()
        {
            while (!TokenChar.IsNewLine(CurrentCharacter))
            {
                Skip();
            }
            return true;    // Matching zero characters is ok.        
        }

        /*
         * Method:  SinkUntil
         * 
         * Sink until the given string is found. Match including the given string.
         */
        internal bool SinkUntil(string find)
        {
            bool found = false;
            while (!EndOfLines && !found)
            {
                if (Sink(find))
                {
                    found = true;
                }
                else
                {
                    Skip();
                }
            }

            return found;    // Return true if the matching string was found.
        }

        /*
         * Method:  SinkMultipleHexDigits
         * 
         * Sink multiple hex digits.
         */
        internal bool SinkMultipleHexDigits()
        {
            int count = 0;
            while (TokenChar.IsHexDigit(CurrentCharacter))
            {
                ++count;
                Skip();
            }
            return count > 0;     // Must match at least one  
        }

        /*
         * Method:  SinkMultipleDecimalDigits
         * 
         * Sink multiple decimal digits.
         */
        internal bool SinkMultipleDecimalDigits()
        {
            int count = 0;
            while (TokenChar.IsDecimalDigit(CurrentCharacter))
            {
                ++count;
                Skip();
            }
            return count > 0;     // Must match at least one 
        }
    }
}

