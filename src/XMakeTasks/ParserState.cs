// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using System.Resources;
using System.Reflection;
using System.Collections;

namespace Microsoft.Build.Tasks
{
    /*
    * Class:   ParseState
    *
    * State used by the c# and vb parsers. Maintains information about
    * what's being parsed and what has been seen so far.
    *
    */
    sealed internal class ParseState
    {
        // Currently resolving a namespace name?
        private bool _resolvingNamespace;
        // Currently resolving a class name?
        private bool _resolvingClass;
        // Currently inside an open conditional preprocessor directive?
        private int _openConditionalDirectives = 0;
        // The current namespace name as its being resolved.
        private string _namespaceName;
        // A stack of namespaces so that nested namespaces can be supported.
        private Stack _namespaceStack = new Stack();

        /*
        * Method:  ParseState
        * 
        * Construct.
        */
        internal ParseState()
        {
            Reset();
        }

        /*
        * Method:  ResolvingNamespace
        * 
        * Get or set the ResolvingNamespace property.
        */
        internal bool ResolvingNamespace
        {
            get { return _resolvingNamespace; }
            set { _resolvingNamespace = value; }
        }

        /*
        * Method:  ResolvingClass
        * 
        * Get or set the ResolvingClass property.
        */
        internal bool ResolvingClass
        {
            get { return _resolvingClass; }
            set { _resolvingClass = value; }
        }

        /*
        * Method:  InsideConditionalDirective
        * 
        * Get the InsideConditionalDirective property.
        */
        internal bool InsideConditionalDirective
        {
            get { return _openConditionalDirectives > 0; }
        }

        /*
        * Method:  Namespace
        * 
        * Get or set the Namespace property.
        */
        internal string Namespace
        {
            get { return _namespaceName; }
            set { _namespaceName = value; }
        }

        /*
        * Method:  Reset
        * 
        * Reset the state, but don't throw away namespace stack information.
        */
        internal void Reset()
        {
            _resolvingNamespace = false;
            _resolvingClass = false;
            _namespaceName = String.Empty;
        }

        /*
         * Method:  OpenConditionalDirective
         * 
         * Note that we've entered a conditional directive
         */
        internal void OpenConditionalDirective()
        {
            _openConditionalDirectives++;
        }

        /*
         * Method:  CloseConditionalDirective
         * 
         * Note that we've exited a conditional directive
         */
        internal void CloseConditionalDirective()
        {
            _openConditionalDirectives--;
        }

        /*
        * Method:  PushNamespacePart
        * 
        * Push a namespace element onto the stack. May be null.
        */
        internal void PushNamespacePart(string namespacePart)
        {
            _namespaceStack.Push(namespacePart);
        }

        /*
        * Method:  PopNamespacePart
        * 
        * Pop a namespace element from the stack. May be null.
        */
        internal string PopNamespacePart()
        {
            if (_namespaceStack.Count == 0)
            {
                return null;
            }

            return (string)_namespaceStack.Pop();
        }

        /*
        * Method:  ComposeQualifiedClassName
        * 
        * Build a fully qualified (i.e. with the namespace) class name
        * base on the contents of the stack.
        */
        internal string ComposeQualifiedClassName(string className)
        {
            StringBuilder fullClass = new StringBuilder(1024);
            foreach (string namespacePiece in _namespaceStack)
            {
                if (null != namespacePiece && namespacePiece.Length > 0)
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
