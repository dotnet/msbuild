// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    internal class EngineFileUtilities
    {
        /// <summary>
        /// Used for the purposes of evaluating an item specification. Given a filespec that may include wildcard characters * and
        /// ?, we translate it into an actual list of files. If the input filespec doesn't contain any wildcard characters, and it
        /// doesn't appear to point to an actual file on disk, then we just give back the input string as an array of length one,
        /// assuming that it wasn't really intended to be a filename (as items are not required to necessarily represent files).
        /// </summary>
        /// <owner>RGoel</owner>
        /// <param name="filespec">The filespec to evaluate.</param>
        /// <returns>Array of file paths.</returns>
        internal static string[] GetFileListEscaped
            (
            string directory,
            string filespec
            )
        {
            ErrorUtilities.VerifyThrow(filespec.Length > 0, "Need a valid file-spec.");

            string[] fileList;

            bool containsEscapedWildcards = EscapingUtilities.ContainsEscapedWildcards(filespec);
            bool containsRealWildcards = FileMatcher.HasWildcards(filespec);

            if (containsEscapedWildcards && containsRealWildcards)
            {
                // Umm, this makes no sense.  The item's Include has both escaped wildcards and 
                // real wildcards.  What does he want us to do?  Go to the file system and find
                // files that literally have '*' in their filename?  Well, that's not going to 
                // happen because '*' is an illegal character to have in a filename.

                // Just return the original string.
                fileList = new string[] { EscapingUtilities.Escape(filespec) };
            }
            else if (!containsEscapedWildcards && containsRealWildcards)
            {
                // Unescape before handing it to the filesystem.
                string filespecUnescaped = EscapingUtilities.UnescapeAll(filespec);

                // Get the list of actual files which match the filespec.  Put
                // the list into a string array.  If the filespec started out
                // as a relative path, we will get back a bunch of relative paths.
                // If the filespec started out as an absolute path, we will get
                // back a bunch of absolute paths.
                fileList = FileMatcher.GetFiles(directory, filespecUnescaped);

                ErrorUtilities.VerifyThrow(fileList != null, "We must have a list of files here, even if it's empty.");

                // Before actually returning the file list, we sort them alphabetically.  This
                // provides a certain amount of extra determinism and reproducability.  That is,
                // we're sure that the build will behave in exactly the same way every time,
                // and on every machine.
                Array.Sort(fileList);

                // We must now go back and make sure all special characters are escaped because we always 
                // store data in the engine in escaped form so it doesn't screw up our parsing.
                // Note that this means that characters that were not escaped in the original filespec
                // may now be escaped, but that's not easy to avoid.
                for (int i = 0; i < fileList.Length; i++)
                {
                    fileList[i] = EscapingUtilities.Escape(fileList[i]);
                }
            }
            else
            {
                // No real wildcards means we just return the original string.  Don't even bother 
                // escaping ... it should already be escaped appropriately since it came directly
                // from the project file or the OM host.
                fileList = new string[] { filespec };
            }

            return fileList;
        }
    }
}
