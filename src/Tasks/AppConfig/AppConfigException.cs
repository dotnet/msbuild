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
        private string fileName = String.Empty;
        internal string FileName
        {
            get
            {
                return fileName;
            }
        }


        /// <summary>
        /// The line number with the error. Is initialized to zero
        /// </summary>
        private int line;
        internal int Line
        {
            get
            {
                return line;
            }
        }

        /// <summary>
        /// The column with the error. Is initialized to zero
        /// </summary>
        private int column;
        internal int Column
        {
            get
            {
                return column;
            }
        }


        /// <summary>
        /// Construct the exception.
        /// </summary>
        public AppConfigException(string message, string fileName, int line, int column, System.Exception inner) : base(message, inner)
        {
            this.fileName = fileName;
            this.line = line;
            this.column = column;
        }

        /// <summary>
        /// Construct the exception.
        /// </summary>
        protected AppConfigException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
