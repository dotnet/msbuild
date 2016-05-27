// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Tools.Build
{
    internal class IncrementalCache
    {
        private const string BuildArgumentsKeyName = "buildArguments";
        private const string InputsKeyName = "inputs";
        private const string OutputsKeyNane = "outputs";

        public CompilerIO CompilerIO { get; }

        /// <summary>
        /// Captures parameters that affect compilation outputs.
        /// </summary>
        public IDictionary<string, string> BuildArguments { get; }

        public IncrementalCache(CompilerIO compilerIO, IEnumerable<KeyValuePair<string, string>> parameters)
        {
            CompilerIO = compilerIO;
            BuildArguments = parameters.ToDictionary(p => p.Key, p => p.Value);
        }

        public void WriteToFile(string cacheFile)
        {
            try
            {
                CreatePathIfAbsent(cacheFile);

                using (var streamWriter = new StreamWriter(new FileStream(cacheFile, FileMode.Create, FileAccess.Write, FileShare.None)))
                {
                    var rootObject = new JObject();
                    rootObject[InputsKeyName] = new JArray(CompilerIO.Inputs);
                    rootObject[OutputsKeyNane] = new JArray(CompilerIO.Outputs);
                    rootObject[BuildArgumentsKeyName] = JObject.FromObject(BuildArguments);

                    JsonSerializer.Create().Serialize(streamWriter, rootObject);
                }
            }
            catch (Exception e)
            {
                throw new InvalidDataException($"Could not write the incremental cache file: {cacheFile}", e);
            }
        }

        private static void CreatePathIfAbsent(string filePath)
        {
            var parentDir = Path.GetDirectoryName(filePath);

            if (!Directory.Exists(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }
        }

        public static IncrementalCache ReadFromFile(string cacheFile)
        {
            try
            {
                using (var streamReader = new StreamReader(new FileStream(cacheFile, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    var jObject = JObject.Parse(streamReader.ReadToEnd());

                    if (jObject == null)
                    {
                        throw new InvalidDataException();
                    }

                    var inputs = ReadArray<string>(jObject, InputsKeyName);
                    var outputs = ReadArray<string>(jObject, OutputsKeyNane);
                    var parameters = ReadDictionary(jObject, BuildArgumentsKeyName);

                    return new IncrementalCache(new CompilerIO(inputs, outputs), parameters);
                }
            }
            catch (Exception e)
            {
                throw new InvalidDataException($"Could not read the incremental cache file: {cacheFile}", e);
            }
        }

        private static IEnumerable<KeyValuePair<string, string>> ReadDictionary(JObject jObject, string keyName)
        {
            var obj = jObject[keyName] as JObject;

            if(obj == null)
            {
                return Enumerable.Empty<KeyValuePair<string, string>>();
            }

            return obj.Properties().ToDictionary(p => p.Name, p => p.Value.ToString());
        }

        private static IEnumerable<T> ReadArray<T>(JObject jObject, string keyName)
        {
            var array = jObject.Value<JToken>(keyName)?.Values<T>();

            if (array == null)
            {
                throw new InvalidDataException($"Could not read key {keyName}");
            }

            return array;
        }
    }
}
