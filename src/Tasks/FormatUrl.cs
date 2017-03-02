// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Formats a url by canonicalizing it (i.e. " " -> "%20") and transforming "localhost" to "machinename".
    /// </summary>
    public sealed class FormatUrl : TaskExtension
    {
        private string _inputUrl;
        private string _outputUrl;

        public string InputUrl
        {
            get { return _inputUrl; }
            set { _inputUrl = value; }
        }

        [Output]
        public string OutputUrl
        {
            get { return _outputUrl; }
            set { _outputUrl = value; }
        }

        public override bool Execute()
        {
            if (_inputUrl != null)
                _outputUrl = PathUtil.Format(_inputUrl);
            else
                _outputUrl = String.Empty;
            return true;
        }
    }
}

