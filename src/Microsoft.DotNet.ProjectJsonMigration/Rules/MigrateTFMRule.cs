using System;
using System.Text;
using System.Globalization;
using Microsoft.Build.Construction;
using System.Linq;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    // TODO: Support Multi-TFM
    public class MigrateTFMRule : IMigrationRule
    {
        private readonly ITransformApplicator _transformApplicator;
        private readonly AddPropertyTransform<NuGetFramework>[] _transforms;

        public MigrateTFMRule(TransformApplicator transformApplicator = null)
        {
            _transformApplicator = transformApplicator ?? new TransformApplicator();

            _transforms = new AddPropertyTransform<NuGetFramework>[]
            {
                OutputPathTransform,
                FrameworkIdentifierTransform,
                FrameworkVersionTransform
            };
        }

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var csproj = migrationRuleInputs.OutputMSBuildProject;
            var propertyGroup = migrationRuleInputs.CommonPropertyGroup;

            CleanExistingProperties(csproj);

            foreach (var transform in _transforms)
            {
                _transformApplicator.Execute(
                    transform.Transform(migrationRuleInputs.DefaultProjectContext.TargetFramework),
                    propertyGroup);
            }
        }

        private void CleanExistingProperties(ProjectRootElement csproj)
        {
            var existingPropertiesToRemove = new string[] { "TargetFrameworkIdentifier", "TargetFrameworkVersion" };
            foreach (var propertyName in existingPropertiesToRemove)
            {
                var properties = csproj.Properties.Where(p => p.Name == propertyName);

                foreach (var property in properties)
                {
                    property.Parent.RemoveChild(property);
                }
            }
        }

        // Taken from private NuGet.Frameworks method
        // https://github.com/NuGet/NuGet.Client/blob/33b8f85a94b01f805f1e955f9b68992b297fad6e/src/NuGet.Core/NuGet.Frameworks/NuGetFramework.cs#L234
        private static string GetDisplayVersion(Version version)
        {
            var sb = new StringBuilder(String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor));

            if (version.Build > 0
                || version.Revision > 0)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, ".{0}", version.Build);

                if (version.Revision > 0)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, ".{0}", version.Revision);
                }
            }

            return sb.ToString();
        }

        // TODO: When we have this inferred in the sdk targets, we won't need this
        private AddPropertyTransform<NuGetFramework> OutputPathTransform =>
            new AddPropertyTransform<NuGetFramework>("OutputPath",
                f => $"bin/$(Configuration)/{f.GetShortFolderName()}",
                f => true);

        private AddPropertyTransform<NuGetFramework> FrameworkIdentifierTransform =>
            new AddPropertyTransform<NuGetFramework>("TargetFrameworkIdentifier",
                f => f.Framework,
                f => true);

        private AddPropertyTransform<NuGetFramework> FrameworkVersionTransform =>
            new AddPropertyTransform<NuGetFramework>("TargetFrameworkVersion",
                f => "v" + GetDisplayVersion(f.Version),
                f => true);
    }
}
