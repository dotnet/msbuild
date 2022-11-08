using System;
using System.IO;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the response file used event
    /// </summary>
    [Serializable]
    public class ResponseFileUsedEventArgs : BuildMessageEventArgs
    {
        /// <summary>
        /// Initialize a new instance of the ResponseFileUsedEventArgs class.
        /// </summary>
        public ResponseFileUsedEventArgs()
        {
        }

        public ResponseFileUsedEventArgs(string message)
            : base(message: message)
        {
        }
    }
}
