// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

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
