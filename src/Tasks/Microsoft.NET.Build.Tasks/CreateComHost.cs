// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.NET.HostModel;
using Microsoft.NET.HostModel.ComHost;

namespace Microsoft.NET.Build.Tasks
{
    public class CreateComHost : TaskWithAssemblyResolveHooks
    {
        [Required]
        public string ComHostSourcePath { get; set; }

        [Required]
        public string ComHostDestinationPath { get; set; }

        [Required]
        public string ClsidMapPath { get; set; }

        public ITaskItem[] TypeLibraries { get; set; }

        protected override void ExecuteCore()
        {
            try
            {
                if (!TypeLibraryDictionaryBuilder.TryCreateTypeLibraryIdDictionary(
                    TypeLibraries,
                    out Dictionary<int, string> typeLibIdMap,
                    out IEnumerable<string> errors))
                {
                    foreach (string error in errors)
                    {
                        Log.LogError(error);
                    }
                    return;
                }

                ComHost.Create(
                    ComHostSourcePath,
                    ComHostDestinationPath,
                    ClsidMapPath,
                    typeLibIdMap);
            }
            catch (ComHostCustomizationUnsupportedOSException)
            {
                Log.LogError(Strings.CannotEmbedClsidMapIntoComhost);
            }
            catch (TypeLibraryDoesNotExistException ex)
            {
                Log.LogError(Strings.TypeLibraryDoesNotExist, ex.Path);
            }
            catch (InvalidTypeLibraryIdException ex)
            {
                Log.LogError(Strings.InvalidTypeLibraryId, ex.Id.ToString(), ex.Path);
            }
            catch (InvalidTypeLibraryException ex)
            {
                Log.LogError(Strings.InvalidTypeLibrary, ex.Path);
            }
        }
    }
}
