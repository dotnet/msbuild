// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Tasks.Deployment.Bootstrapper
{
    /// <summary>
    /// Represents the results of the Build operation of the BootstrapperBuilder.
    /// </summary>
    [ComVisible(true), Guid("FAD7BA7C-CA00-41e0-A5EF-2DA9A74E58E6"), ClassInterface(ClassInterfaceType.None)]
    public class BuildResults : IBuildResults
    {
        private readonly List<string> _componentFiles = new List<string>();
        private readonly List<BuildMessage> _messages = new List<BuildMessage>();

        internal BuildResults()
        {
        }

        /// <summary>
        /// Returns true if the bootstrapper build was successful, false otherwise
        /// </summary>
        public bool Succeeded { get; private set; }

        /// <summary>
        /// The file path to the generated primary bootstrapper file
        /// </summary>
        /// <value>Path to setup.exe</value>
        public string KeyFile { get; private set; } = string.Empty;

        /// <summary>
        /// File paths to copied component installer files
        /// </summary>
        /// <value>Path to component files</value>
        public string[] ComponentFiles
        {
            get
            {
                if (_componentFiles.Count == 0)
                {
                    return null;
                }

                return _componentFiles.ToArray();
            }
        }

        /// <summary>
        /// The build messages generated from a bootstrapper build
        /// </summary>
        public BuildMessage[] Messages
        {
            get
            {
                if (_messages.Count == 0)
                {
                    return null;
                }

                return _messages.ToArray();
            }
        }

        internal void AddMessage(BuildMessage message)
        {
            _messages.Add(message);
        }

        internal void AddComponentFiles(string[] filePaths)
        {
            _componentFiles.AddRange(filePaths);
        }

        internal void BuildSucceeded()
        {
            Succeeded = true;
        }

        internal void SetKeyFile(string filePath)
        {
            KeyFile = filePath;
        }
    }
}
