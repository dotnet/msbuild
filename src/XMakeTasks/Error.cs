// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Globalization;
using System.Resources;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Task that simply emits an error. Engine will add project file path and line/column
    /// information.
    /// </summary>
    public sealed class Error : TaskExtension
    {
        private string _text;

        /// <summary>
        /// Error message
        /// </summary>
        public string Text
        {
            get
            {
                return _text;
            }

            set
            {
                _text = value;
            }
        }

        private string _code;

        /// <summary>
        /// Error code
        /// </summary>
        public string Code
        {
            get
            {
                return _code;
            }
            set
            {
                _code = value;
            }
        }

        private string _file;

        /// <summary>
        /// Relevant file if any.
        /// If none is provided, the file containing the Error 
        /// task will be used.
        /// </summary>
        public string File
        {
            get
            {
                return _file;
            }
            set
            {
                _file = value;
            }
        }

        private string _helpKeyword;

        /// <summary>
        /// Error help keyword
        /// </summary>
        public string HelpKeyword
        {
            get
            {
                return _helpKeyword;
            }
            set
            {
                _helpKeyword = value;
            }
        }

        /// <summary>
        /// Main task method
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            Log.LogError(null, Code, HelpKeyword, File, 0, 0, 0, 0, Text ?? TaskResources.GetString("ErrorAndWarning.EmptyMessage"));

            // careful to return false. Otherwise the build would continue.
            return false;
        }
    }
}
