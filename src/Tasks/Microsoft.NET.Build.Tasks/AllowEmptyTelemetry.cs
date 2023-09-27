// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.NET.Build.Tasks;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Task that logs telemetry.
    /// </summary>
    public sealed class AllowEmptyTelemetry : TaskBase
    {
        public AllowEmptyTelemetry()
        {
            EventData = Array.Empty<ITaskItem>();
            EventName = string.Empty;
        }

        /// <summary>
        /// Gets or sets an array of ITaskItems. The ItemSpec of the item will be the 'key' of the telemetry event property. The 'Value' metadata will be the value.
        /// If the 'Hash' metadata is present and set to 'true', the value will be hashed before being logged.
        /// </summary>
        public ITaskItem[] EventData { get; set; }

        [Required] public string EventName { get; set; }

        protected override void ExecuteCore()
        {
            if (EventData == null)
            {
                (BuildEngine as IBuildEngine5)?.LogTelemetry(EventName, null);
            }
            else
            {
                IDictionary<string, string> properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in EventData)
                {
                    var key = item.ItemSpec;
                    var availableNames = item.MetadataNames.Cast<string>();
                    var hasValue = availableNames.Contains("Value");
                    var value = hasValue ? item.GetMetadata("Value") : null;
                    var hasHash = availableNames.Contains("Hash");
                    var hash = hasHash ? bool.Parse(item.GetMetadata("Hash")) : false;
                    if (hash && value != null)
                    {
                        value = HashWithNormalizedCasing(value);
                    }
                    if (string.IsNullOrEmpty(value))
                    {
                        properties[key] = "null";
                    }
                    else
                    {
                        properties[key] = value;
                    }
                }
                (BuildEngine as IBuildEngine5)?.LogTelemetry(EventName, properties);
            }
        }

        // A local copy of Sha256Hasher.HashWithNormalizedCasing from the Microsoft.DotNet.Cli.Utils project.
        // We don't want to introduce a project<->project dependency, and the logic is straightforward enough.
        private static string HashWithNormalizedCasing(string text)
        {
            var utf8UpperBytes = Encoding.UTF8.GetBytes(text.ToUpperInvariant());
#if NETFRAMEWORK
            var crypt = System.Security.Cryptography.SHA256.Create();
            var hash = new System.Text.StringBuilder();
            byte[] crypto = crypt.ComputeHash(utf8UpperBytes);
            foreach (byte theByte in crypto)
            {
                hash.Append(theByte.ToString("x2"));
            }
            return hash.ToString().ToLowerInvariant();
#endif
#if NET
            byte[] hash = System.Security.Cryptography.SHA256.HashData(utf8UpperBytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
#endif
        }
    }
}
