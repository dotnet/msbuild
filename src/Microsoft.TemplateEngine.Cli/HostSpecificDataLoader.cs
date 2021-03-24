using System.IO;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Cli
{
    public class HostSpecificDataLoader : IHostSpecificDataLoader
    {
        public HostSpecificDataLoader(ISettingsLoader settingsLoader)
        {
            _settingsLoader = settingsLoader;
        }

        private ISettingsLoader _settingsLoader;

        public HostSpecificTemplateData ReadHostSpecificTemplateData(ITemplateInfo templateInfo)
        {
            IMountPoint mountPoint = null;

            try
            {
                if (_settingsLoader.TryGetFileFromIdAndPath(templateInfo.MountPointUri, templateInfo.HostConfigPlace, out IFile file, out mountPoint))
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
