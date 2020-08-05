using System;

namespace Microsoft.TemplateEngine.Cli
{
    public struct InstallationRequest : IEquatable<InstallationRequest>
    {
        public InstallationRequest(string installString, bool isPartOfAnOptionalWorkload = false)
        {
            InstallString = installString;
            IsPartOfAnOptionalWorkload = isPartOfAnOptionalWorkload;
        }

        /// <summary>
        /// String to identify the package/directory/archive containing the templates
        /// </summary>
        public string InstallString { get; set; }

        /// <summary>
        /// Is this package being installed as part of an optional workload?
        /// </summary>
        public bool IsPartOfAnOptionalWorkload { get; set; }

        public bool Equals(InstallationRequest other)
        {
            return InstallString == other.InstallString &&
                IsPartOfAnOptionalWorkload == other.IsPartOfAnOptionalWorkload;
        }

        public override string ToString()
        {
            return $"{(IsPartOfAnOptionalWorkload ? "[OW]" : string.Empty)}{InstallString}";
        }
    }
}
