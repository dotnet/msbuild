// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Utilities;
using Microsoft.Extensions.JsonParser.Sources;
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
                var jobject = JsonDeserializer.Deserialize(reader) as JsonObject;

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
                    var rootJObject = JsonDeserializer.Deserialize(new StreamReader(stream)) as JsonObject;

                    if (rootJObject == null)
                    {
                        throw new InvalidDataException();
                    }

                    var version = ReadInt(rootJObject, "version", defaultValue: int.MinValue);
                    var exports = ReadObject(rootJObject.ValueAsJsonObject("exports"), ReadTargetLibrary);

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

        private LockFile ReadLockFile(string lockFilePath, JsonObject cursor)
        {
            var lockFile = new LockFile(lockFilePath);
            lockFile.Version = ReadInt(cursor, "version", defaultValue: int.MinValue);
            lockFile.Targets = ReadObject(cursor.ValueAsJsonObject("targets"), ReadTarget);
            lockFile.ProjectFileDependencyGroups = ReadObject(cursor.ValueAsJsonObject("projectFileDependencyGroups"), ReadProjectFileDependencyGroup);
            ReadLibrary(cursor.ValueAsJsonObject("libraries"), lockFile);

            return lockFile;
        }

        private void ReadLibrary(JsonObject json, LockFile lockFile)
        {
            if (json == null)
            {
                return;
            }

            foreach (var key in json.Keys)
            {
                var value = json.ValueAsJsonObject(key);
                if (value == null)
                {
                    throw FileFormatException.Create("The value type is not object.", json.Value(key));
                }

                var parts = key.Split(new[] { '/' }, 2);
                var name = parts[0];
                var version = parts.Length == 2 ? _symbols.GetVersion(parts[1]) : null;

                var type = _symbols.GetString(value.ValueAsString("type")?.Value);

                if (type == null || string.Equals(type, "package", StringComparison.OrdinalIgnoreCase))
                {
                    lockFile.PackageLibraries.Add(new LockFilePackageLibrary
                    {
                        Name = name,
                        Version = version,
                        IsServiceable = ReadBool(value, "serviceable", defaultValue: false),
                        Sha512 = ReadString(value.Value("sha512")),
                        Files = ReadPathArray(value.Value("files"), ReadString)
                    });
                }
                else if (type == "project")
                {
                    var projectLibrary = new LockFileProjectLibrary
                    {
                        Name = name,
                        Version = version
                    };

                    var pathValue = value.Value("path");
                    projectLibrary.Path = pathValue == null ? null : ReadString(pathValue);

                    var buildTimeDependencyValue = value.Value("msbuildProject");
                    projectLibrary.MSBuildProject = buildTimeDependencyValue == null ? null : ReadString(buildTimeDependencyValue);

                    lockFile.ProjectLibraries.Add(projectLibrary);
                }
            }
        }

        private LockFileTarget ReadTarget(string property, JsonValue json)
        {
            var jobject = json as JsonObject;
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

        private LockFileTargetLibrary ReadTargetLibrary(string property, JsonValue json)
        {
            var jobject = json as JsonObject;
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

            library.Type = _symbols.GetString(jobject.ValueAsString("type"));
            var framework = jobject.ValueAsString("framework");
            if (framework != null)
            {
                library.TargetFramework = _symbols.GetFramework(framework);
            }

            library.Dependencies = ReadObject(jobject.ValueAsJsonObject("dependencies"), ReadPackageDependency);
            library.FrameworkAssemblies = new HashSet<string>(ReadArray(jobject.Value("frameworkAssemblies"), ReadFrameworkAssemblyReference), StringComparer.OrdinalIgnoreCase);
            library.RuntimeAssemblies = ReadObject(jobject.ValueAsJsonObject("runtime"), ReadFileItem);
            library.CompileTimeAssemblies = ReadObject(jobject.ValueAsJsonObject("compile"), ReadFileItem);
            library.ResourceAssemblies = ReadObject(jobject.ValueAsJsonObject("resource"), ReadFileItem);
            library.NativeLibraries = ReadObject(jobject.ValueAsJsonObject("native"), ReadFileItem);
            library.ContentFiles = ReadObject(jobject.ValueAsJsonObject("contentFiles"), ReadContentFile);
            library.RuntimeTargets = ReadObject(jobject.ValueAsJsonObject("runtimeTargets"), ReadRuntimeTarget);

            return library;
        }

        private LockFileRuntimeTarget ReadRuntimeTarget(string property, JsonValue json)
        {
            var jsonObject = json as JsonObject;
            if (jsonObject == null)
            {
                throw FileFormatException.Create("The value type is not an object.", json);
            }

            return new LockFileRuntimeTarget(
                path: _symbols.GetString(property),
                runtime: _symbols.GetString(jsonObject.ValueAsString("rid")),
                assetType: _symbols.GetString(jsonObject.ValueAsString("assetType"))
                );
        }

        private LockFileContentFile ReadContentFile(string property, JsonValue json)
        {
            var contentFile = new LockFileContentFile()
            {
                Path = property
            };

            var jsonObject = json as JsonObject;
            if (jsonObject != null)
            {

                BuildAction action;
                BuildAction.TryParse(jsonObject.ValueAsString("buildAction"), out action);

                contentFile.BuildAction = action;
                var codeLanguage = _symbols.GetString(jsonObject.ValueAsString("codeLanguage"));
                if (codeLanguage == "any")
                {
                    codeLanguage = null;
                }
                contentFile.CodeLanguage = codeLanguage;
                contentFile.OutputPath = jsonObject.ValueAsString("outputPath");
                contentFile.PPOutputPath = jsonObject.ValueAsString("ppOutputPath");
                contentFile.CopyToOutput = ReadBool(jsonObject, "copyToOutput", false);
            }

            return contentFile;
        }

        private ProjectFileDependencyGroup ReadProjectFileDependencyGroup(string property, JsonValue json)
        {
            return new ProjectFileDependencyGroup(
                string.IsNullOrEmpty(property) ? null : NuGetFramework.Parse(property),
                ReadArray(json, ReadString));
        }

        private PackageDependency ReadPackageDependency(string property, JsonValue json)
        {
            var versionStr = ReadString(json);
            return new PackageDependency(
                _symbols.GetString(property),
                versionStr == null ? null : _symbols.GetVersionRange(versionStr));
        }

        private LockFileItem ReadFileItem(string property, JsonValue json)
        {
            var item = new LockFileItem { Path = _symbols.GetString(PathUtility.GetPathWithDirectorySeparator(property)) };
            var jobject = json as JsonObject;

            if (jobject != null)
            {
                foreach (var subProperty in jobject.Keys)
                {
                    item.Properties[_symbols.GetString(subProperty)] = jobject.ValueAsString(subProperty);
                }
            }
            return item;
        }

        private string ReadFrameworkAssemblyReference(JsonValue json)
        {
            return ReadString(json);
        }

        private static IList<TItem> ReadArray<TItem>(JsonValue json, Func<JsonValue, TItem> readItem)
        {
            if (json == null)
            {
                return new List<TItem>();
            }

            var jarray = json as JsonArray;
            if (jarray == null)
            {
                throw FileFormatException.Create("The value type is not array.", json);
            }

            var items = new List<TItem>();
            for (int i = 0; i < jarray.Length; ++i)
            {
                items.Add(readItem(jarray[i]));
            }
            return items;
        }

        private IList<string> ReadPathArray(JsonValue json, Func<JsonValue, string> readItem)
        {
            return ReadArray(json, readItem).Select(f => _symbols.GetString(PathUtility.GetPathWithDirectorySeparator(f))).ToList();
        }

        private static IList<TItem> ReadObject<TItem>(JsonObject json, Func<string, JsonValue, TItem> readItem)
        {
            if (json == null)
            {
                return new List<TItem>();
            }
            var items = new List<TItem>();
            foreach (var childKey in json.Keys)
            {
                items.Add(readItem(childKey, json.Value(childKey)));
            }
            return items;
        }

        private static bool ReadBool(JsonObject cursor, string property, bool defaultValue)
        {
            var valueToken = cursor.Value(property) as JsonBoolean;
            if (valueToken == null)
            {
                return defaultValue;
            }

            return valueToken.Value;
        }

        private static int ReadInt(JsonObject cursor, string property, int defaultValue)
        {
            var number = cursor.Value(property) as JsonNumber;
            if (number == null)
            {
                return defaultValue;
            }

            try
            {
                var resultInInt = Convert.ToInt32(number.Raw);
                return resultInInt;
            }
            catch (Exception ex)
            {
                // FormatException or OverflowException
                throw FileFormatException.Create(ex, cursor);
            }
        }

        private string ReadString(JsonValue json)
        {
            if (json is JsonString)
            {
                return _symbols.GetString((json as JsonString).Value);
            }
            else if (json is JsonNull)
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
