// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Microsoft.DotNet.ApiCompat
{
    /// <summary>
    /// A regex string transformer that transforms an input string via a set of regex patterns and replacement strings.
    /// </summary>
    internal class RegexStringTransformer
    {
        private readonly (Regex Regex, string ReplacementString)[] _patterns;

        /// <summary>
        /// Initializes the regex string transformer with a given capture group and replacement patterns.
        /// </summary>
        /// <param name="captureGroupPattern">The capture group pattern to retrieve data that should be embedded into the replacement string.</param>
        /// <param name="replacementString">The replacement string that contains the capture group markers (i.e. $1).</param>

        public RegexStringTransformer(string captureGroupPattern, string replacementString)
            : this(new (string, string)[] { (captureGroupPattern, replacementString) })
        {
        }

        /// <summary>
        /// Initializes the regex string transformer with the given capture group and replacement patterns.
        /// </summary>
        public RegexStringTransformer((string CaptureGroupPattern, string ReplacementString)[] rawPatterns)
        {
            _patterns = new (Regex Regex, string ReplacementString)[rawPatterns.Length];
            for (int i = 0; i < rawPatterns.Length; i++)
            {
                _patterns[i] = (new Regex(rawPatterns[i].CaptureGroupPattern, RegexOptions.Compiled), rawPatterns[i].ReplacementString);
            }
        }

        /// <summary>
        /// Transforms the input string via a regex pattern to a replacement string.
        /// </summary>
        /// <param name="input">The input string to transform.</param>
        /// <returns>Returns the transformed input string. If the matches weren't successful, returns the untouched input.</returns>
        public string Transform(string input)
        {
            string current = input;
            foreach ((Regex regex, string replacementString) in _patterns)
            {
                current = regex.Replace(current, replacementString);
            }

            return current;
        }
    }
}
