// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//Microsoft.NET.Build.Extensions.Tasks (net7.0) has nullables disabled
#pragma warning disable IDE0240 // Remove redundant nullable directive
#nullable disable
#pragma warning restore IDE0240 // Remove redundant nullable directive

using System.Globalization;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Provides a base localizable mechanism for logging messages of different kids from the SDK targets.
    /// </summary>
    public abstract class MessageBase : TaskBase
    {
        /// <summary>
        /// Formatted text for the message
        /// </summary>
        public string FormattedText { get; set; }

        /// <summary>
        /// The name of the resource in Strings.resx that contains the desired error message.
        /// </summary>
        public string ResourceName { get; set; }

        /// <summary>
        /// The arguments provided to <see cref="string.Format"/> along with the retrieved resource as the format.
        /// </summary>
        public string[] FormatArguments { get; set; }

        private static readonly string[] EmptyArguments = new[] { "" };

        internal MessageBase()
        {
        }

        protected override void ExecuteCore()
        {
            string message = null;
            if (string.IsNullOrEmpty(FormattedText) && string.IsNullOrEmpty(ResourceName))
            {
                throw new ArgumentException($"Either {nameof(FormattedText)} or {nameof(ResourceName)} must be specified.");
            }
            else if (!string.IsNullOrEmpty(FormattedText) && !string.IsNullOrEmpty(ResourceName))
            {
                throw new ArgumentException($"Only one of {nameof(FormattedText)} and {nameof(ResourceName)} can be specified.");
            }
            else if (!string.IsNullOrEmpty(ResourceName))
            {
                if (FormatArguments == null || FormatArguments.Length == 0)
                {
                    // We use a single-item array with one empty string in this case so that
                    // it is possible to interpret FormatArguments="$(EmptyVariable)" as a request
                    // to pass an empty string on to string.Format. Note if there are not placeholders
                    // in the string, then the empty string arg will be ignored.
                    FormatArguments = EmptyArguments;
                }

                string format = Strings.ResourceManager.GetString(ResourceName, Strings.Culture);
                message = string.Format(CultureInfo.CurrentCulture, format, FormatArguments);
            }
            else
            {
                message = FormattedText;
            }
            

            LogMessage(message);
        }

        protected abstract void LogMessage(string message);
    }
}
