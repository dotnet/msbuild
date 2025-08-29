// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

#nullable disable

namespace Microsoft.Build.Framework
{
    internal static class ITaskItemExtensions
    {
        /// <summary>
        /// Provides a way to efficiently enumerate custom metadata of an item, without built-in metadata.
        /// </summary>
        /// <param name="taskItem">TaskItem implementation to return metadata from</param>
        /// <remarks>WARNING: do NOT use List`1.AddRange to iterate over this collection.
        /// CopyOnWriteDictionary from Microsoft.Build.Utilities.v4.0.dll is broken.</remarks>
        /// <returns>A non-null (but possibly empty) enumerable of item metadata.</returns>
        public static IEnumerable<KeyValuePair<string, string>> EnumerateMetadata(this ITaskItem taskItem)
        {
            if (taskItem is IMetadataContainer container)
            {
                // This is the common case: most implementations should implement this for quick access
                return container.EnumerateMetadata();
            }

            // This runs if ITaskItem is Microsoft.Build.Utilities.TaskItem from Microsoft.Build.Utilities.v4.0.dll
            // that is loaded from the GAC.
            IDictionary customMetadata = taskItem.CloneCustomMetadata();
            if (customMetadata is IEnumerable<KeyValuePair<string, string>> enumerableMetadata)
            {
                return enumerableMetadata;
            }

            // Fallback for
            //  * ITaskItem implementations from MSBuild 3.5 from the GAC
            //  * Custom ITaskItems that don't use Dictionary<string,string>
            var list = new KeyValuePair<string, string>[customMetadata.Count];
            int i = 0;

            foreach (string metadataName in customMetadata.Keys)
            {
                string valueOrError;

                try
                {
                    valueOrError = taskItem.GetMetadata(metadataName);
                }
                // Temporarily try catch all to mitigate frequent NullReferenceExceptions in
                // the logging code until CopyOnWritePropertyDictionary is replaced with
                // ImmutableDictionary. Calling into Debug.Fail to crash the process in case
                // the exception occurres in Debug builds.
                catch (Exception e)
                {
                    valueOrError = e.Message;
                    Debug.Fail(e.ToString());
                }

                list[i++] = new KeyValuePair<string, string>(metadataName, valueOrError);
            }

            return list;
        }
    }
}
