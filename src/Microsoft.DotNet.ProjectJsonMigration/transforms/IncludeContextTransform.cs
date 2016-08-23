using Microsoft.DotNet.ProjectModel.Files;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectJsonMigration.Models;

namespace Microsoft.DotNet.ProjectJsonMigration.Transforms
{
    public class IncludeContextTransform : ConditionalTransform<IncludeContext, IEnumerable<ProjectItemElement>>
    {
        // TODO: If a directory is specified in project.json does this need to be replaced with a glob in msbuild?
        //     - Partially solved, what if the resolved glob is a directory?
        // TODO: Support mappings

        private readonly string _itemName;
        private bool _transformMappings;
        private readonly List<ItemMetadataValue<IncludeContext>> _metadata = new List<ItemMetadataValue<IncludeContext>>();
        private AddItemTransform<IncludeContext>[] _transformSet;

        public IncludeContextTransform(
            string itemName,
            bool transformMappings = true,
            Func<IncludeContext, bool> condition = null) : base(condition)
        {
            _itemName = itemName;
            _transformMappings = transformMappings;
        }

        public IncludeContextTransform WithMetadata(string metadataName, string metadataValue)
        {
            _metadata.Add(new ItemMetadataValue<IncludeContext>(metadataName, metadataValue));
            return this;
        }

        public IncludeContextTransform WithMetadata(string metadataName, Func<IncludeContext, string> metadataValueFunc)
        {
            _metadata.Add(new ItemMetadataValue<IncludeContext>(metadataName, metadataValueFunc));
            return this;
        }

        private void CreateTransformSet()
        {
            var includeFilesExcludeFilesTransformation = new AddItemTransform<IncludeContext>(
                _itemName,
                includeContext => FormatPatterns(includeContext.IncludeFiles, includeContext.SourceBasePath),
                includeContext => FormatPatterns(includeContext.ExcludeFiles, includeContext.SourceBasePath),
                includeContext => includeContext != null && includeContext.IncludeFiles.Count > 0);

            var includeExcludeTransformation = new AddItemTransform<IncludeContext>(
                _itemName,
                includeContext => 
                {
                    var fullIncludeSet = includeContext.IncludePatterns.OrEmptyIfNull()
                                         .Union(includeContext.BuiltInsInclude.OrEmptyIfNull());

                    return FormatPatterns(fullIncludeSet, includeContext.SourceBasePath);
                },
                includeContext =>
                {
                    var fullExcludeSet = includeContext.ExcludePatterns.OrEmptyIfNull()
                                         .Union(includeContext.BuiltInsExclude.OrEmptyIfNull())
                                         .Union(includeContext.ExcludeFiles.OrEmptyIfNull());

                    return FormatPatterns(fullExcludeSet, includeContext.SourceBasePath);
                },
                includeContext => 
                {
                    return includeContext != null &&
                        ( 
                            (includeContext.IncludePatterns != null && includeContext.IncludePatterns.Count > 0)
                            ||
                            (includeContext.BuiltInsInclude != null && includeContext.BuiltInsInclude.Count > 0)
                        );
                });

            foreach (var metadata in _metadata)
            {
                includeFilesExcludeFilesTransformation.WithMetadata(metadata);
                includeExcludeTransformation.WithMetadata(metadata);
            }

            _transformSet = new []
            {
                includeFilesExcludeFilesTransformation,
                includeExcludeTransformation
            };
        }

        private string FormatPatterns(IEnumerable<string> patterns, string projectDirectory)
        {
            List<string> mutatedPatterns = new List<string>(patterns.Count());

            foreach (var pattern in patterns)
            {
                // Do not use forward slashes
                // https://github.com/Microsoft/msbuild/issues/724
                var mutatedPattern = pattern.Replace('/', '\\');

                // MSBuild cannot copy directories
                mutatedPattern = ReplaceDirectoriesWithGlobs(mutatedPattern, projectDirectory);

                mutatedPatterns.Add(mutatedPattern);
            }

            return string.Join(";", mutatedPatterns);
        }

        private string ReplaceDirectoriesWithGlobs(string pattern, string projectDirectory)
        {
            if (PatternIsDirectory(pattern, projectDirectory))
            {
                return $"{pattern.TrimEnd(new char[] { '\\' })}\\**\\*";
            }
            else
            {
                return pattern;
            }
        }

        private bool PatternIsDirectory(string pattern, string projectDirectory)
        {
            // TODO: what about /some/path/**/somedir?
            // Should this even be migrated?
            var path = pattern;

            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(projectDirectory, path);
            }

            return Directory.Exists(path);
        }

        public override IEnumerable<ProjectItemElement> ConditionallyTransform(IncludeContext source)
        {
            CreateTransformSet();

            return _transformSet.Select(t => t.Transform(source));
        }
    }
}
