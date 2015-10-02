// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// An exception thrown while parsing through an app.config.
    /// </summary>
    [Serializable]
    internal class AppConfigException :
#if FEATURE_VARIOUS_EXCEPTIONS
        System.ApplicationException
#else
        Exception
#endif
    {
        /// <summary>
        /// The name of the app.config file.
        /// </summary>
        private string _fileName = String.Empty;
        internal string FileName
        {
            get
            {
                return _fileName;
            }
        }


        /// <summary>
        /// The line number with the error. Is initialized to zero
        /// </summary>
        private int _line;
        internal int Line
        {
            get
            {
                return _line;
            }
        }

        /// <summary>
        /// The column with the error. Is initialized to zero
        /// </summary>
        private int _column;
        internal int Column
        {
            get
            {
                return _column;
            }
        }


        /// <summary>
        /// Construct the exception.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="fileName"></param>
        /// <param name="line"></param>
        /// <param name="column"></param>
        /// <param name="inner"></param>
        public AppConfigException(string message, string fileName, int line, int column, System.Exception inner) : base(message, inner)
        {
            _fileName = fileName;
            _line = line;
            _column = column;
        }

#if FEATURE_BINARY_SERIALIZATION
        /// <summary>
        /// Construct the exception.
        /// </summary>
        protected AppConfigException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
#endif
    }
}
