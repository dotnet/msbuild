// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Extensions.ProjectModel
{
    public class CompilerOptions
    {
        public IEnumerable<string> Defines { get; set; }

        public string LanguageVersion { get; set; }

        public string Platform { get; set; }

        public bool? AllowUnsafe { get; set; }

        public bool? WarningsAsErrors { get; set; }

        public bool? Optimize { get; set; }

        public string KeyFile { get; set; }

        public bool? DelaySign { get; set; }

        public bool? StrongName { get; set; }

        public bool? EmitEntryPoint { get; set; }

        public static CompilerOptions Combine(params CompilerOptions[] options)
        {
            var result = new CompilerOptions();
            foreach (var option in options)
            {
                // Skip null options
                if (option == null)
                {
                    continue;
                }

                // Defines are always combined
                if (option.Defines != null)
                {
                    var existing = result.Defines ?? Enumerable.Empty<string>();
                    result.Defines = existing.Concat(option.Defines).Distinct();
                }

                if (option.LanguageVersion != null)
                {
                    result.LanguageVersion = option.LanguageVersion;
                }

                if (option.Platform != null)
                {
                    result.Platform = option.Platform;
                }

                if (option.AllowUnsafe != null)
                {
                    result.AllowUnsafe = option.AllowUnsafe;
                }

                if (option.WarningsAsErrors != null)
                {
                    result.WarningsAsErrors = option.WarningsAsErrors;
                }

                if (option.Optimize != null)
                {
                    result.Optimize = option.Optimize;
                }

                if (option.KeyFile != null)
                {
                    result.KeyFile = option.KeyFile;
                }

                if (option.DelaySign != null)
                {
                    result.DelaySign = option.DelaySign;
                }

                if (option.StrongName != null)
                {
                    result.StrongName = option.StrongName;
                }

                if (option.EmitEntryPoint != null)
                {
                    result.EmitEntryPoint = option.EmitEntryPoint;
                }
            }

            return result;
        }
    }
}