// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Files;

namespace Microsoft.DotNet.ProjectModel
{
    public class CommonCompilerOptions
    {
        public IEnumerable<string> Defines { get; set; }

        public string LanguageVersion { get; set; }

        public string Platform { get; set; }

        public bool? AllowUnsafe { get; set; }

        public bool? WarningsAsErrors { get; set; }

        public bool? Optimize { get; set; }

        public string KeyFile { get; set; }

        public bool? DelaySign { get; set; }

        public bool? PublicSign { get; set; }

        public string DebugType { get; set; }

        public bool? EmitEntryPoint { get; set; }

        public bool? PreserveCompilationContext { get; set; }

        public bool? GenerateXmlDocumentation { get; set; }

        public IEnumerable<string> SuppressWarnings { get; set; }

        public IEnumerable<string> AdditionalArguments { get; set; }

        public string OutputName { get; set; }

        public string CompilerName { get; set; }

        public IncludeContext CompileInclude { get; set; }

        public IncludeContext EmbedInclude { get; set; }

        public IncludeContext CopyToOutputInclude { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as CommonCompilerOptions;
            return other != null &&
                   LanguageVersion == other.LanguageVersion &&
                   Platform == other.Platform &&
                   AllowUnsafe == other.AllowUnsafe &&
                   WarningsAsErrors == other.WarningsAsErrors &&
                   Optimize == other.Optimize &&
                   KeyFile == other.KeyFile &&
                   DelaySign == other.DelaySign &&
                   PublicSign == other.PublicSign &&
                   DebugType == other.DebugType &&
                   EmitEntryPoint == other.EmitEntryPoint &&
                   GenerateXmlDocumentation == other.GenerateXmlDocumentation &&
                   PreserveCompilationContext == other.PreserveCompilationContext &&
                   EnumerableEquals(Defines, other.Defines) &&
                   EnumerableEquals(SuppressWarnings, other.SuppressWarnings) &&
                   EnumerableEquals(AdditionalArguments, other.AdditionalArguments) &&
                   OutputName == other.OutputName &&
                   CompilerName == other.CompilerName &&
                   IsEqual(CompileInclude, other.CompileInclude) &&
                   IsEqual(EmbedInclude, other.EmbedInclude) &&
                   IsEqual(CopyToOutputInclude, other.CopyToOutputInclude);
        }

        private static bool IsEqual(IncludeContext first, IncludeContext second)
        {
            if (first == null || second == null)
            {
                return first == second;
            }

            return first.Equals(second);
        }

        private static bool EnumerableEquals(IEnumerable<string> left, IEnumerable<string> right)
            => Enumerable.SequenceEqual(left ?? EmptyArray<string>.Value, right ?? EmptyArray<string>.Value);

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        private static IEnumerable<string> Combine(IEnumerable<string> @new, IEnumerable<string> old)
        {
            if (@new != null)
            {
                old = old ?? EmptyArray<string>.Value;
                return old.Concat(@new).Distinct().ToArray();
            }
            return old;
        }

        public static CommonCompilerOptions Combine(params CommonCompilerOptions[] options)
        {
            var result = new CommonCompilerOptions();
            foreach (var option in options)
            {
                // Skip null options
                if (option == null)
                {
                    continue;
                }

                // Defines, suppressions, and additional arguments are always combined
                result.Defines = Combine(option.Defines, result.Defines);
                result.SuppressWarnings = Combine(option.SuppressWarnings, result.SuppressWarnings);
                result.AdditionalArguments = Combine(option.AdditionalArguments, result.AdditionalArguments);

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

                if (option.PublicSign != null)
                {
                    result.PublicSign = option.PublicSign;
                }

                if (option.DebugType != null)
                {
                    result.DebugType = option.DebugType;
                }

                if (option.EmitEntryPoint != null)
                {
                    result.EmitEntryPoint = option.EmitEntryPoint;
                }

                if (option.PreserveCompilationContext != null)
                {
                    result.PreserveCompilationContext = option.PreserveCompilationContext;
                }

                if (option.GenerateXmlDocumentation != null)
                {
                    result.GenerateXmlDocumentation = option.GenerateXmlDocumentation;
                }

                if (option.OutputName != null)
                {
                    result.OutputName = option.OutputName;
                }

                if (option.CompileInclude != null)
                {
                    result.CompileInclude = option.CompileInclude;
                }

                if (option.EmbedInclude != null)
                {
                    result.EmbedInclude = option.EmbedInclude;
                }

                if (option.CopyToOutputInclude != null)
                {
                    result.CopyToOutputInclude = option.CopyToOutputInclude;
                }

                // compilerName set in the root cannot be overriden.
                if (result.CompilerName == null)
                {
                    result.CompilerName = option.CompilerName;
                }
            }

            return result;
        }
    }
}
