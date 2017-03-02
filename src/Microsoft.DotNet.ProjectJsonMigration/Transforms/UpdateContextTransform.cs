// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Internal.ProjectModel.Files;
using System;
using System.Linq;

namespace Microsoft.DotNet.ProjectJsonMigration.Transforms
{
    internal class UpdateContextTransform : IncludeContextTransform
    {
        protected override Func<string, AddItemTransform<IncludeContext>> IncludeFilesExcludeFilesTransformGetter =>
            (itemName) =>
                new AddItemTransform<IncludeContext>(
                    itemName,
                    includeContext => string.Empty,
                    includeContext => FormatGlobPatternsForMsbuild(includeContext.ExcludeFiles, includeContext.SourceBasePath),
                    includeContext => FormatGlobPatternsForMsbuild(
                        includeContext.IncludeFiles.OrEmptyIfNull().Where(
                            pattern => !ExcludePatternRule(pattern)), includeContext.SourceBasePath),
                    includeContext => includeContext != null
                        && includeContext.IncludeFiles != null
                        && includeContext.IncludeFiles.Where(
                            pattern => !ExcludePatternRule(pattern)).Count() > 0);

        protected override Func<string, AddItemTransform<IncludeContext>> IncludeExcludeTransformGetter =>
            (itemName) => new AddItemTransform<IncludeContext>(
                itemName,
                includeContext => string.Empty,
                includeContext =>
                {
                    var fullExcludeSet = includeContext.ExcludePatterns.OrEmptyIfNull()
                                         .Union(includeContext.BuiltInsExclude.OrEmptyIfNull())
                                         .Union(includeContext.ExcludeFiles.OrEmptyIfNull());

                    return FormatGlobPatternsForMsbuild(fullExcludeSet, includeContext.SourceBasePath);
                },
                includeContext =>
                {
                    var fullIncludeSet = includeContext.IncludePatterns.OrEmptyIfNull()
                                         .Union(includeContext.BuiltInsInclude.OrEmptyIfNull())
                                         .Union(includeContext.CustomIncludePatterns.OrEmptyIfNull());

                    return FormatGlobPatternsForMsbuild(
                        fullIncludeSet.Where(pattern => !ExcludePatternRule(pattern)),
                        includeContext.SourceBasePath);
                },
                includeContext =>
                {
                    return includeContext != null &&includeContext.IncludePatterns.OrEmptyIfNull()
                                         .Union(includeContext.BuiltInsInclude.OrEmptyIfNull())
                                         .Union(includeContext.CustomIncludePatterns.OrEmptyIfNull())
                                         .Where(pattern => !ExcludePatternRule(pattern)).Count() > 0;
                });

        public UpdateContextTransform(
            string itemName,
            bool transformMappings = true,
            Func<IncludeContext, bool> condition = null,
            Func<string, bool> excludePatternsRule = null) : base(
                itemName,
                transformMappings,
                condition,
                excludePatternsRule: excludePatternsRule)
        {
        }
    }
}