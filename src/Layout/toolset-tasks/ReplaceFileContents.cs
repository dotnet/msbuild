// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Cli.Build
{
    /// <summary>
    /// Reads contents of an input file, and searches for each replacement passed in.
    ///
    /// When ReplacementItems is matched, it will replace the Include/ItemSpec with the corresponding
    /// ReplacementString metadata value. This can be useful if the ReplacementString is a value that
    /// cannot be represented by ITaskItem.ItemSpec (like string.Empty).
    ///
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
        public ITaskItem[] InputFiles { get; set; }

        [Required]
        public ITaskItem[] DestinationFiles { get; set; }

        public ITaskItem[] ReplacementItems { get; set; }

        public ITaskItem[] ReplacementPatterns { get; set; }

        public ITaskItem[] ReplacementStrings { get; set; }

        public override bool Execute()
        {
            if (ReplacementItems == null && ReplacementPatterns == null && ReplacementStrings == null)
            {
                throw new Exception($"ReplaceFileContents was called with no replacement values. Either pass ReplacementItems or ReplacementPatterns/ReplacementStrings properties.");
            }

            ReplacementItems = ReplacementItems ?? Array.Empty<ITaskItem>();
            ReplacementPatterns = ReplacementPatterns ?? Array.Empty<ITaskItem>();
            ReplacementStrings = ReplacementStrings ?? Array.Empty<ITaskItem>();

            if (ReplacementPatterns.Length != ReplacementStrings.Length)
            {
                throw new Exception($"Expected {nameof(ReplacementPatterns)}  (length {ReplacementPatterns.Length}) and {nameof(ReplacementStrings)} (length {ReplacementStrings.Length}) to have the same length.");
            }

            if (InputFiles.Length != DestinationFiles.Length)
            {
                throw new Exception($"Expected {nameof(InputFiles)}  (length {InputFiles.Length}) and {nameof(DestinationFiles)} (length {DestinationFiles.Length}) to have the same length.");
            }

            var filesNotFound = InputFiles.Where(i => !File.Exists(i.ItemSpec)).Select(i => i.ItemSpec);
            if (filesNotFound.Any())
            {
                var filesNotFoundString = string.Join(",", filesNotFound);
                throw new FileNotFoundException($"Expected files where not found: {filesNotFoundString}");
            }

            Log.LogMessage(MessageImportance.High, $"ReplacingContents for `{InputFiles.Length}` files.");

            for (var i = 0; i < InputFiles.Length; i++)
            {
                ReplaceContents(InputFiles[i].ItemSpec, DestinationFiles[i].ItemSpec);
            }

            return true;
        }

        public void ReplaceContents(string inputFile, string destinationFile)
        {
            string inputFileText = File.ReadAllText(inputFile);
            string outputFileText = ReplacePatterns(inputFileText);

            WriteOutputFile(destinationFile, outputFileText);
        }

        public string ReplacePatterns(string inputFileText)
        {
            var outText = inputFileText;

            foreach (var replacementItem in ReplacementItems)
            {
                var replacementPattern = replacementItem.ItemSpec;
                var replacementString = replacementItem.GetMetadata("ReplacementString");

                outText = outText.Replace(replacementPattern, replacementString);
            }

            for (int i = 0; i < ReplacementPatterns.Length; ++i)
            {
                var replacementPattern = ReplacementPatterns[i].ItemSpec;
                var replacementString = ReplacementStrings[i].ItemSpec;

                var regex = new Regex(replacementPattern);
                outText = regex.Replace(outText, replacementString);
            }

            return outText;
        }

        public void WriteOutputFile(string destinationFile, string outputFileText)
        {
            var destinationDirectory = Path.GetDirectoryName(destinationFile);
            Log.LogMessage(MessageImportance.High, $"Destination Directory: {destinationDirectory}");
            if (!Directory.Exists(destinationDirectory))
            {
                Log.LogMessage(MessageImportance.High, $"Destination Directory `{destinationDirectory}` does not exist. Creating...");
                Directory.CreateDirectory(destinationDirectory);
            }

            Log.LogMessage(MessageImportance.High, $"Writing file: {destinationFile}");
            File.WriteAllText(destinationFile, outputFileText);
        }
    }
}
