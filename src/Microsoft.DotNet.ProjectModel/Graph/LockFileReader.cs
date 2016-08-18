// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Microsoft.DotNet.ProjectModel.Graph
{
    public class LockFileReader
    {
        private readonly LockFileSymbolTable _symbols;

        public LockFileReader() : this(new LockFileSymbolTable()) { }

        public LockFileReader(LockFileSymbolTable symbols)
        {
            _symbols = symbols;
        }

        public static LockFile Read(string lockFilePath, bool designTime)
        {
            using (var stream = ResilientFileStreamOpener.OpenFile(lockFilePath))
            {
                try
                {
                    return new LockFileReader().ReadLockFile(lockFilePath, stream, designTime);
                }
                catch (FileFormatException ex)
                {
                    throw ex.WithFilePath(lockFilePath);
                }
                catch (Exception ex)
                {
                    throw FileFormatException.Create(ex, lockFilePath);
                }
            }
        }

        public LockFile ReadLockFile(string lockFilePath, Stream stream, bool designTime)
        {
            try
            {
                var reader = new StreamReader(stream);
                var jobject = JObject.Load(new JsonTextReader(reader));

                if (jobject == null)
                {
                    throw new InvalidDataException();
                }

                var lockFile = ReadLockFile(lockFilePath, jobject);

                if (!designTime)
                {
                    var patcher = new LockFilePatcher(lockFile, this);
                    patcher.Patch();
                }

                return lockFile;
            }
            catch (LockFilePatchingException)
            {
                throw;
            }
            catch
            {
                // Ran into parsing errors, mark it as unlocked and out-of-date
                return new LockFile(lockFilePath)
                {
                    Version = int.MinValue
                };
            }
        }

        public ExportFile ReadExportFile(string fragmentLockFilePath)
        {
            using (var stream = ResilientFileStreamOpener.OpenFile(fragmentLockFilePath))
            {
                try
                {
                    var rootJObject = JObject.ReadFrom(new JsonTextReader(new StreamReader(stream))) as JObject;

                    if (rootJObject == null)
                    {
                        throw new InvalidDataException();
                    }

                    var version = ReadInt(rootJObject, "version", defaultValue: int.MinValue);
                    var exports = ReadObject(rootJObject.Value<JObject>("exports"), ReadTargetLibrary);

                    return new ExportFile(fragmentLockFilePath, version, exports);

                }
                catch (FileFormatException ex)
                {
                    throw ex.WithFilePath(fragmentLockFilePath);
                }
                catch (Exception ex)
                {
                    throw FileFormatException.Create(ex, fragmentLockFilePath);
                }
            }
        }

        private LockFile ReadLockFile(string lockFilePath, JObject cursor)
        {
            var lockFile = new LockFile(lockFilePath);
            lockFile.Version = ReadInt(cursor, "version", defaultValue: int.MinValue);
            lockFile.Targets = ReadObject(cursor.Value<JObject>("targets"), ReadTarget);
            lockFile.ProjectFileDependencyGroups = ReadObject(cursor.Value<JObject>("projectFileDependencyGroups"), ReadProjectFileDependencyGroup);
            ReadLibrary(cursor.Value<JObject>("libraries"), lockFile);
            lockFile.PackageFolders = ReadObject(cursor.Value<JObject>("packageFolders"), ReadPackageFolder);

            return lockFile;
        }

        private void ReadLibrary(JObject json, LockFile lockFile)
        {
            if (json == null)
            {
                return;
            }

            foreach (var child in json)
            {
                var key = child.Key;
                var value = json.Value<JObject>(key);
                if (value == null)
                {
                    throw FileFormatException.Create("The value type is not object.", json[key]);
                }

                var parts = key.Split(new[] { '/' }, 2);
                var name = parts[0];
                var version = parts.Length == 2 ? _symbols.GetVersion(parts[1]) : null;

                var type = _symbols.GetString(value.Value<string>("type"));

                var pathValue = value["path"];
                var path = pathValue == null ? null : ReadString(pathValue);

                if (type == null || string.Equals(type, "package", StringComparison.OrdinalIgnoreCase))
                {
                    lockFile.PackageLibraries.Add(new LockFilePackageLibrary
                    {
                        Name = name,
                        Version = version,
                        IsServiceable = ReadBool(value, "serviceable", defaultValue: false),
                        Sha512 = ReadString(value["sha512"]),
                        Files = ReadPathArray(value["files"], ReadString),
                        Path = path
                    });
                }
                else if (type == "project")
                {
                    var projectLibrary = new LockFileProjectLibrary
                    {
                        Name = name,
                        Version = version
                    };
                    
                    projectLibrary.Path = path;

                    var buildTimeDependencyValue = value["msbuildProject"];
                    projectLibrary.MSBuildProject = buildTimeDependencyValue == null ? null : ReadString(buildTimeDependencyValue);

                    lockFile.ProjectLibraries.Add(projectLibrary);
                }
            }
        }

        private LockFileTarget ReadTarget(string property, JToken json)
        {
            var jobject = json as JObject;
            if (jobject == null)
            {
                throw FileFormatException.Create("The value type is not an object.", json);
            }

            var target = new LockFileTarget();
            var parts = property.Split(new[] { '/' }, 2);
            target.TargetFramework = _symbols.GetFramework(parts[0]);
            if (parts.Length == 2)
            {
                target.RuntimeIdentifier = _symbols.GetString(parts[1]);
            }

            target.Libraries = ReadObject(jobject, ReadTargetLibrary);

            return target;
        }

        private LockFilePackageFolder ReadPackageFolder(string property, JToken json)
        {
            var jobject = json as JObject;
            if (jobject == null)
            {
                throw FileFormatException.Create("The value type is not an object.", json);
            }

            var packageFolder = new LockFilePackageFolder();
            packageFolder.Path = property;

            return packageFolder;
        }

        private LockFileTargetLibrary ReadTargetLibrary(string property, JToken json)
        {
            var jobject = json as JObject;
            if (jobject == null)
            {
                throw FileFormatException.Create("The value type is not an object.", json);
            }

            var library = new LockFileTargetLibrary();

            var parts = property.Split(new[] { '/' }, 2);
            library.Name = _symbols.GetString(parts[0]);
            if (parts.Length == 2)
            {
                library.Version = _symbols.GetVersion(parts[1]);
            }

            library.Type = _symbols.GetString(jobject.Value<string>("type"));
            var framework = jobject.Value<string>("framework");
            if (framework != null)
            {
                library.TargetFramework = _symbols.GetFramework(framework);
            }

            library.Dependencies = ReadObject(jobject.Value<JObject>("dependencies"), ReadPackageDependency);
            library.FrameworkAssemblies = new HashSet<string>(ReadArray(jobject["frameworkAssemblies"], ReadFrameworkAssemblyReference), StringComparer.OrdinalIgnoreCase);
            library.RuntimeAssemblies = ReadObject(jobject.Value<JObject>("runtime"), ReadFileItem);
            library.CompileTimeAssemblies = ReadObject(jobject.Value<JObject>("compile"), ReadFileItem);
            library.ResourceAssemblies = ReadObject(jobject.Value<JObject>("resource"), ReadFileItem);
            library.NativeLibraries = ReadObject(jobject.Value<JObject>("native"), ReadFileItem);
            library.ContentFiles = ReadObject(jobject.Value<JObject>("contentFiles"), ReadContentFile);
            library.RuntimeTargets = ReadObject(jobject.Value<JObject>("runtimeTargets"), ReadRuntimeTarget);

            return library;
        }

        private LockFileRuntimeTarget ReadRuntimeTarget(string property, JToken json)
        {
            var jsonObject = json as JObject;
            if (jsonObject == null)
            {
                throw FileFormatException.Create("The value type is not an object.", json);
            }

            return new LockFileRuntimeTarget(
                path: _symbols.GetString(property),
                runtime: _symbols.GetString(jsonObject.Value<string>("rid")),
                assetType: _symbols.GetString(jsonObject.Value<string>("assetType"))
                );
        }

        private LockFileContentFile ReadContentFile(string property, JToken json)
        {
            var contentFile = new LockFileContentFile()
            {
                Path = property
            };

            var jsonObject = json as JObject;
            if (jsonObject != null)
            {

                BuildAction action;
                BuildAction.TryParse(jsonObject.Value<string>("buildAction"), out action);

                contentFile.BuildAction = action;
                var codeLanguage = _symbols.GetString(jsonObject.Value<string>("codeLanguage"));
                if (codeLanguage == "any")
                {
                    codeLanguage = null;
                }
                contentFile.CodeLanguage = codeLanguage;
                contentFile.OutputPath = jsonObject.Value<string>("outputPath");
                contentFile.PPOutputPath = jsonObject.Value<string>("ppOutputPath");
                contentFile.CopyToOutput = ReadBool(jsonObject, "copyToOutput", false);
            }

            return contentFile;
        }

        private ProjectFileDependencyGroup ReadProjectFileDependencyGroup(string property, JToken json)
        {
            return new ProjectFileDependencyGroup(
                string.IsNullOrEmpty(property) ? null : NuGetFramework.Parse(property),
                ReadArray(json, ReadString));
        }

        private PackageDependency ReadPackageDependency(string property, JToken json)
        {
            var versionStr = ReadString(json);
            return new PackageDependency(
                _symbols.GetString(property),
                versionStr == null ? null : _symbols.GetVersionRange(versionStr));
        }

        private LockFileItem ReadFileItem(string property, JToken json)
        {
            var item = new LockFileItem { Path = _symbols.GetString(PathUtility.GetPathWithDirectorySeparator(property)) };
            var jobject = json as JObject;

            if (jobject != null)
            {
                foreach (var child in jobject)
                {
                    item.Properties[_symbols.GetString(child.Key)] = jobject.Value<string>(child.Key);
                }
            }
            return item;
        }

        private string ReadFrameworkAssemblyReference(JToken json)
        {
            return ReadString(json);
        }

        private static IList<TItem> ReadArray<TItem>(JToken json, Func<JToken, TItem> readItem)
        {
            if (json == null)
            {
                return new List<TItem>();
            }

            var jarray = json as JArray;
            if (jarray == null)
            {
                throw FileFormatException.Create("The value type is not array.", json);
            }

            var items = new List<TItem>();
            for (int i = 0; i < jarray.Count; ++i)
            {
                items.Add(readItem(jarray[i]));
            }
            return items;
        }

        private IList<string> ReadPathArray(JToken json, Func<JToken, string> readItem)
        {
            return ReadArray(json, readItem).Select(f => _symbols.GetString(PathUtility.GetPathWithDirectorySeparator(f))).ToList();
        }

        private static IList<TItem> ReadObject<TItem>(JObject json, Func<string, JToken, TItem> readItem)
        {
            if (json == null)
            {
                return new List<TItem>();
            }
            var items = new List<TItem>();
            foreach (var child in json)
            {
                items.Add(readItem(child.Key, json[child.Key]));
            }
            return items;
        }

        private static bool ReadBool(JObject cursor, string property, bool defaultValue)
        {
            var valueToken = cursor[property] as JValue;
            if (valueToken == null || valueToken.Type != JTokenType.Boolean)
            {
                return defaultValue;
            }

            return (bool)valueToken.Value;
        }

        private static int ReadInt(JObject cursor, string property, int defaultValue)
        {
            var number = cursor[property] as JValue;
            if (number == null || number.Type != JTokenType.Integer)
            {
                return defaultValue;
            }

            try
            {
                var resultInInt = Convert.ToInt32(number.Value);
                return resultInInt;
            }
            catch (Exception ex)
            {
                // FormatException or OverflowException
                throw FileFormatException.Create(ex, cursor);
            }
        }

        private string ReadString(JToken json)
        {
            if (json.Type == JTokenType.String)
            {
                return _symbols.GetString(json.ToString());
            }
            else if (json.Type == JTokenType.Null)
            {
                return null;
            }
            else
            {
                throw FileFormatException.Create("The value type is not string.", json);
            }
        }
    }
}
