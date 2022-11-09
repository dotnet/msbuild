using System;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the response file used event
    /// </summary>
    [Serializable]
    public class ResponseFileUsedEventArgs : CustomBuildEventArgs
    {
        /// <summary>
        /// Initialize a new instance of the ResponseFileUsedEventArgs class.
        /// </summary>
        public ResponseFileUsedEventArgs() : base() { }

        public ResponseFileUsedEventArgs(string responseFilePath) : base()
        {
            ResponseFilePath = responseFilePath;
        }

        public string ResponseFilePath { get; set; }
    }
}
