// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization.Json;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Sdk.BlazorWebAssembly
{
    public class GenerateBlazorBootExtensionJson : Task
    {
        [Required]
        public string OutputPath { get; set; }

        public override bool Execute()
        {
            using var fileStream = File.Create(OutputPath);

            try
            {
                WriteJson(fileStream);
            }
            catch (Exception ex)
            {
                Log.LogError(ex.ToString());
            }

            return !Log.HasLoggedErrors;
        }

        // Internal for tests
        public void WriteJson(Stream output)
        {
            var result = new BootExtensionJsonData();

            var serializer = new DataContractJsonSerializer(typeof(BootExtensionJsonData), new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true
            });

            using var writer = JsonReaderWriterFactory.CreateJsonWriter(output, Encoding.UTF8, ownsStream: false, indent: true);
            serializer.WriteObject(writer, result);
        }
    }
}
