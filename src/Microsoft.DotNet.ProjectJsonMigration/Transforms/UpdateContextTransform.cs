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
                    includeContext => FormatGlobPatternsForMsbuild(includeContext.IncludeFiles, includeContext.SourceBasePath),
                    includeContext => includeContext != null
                        && includeContext.IncludeFiles != null
                        && includeContext.IncludeFiles.Count > 0);

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
                                         .Union(includeContext.BuiltInsInclude.OrEmptyIfNull());

                    return FormatGlobPatternsForMsbuild(fullIncludeSet, includeContext.SourceBasePath);
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

        public UpdateContextTransform(
            string itemName,
            bool transformMappings = true,
            Func<IncludeContext, bool> condition = null) : base(itemName, transformMappings, condition)
        {
        }
    }
}