// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Cli.Build
{
    /// <summary>
    /// Reads contents of an input file, and searches for each ReplacementPattern passed in. 
    /// When a ReplacementPattern is matched it will replace it with the string of the corresponding (by index) 
    /// item in ReplacementStrings.
    /// 
    /// For example, if 2 ReplacementPatterns are passed in, 2 ReplacementStrings must also passed in and the first 
    /// pattern will be replaced with the first string, and the second pattern replaced with the second string.
    /// 
    /// ReplacementPattern could easily be a regex, but it isn't needed for current use cases, so leaving this 
    /// as just a string that will be replaced.
    /// </summary>
    public class ReplaceFileContents : Task
    {
        [Required]
        public string InputFile { get; set; }

        [Required]
        public string DestinationFile { get; set; }

        [Required]
        public ITaskItem[] ReplacementPatterns { get; set; }

        [Required]
        public ITaskItem[] ReplacementStrings { get; set; }

        public override bool Execute()
        {
            if (ReplacementPatterns.Length != ReplacementStrings.Length)
            {
                throw new Exception($"Expected {nameof(ReplacementPatterns)}  (length {ReplacementPatterns.Length}) and {nameof(ReplacementStrings)} (length {ReplacementStrings.Length}) to have the same length.");
            }

            if (!File.Exists(InputFile))
            {
                throw new FileNotFoundException($"Expected file {InputFile} was not found.");
            }

            string inputFileText = File.ReadAllText(InputFile);
            string outputFileText = ReplacePatterns(inputFileText);

            WriteOutputFile(outputFileText);

            return true;
        }

        public string ReplacePatterns(string inputFileText)
        {
            var outText = inputFileText;

            for (int i=0; i<ReplacementPatterns.Length; ++i)
            {
                var replacementPattern = ReplacementPatterns[i].ItemSpec;
                var replacementString = ReplacementStrings[i].ItemSpec;

                outText = outText.Replace(replacementPattern, replacementString);
            }

            return outText;
        }

        public void WriteOutputFile(string outputFileText)
        {
            var destinationDirectory = Path.GetDirectoryName(DestinationFile);
            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.WriteAllText(DestinationFile, outputFileText);
        }
    }
}
