// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;
using System.Collections;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// State used by the c# and vb parsers. Maintains information about
    /// what's being parsed and what has been seen so far.
    /// </summary>
    internal sealed class ParseState
    {
        // Currently inside an open conditional preprocessor directive?
        private int _openConditionalDirectives;

        // A stack of namespaces so that nested namespaces can be supported.
        private readonly Stack _namespaceStack = new Stack();

        internal ParseState()
        {
            Reset();
        }

        /// <summary>
        /// Are we resolving a namespace?
        /// </summary>
        internal bool ResolvingNamespace { get; set; }

        /// <summary>
        /// Are we resolving a class?
        /// </summary>
        internal bool ResolvingClass { get; set; }

        /// <summary>
        /// Are we inside a conditional directive?
        /// </summary>
        internal bool InsideConditionalDirective => _openConditionalDirectives > 0;

        /// <summary>
        /// The current namespace name as its being resolved.
        /// </summary>
        internal string Namespace { get; set; }

        /// <summary>
        /// Reset the state, but don't throw away namespace stack information.
        /// </summary>
        internal void Reset()
        {
            ResolvingNamespace = false;
            ResolvingClass = false;
            Namespace = String.Empty;
        }

        /// <summary>
        /// Note that we've entered a conditional directive
        /// </summary>
        internal void OpenConditionalDirective()
        {
            _openConditionalDirectives++;
        }

        /// <summary>
        /// Note that we've exited a conditional directive
        /// </summary>
        internal void CloseConditionalDirective()
        {
            _openConditionalDirectives--;
        }

        /// <summary>
        /// Push a namespace element onto the stack. May be null.
        /// </summary>
        internal void PushNamespacePart(string namespacePart)
        {
            _namespaceStack.Push(namespacePart);
        }

        /// <summary>
        /// Pop a namespace element from the stack. May be null.
        /// </summary>
        internal string PopNamespacePart()
        {
            if (_namespaceStack.Count == 0)
            {
                return null;
            }

            return (string)_namespaceStack.Pop();
        }

        /// <summary>
        /// Build a fully qualified (i.e. with the namespace) class name based on the contents of the stack.
        /// </summary>
        internal string ComposeQualifiedClassName(string className)
        {
            var fullClass = new StringBuilder(1024);
            foreach (string namespacePiece in _namespaceStack)
            {
                if (!string.IsNullOrEmpty(namespacePiece))
                {
                    fullClass.Insert(0, '.');
                    fullClass.Insert(0, namespacePiece);
                }
            }

            // Append the class.
            fullClass.Append(className);
            return fullClass.ToString();
        }
    }
}
