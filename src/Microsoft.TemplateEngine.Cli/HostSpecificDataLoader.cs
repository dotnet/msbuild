// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#nullable enable

namespace Microsoft.TemplateEngine.Cli
{
    public class HostSpecificDataLoader : IHostSpecificDataLoader
    {
        private readonly ISettingsLoader _settingsLoader;

        public HostSpecificDataLoader(ISettingsLoader settingsLoader)
        {
            _settingsLoader = settingsLoader;
        }

        public HostSpecificTemplateData ReadHostSpecificTemplateData(ITemplateInfo templateInfo)
        {
            IMountPoint? mountPoint = null;

            try
            {
                if (!string.IsNullOrEmpty(templateInfo.HostConfigPlace) && _settingsLoader.TryGetMountPoint(templateInfo.MountPointUri, out mountPoint))
                {
                    var file = mountPoint.FileInfo(templateInfo.HostConfigPlace);
                    if (file != null && file.Exists)
                    {
                        JObject jsonData;
                        using (Stream stream = file.OpenRead())
                        using (TextReader textReader = new StreamReader(stream, true))
                        using (JsonReader jsonReader = new JsonTextReader(textReader))
                        {
                            jsonData = JObject.Load(jsonReader);
                        }

                        return jsonData.ToObject<HostSpecificTemplateData>();
                    }
                }
            }
            catch
            {
                // ignore malformed host files
            }
            finally
            {
                mountPoint?.Dispose();
            }

            return HostSpecificTemplateData.Default;
        }
    }
}
