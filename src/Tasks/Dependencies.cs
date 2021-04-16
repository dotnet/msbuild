// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.BackEnd;
using System;
using System.Collections.Generic;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Represents a cache of inputs to a compilation-style task.
    /// </summary>
    internal class Dependencies
    {
        /// <summary>
        /// Dictionary of other dependency files.
        /// Key is filename and value is DependencyFile.
        /// </summary>
        internal Dictionary<string, DependencyFile> dependencies = new();

        internal Dependencies() { }

        internal Dependencies(ITranslator translator, Type t)
        {
            Translate(translator, t);
        }

        public void Translate(ITranslator translator, Type t)
        {
            translator.TranslateDictionary(ref dependencies, (ITranslator translator, ref DependencyFile dependency) =>
            {
                if (t == typeof(ResGenDependencies.ResXFile))
                {
                    ResGenDependencies.ResXFile resx = dependency as ResGenDependencies.ResXFile;
                    resx ??= new();
                    resx.Translate(translator);
                    dependency = resx;
                }
                else if (t == typeof(ResGenDependencies.PortableLibraryFile))
                {
                    ResGenDependencies.PortableLibraryFile lib = dependency as ResGenDependencies.PortableLibraryFile;
                    lib ??= new();
                    lib.Translate(translator);
                    dependency = lib;
                }

                dependency ??= new();
                translator.Translate(ref dependency.filename);
                translator.Translate(ref dependency.lastModified);
                translator.Translate(ref dependency.exists);
            });
        }

        /// <summary>
        /// Look up a dependency file. Return null if it isn't there.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
            internal DependencyFile GetDependencyFile(string filename)
        {
            dependencies.TryGetValue(filename, out DependencyFile file);
            return file;
        }

        /// <summary>
        /// Add a new dependency file.
        /// </summary>
        internal void AddDependencyFile(string filename, DependencyFile file)
        {
            dependencies[filename] = file;
        }

        /// <summary>
        /// Remove new dependency file.
        /// </summary>
        internal void RemoveDependencyFile(string filename)
        {
            dependencies.Remove(filename);
        }

        /// <summary>
        /// Remove all entries from the dependency table.
        /// </summary>
        internal void Clear()
        {
            dependencies.Clear();
        }
    }
}
