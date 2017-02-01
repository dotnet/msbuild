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
    /// Task that simply emits a warning. Engine will add the project path because
    /// we do not specify a filename.
    /// </summary>
    public sealed class Warning : TaskExtension
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
        /// Warning code
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

        /// <summary>
        /// Relevant file if any.
        /// If none is provided, the file containing the Warning 
        /// task will be used.
        /// </summary>
        public string File
        {
            get;
            set;
        }

        private string _helpKeyword;

        /// <summary>
        /// Warning help keyword
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
            Log.LogWarning(null, Code, HelpKeyword, File, 0, 0, 0, 0, Text ?? TaskResources.GetString("ErrorAndWarning.EmptyMessage"));

            return true;
        }
    }
}
