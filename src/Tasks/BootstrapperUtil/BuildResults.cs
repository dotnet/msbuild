// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Tasks.Deployment.Bootstrapper
{
    /// <summary>
    /// Represents the results of the Build operation of the BootstrapperBuilder.
    /// </summary>
    [ComVisible(true), GuidAttribute("FAD7BA7C-CA00-41e0-A5EF-2DA9A74E58E6"), ClassInterface(ClassInterfaceType.None)]
    public class BuildResults : IBuildResults
    {
        private bool _succeeded;
        private string _keyFile;
        private ArrayList _componentFiles;
        private ArrayList _messages;

        internal BuildResults()
        {
            _succeeded = false;
            _keyFile = string.Empty;
            _componentFiles = new ArrayList();
            _messages = new ArrayList();
        }

        /// <summary>
        /// Returns true if the bootstrapper build was successful, false otherwise
        /// </summary>
        public bool Succeeded
        {
            get { return _succeeded; }
        }

        /// <summary>
        /// The file path to the generated primary bootstrapper file
        /// </summary>
        /// <value>Path to setup.exe</value>
        public string KeyFile
        {
            get { return _keyFile; }
        }

        /// <summary>
        /// File paths to copied component installer files
        /// </summary>
        /// <value>Path to component files</value>
        public string[] ComponentFiles
        {
            get
            {
                if (_componentFiles.Count == 0)
                    return null;

                string[] files = new string[_componentFiles.Count];
                _componentFiles.CopyTo(files);
                return files;
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
                    return null;

                BuildMessage[] msgs = new BuildMessage[_messages.Count];
                _messages.CopyTo(msgs);
                return msgs;
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
            _succeeded = true;
        }

        internal void SetKeyFile(string filePath)
        {
            _keyFile = filePath;
        }
    }
}
