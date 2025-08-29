﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;

#nullable disable

namespace Microsoft.Build.Shared.LanguageParser
{
    /*
     * Class:   TokenEnumerator
     *
     * Abstract base class for implementing IEnumerator over a TokenCharReader.
     * Derived class is responsible for actual tokenization.
     *
     */
    internal abstract class TokenEnumerator : IEnumerator
    {
        // The current token that was found.
        protected Token current = null;

        // Return the token char reader.
        internal abstract TokenCharReader Reader { get; }

        // Implemented by derived class to find the next token.
        internal abstract bool FindNextToken();

        /*
        * Method:  MoveNext
        * 
        * Declare the MoveNext method required by IEnumerator
        */
        public bool MoveNext()
        {
            if (Reader.EndOfLines)
            {
                return false;
            }

            int startLine = Reader.CurrentLine;
            int startPosition = Reader.Position;

            bool found = FindNextToken();

            // If a token was found, record the line number and text into
            if (found && this.current != null)
            {
                this.current.Line = startLine;

                // Don't record if there is already something there.
                // This is so that FindNextToken can set the value if it wants to.
                if (this.current.InnerText == null)
                {
                    this.current.InnerText = Reader.GetCurrentMatchedString(startPosition);
                }
            }
            return found;
        }

        /*
        * Method:  Reset
        * 
        * Declare the Reset method required by IEnumerator
        */
        public void Reset()
        {
            Reader.Reset();
            this.current = null;
        }

        /*
        * Method:  Current
        * 
        * Declare the Current property required by IEnumerator
        */
        public object Current
        {
            get { return current; }
        }
    }
}
