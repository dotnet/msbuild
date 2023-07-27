// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    static class TypeLibraryDictionaryBuilder
    {
        public static bool TryCreateTypeLibraryIdDictionary(ITaskItem[] typeLibraries, out Dictionary<int, string> typeLibraryIdMap, out IEnumerable<string> errors)
        {
            typeLibraryIdMap = null;
            errors = Enumerable.Empty<string>();
            List<string> errorsLocal = new List<string>();
            if (typeLibraries is null || typeLibraries.Length == 0)
            {
                return true;
            }
            else if (typeLibraries.Length == 1)
            {
                int id = 1;
                string idMetadata = typeLibraries[0].GetMetadata("Id");
                if (!string.IsNullOrEmpty(idMetadata))
                {
                    if (!int.TryParse(idMetadata, out id) || id == 0)
                    {
                        errorsLocal.Add(string.Format(Strings.InvalidTypeLibraryId, idMetadata, typeLibraries[0].ItemSpec));
                        errors = errorsLocal;
                        return false;
                    }
                }
                typeLibraryIdMap = new Dictionary<int, string> { { id, typeLibraries[0].ItemSpec } };
                return true;
            }
            typeLibraryIdMap = new Dictionary<int, string>();
            foreach (ITaskItem typeLibrary in typeLibraries)
            {
                string idMetadata = typeLibrary.GetMetadata("Id");
                if (string.IsNullOrEmpty(idMetadata))
                {
                    errorsLocal.Add(string.Format(Strings.MissingTypeLibraryId, typeLibrary.ItemSpec));
                    continue;
                }

                if (!int.TryParse(idMetadata, out int id) || id == 0)
                {
                    errorsLocal.Add(string.Format(Strings.InvalidTypeLibraryId, idMetadata, typeLibrary.ItemSpec));
                    continue;
                }

                if (typeLibraryIdMap.ContainsKey(id))
                {
                    errorsLocal.Add(string.Format(Strings.DuplicateTypeLibraryIds, idMetadata, typeLibraryIdMap[id], typeLibrary.ItemSpec));
                }
                else
                {
                    typeLibraryIdMap[id] = typeLibrary.ItemSpec;
                }
            }
            errors = errorsLocal;
            return errorsLocal.Count == 0;
        }
    }
}
