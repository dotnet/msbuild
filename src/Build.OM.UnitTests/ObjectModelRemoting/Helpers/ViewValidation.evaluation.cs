// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Build.Evaluation;
    using Xunit;
    using Microsoft.Build.Framework;

    internal class ProjectPair : LinkPair<Project>
    {
        public ProjectPair(Project view, Project real)
            : base(view, real)
        {
        }

        public void ValidatePropertyValue(string name, string value)
        {
            Assert.Equal(value, this.View.GetPropertyValue(name));
            Assert.Equal(value, this.Real.GetPropertyValue(name));
        }

        private ProjectItem VerifyAfterAddSingleItem(ObjectType where, ICollection<ProjectItem> added, IEnumerable<KeyValuePair<string, string>> metadata)
        {
            Assert.NotNull(added);
            Assert.Equal(1, added.Count);
            var result = added.First();
            Assert.NotNull(result);

            // validate there is exactly 1 item with this include in both view and real and it is the exact same object.
            Assert.Same(result, this.GetSingleItemWithVerify(where, result.EvaluatedInclude));


            if (metadata != null)
            {
                foreach (var m in metadata)
                {
                    Assert.True(result.HasMetadata(m.Key));
                    var md = result.GetMetadata(m.Key);
                    Assert.NotNull(md);
                    Assert.Equal(m.Value, md.UnevaluatedValue);
                }
            }

            return result;
        }

        public ProjectItem AddSingleItemWithVerify(ObjectType where, string itemType, string unevaluatedInclude, IEnumerable<KeyValuePair<string, string>> metadata = null)
        {
            var toAdd = this.Get(where);
            var added = (metadata == null) ? toAdd.AddItem(itemType, unevaluatedInclude) : toAdd.AddItem(itemType, unevaluatedInclude, metadata);
            return VerifyAfterAddSingleItem(where, added, metadata);
        }

        public ProjectItem AddSingleItemFastWithVerify(ObjectType where, string itemType, string unevaluatedInclude, IEnumerable<KeyValuePair<string, string>> metadata = null)
        {
            var toAdd = this.Get(where);
            var added = (metadata == null) ? toAdd.AddItemFast(itemType, unevaluatedInclude) : toAdd.AddItemFast(itemType, unevaluatedInclude, metadata);
            return VerifyAfterAddSingleItem(where, added, metadata);
        }

        public ProjectItem GetSingleItemWithVerify(ObjectType which, string evaluatedInclude)
        {
            var realItems = this.Real.GetItemsByEvaluatedInclude(evaluatedInclude);
            var viewItems = this.View.GetItemsByEvaluatedInclude(evaluatedInclude);

            ViewValidation.Verify(viewItems, realItems, ViewValidation.Verify, new ValidationContext(this));
            if (viewItems == null || viewItems.Count == 0) return null;
            Assert.Equal(1, viewItems.Count);
            return which == ObjectType.View ? viewItems.First() : realItems.First();
        }

        public ProjectProperty SetPropertyWithVerify(ObjectType where, string name, string unevaluatedValue)
        {
            var toAdd = this.Get(where);
            var added = toAdd.SetProperty(name, unevaluatedValue);
            Assert.NotNull(added);
            Assert.Same(added, toAdd.GetProperty(name));
            Assert.Equal(unevaluatedValue, added.UnevaluatedValue);

            var view = this.View.GetProperty(name);
            var real = this.Real.GetProperty(name);
            ViewValidation.Verify(view, real, new ValidationContext(this));

            return added;
        }
    }

    internal static partial class ViewValidation
    {
        public static void Verify(ProjectProperty view, ProjectProperty real, ValidationContext context)
        {
            if (view == null && real == null) return;
            VerifyLinkedNotNull(view);
            VerifyNotLinkedNotNull(real);

            Assert.Equal(real.Name, view.Name);
            Assert.Equal(real.EvaluatedValue, view.EvaluatedValue);
            Assert.Equal(real.UnevaluatedValue, view.UnevaluatedValue);
            Assert.Equal(real.IsEnvironmentProperty, view.IsEnvironmentProperty);
            Assert.Equal(real.IsGlobalProperty, view.IsGlobalProperty);
            Assert.Equal(real.IsReservedProperty, view.IsReservedProperty);
            Assert.Equal(real.IsImported, view.IsImported);

            Verify(view.Xml, real.Xml);

            VerifyLinkedNotNull(view.Project);
            VerifyNotLinkedNotNull(real.Project);
            if (context?.Pair != null)
            {
                Assert.Same(context.Pair.View, view.Project);
                Assert.Same(context.Pair.Real, real.Project);
            }

            Verify(view.Predecessor, real.Predecessor, context);
        }

        // public static void Verify(ProjectMetadata view, ProjectMetadata real) => Verify(view, real, null);
        public static void Verify(ProjectMetadata view, ProjectMetadata real, ValidationContext context)
        {
            if (view == null && real == null) return;
            VerifyLinkedNotNull(view);
            VerifyNotLinkedNotNull(real);

            Assert.Equal(real.Name, view.Name);
            Assert.Equal(real.EvaluatedValue, view.EvaluatedValue);
            Assert.Equal(real.UnevaluatedValue, view.UnevaluatedValue);
            Assert.Equal(real.ItemType, view.ItemType);
            Assert.Equal(real.IsImported, view.IsImported);

            VerifySameLocation(real.Location, view.Location);
            VerifySameLocation(real.ConditionLocation, view.ConditionLocation);

            Verify(view.Xml, real.Xml);

            VerifyLinkedNotNull(view.Project);
            VerifyNotLinkedNotNull(real.Project);
            if (context?.Pair != null)
            {
                Assert.Same(context?.Pair.View, view.Project);
                Assert.Same(context?.Pair.Real, real.Project);
            }

            Verify(view.Predecessor, real.Predecessor, context);
        }

        // public static void Verify(ProjectItemDefinition view, ProjectItemDefinition real) => Verify(view, real, null);
        public static void Verify(ProjectItemDefinition view, ProjectItemDefinition real, ValidationContext context)
        {
            if (view == null && real == null) return;
            VerifyLinkedNotNull(view);
            VerifyNotLinkedNotNull(real);

            // note ItemDefinition does not have a XML element
            // this is since it is [or can be] a aggregation of multiple ProjectItemDefinitionElement's.
            // This is somewhat of deficiency of MSBuild API.
            // (for example SetMetadata will always create a new ItemDefinitionElement because of that, for new metadata).

            Assert.Equal(real.ItemType, view.ItemType);
            Assert.Equal(real.MetadataCount, view.MetadataCount);

            Verify(view.Metadata, real.Metadata, Verify, context);

            foreach (var rm in real.Metadata)
            {
                var rv = real.GetMetadataValue(rm.Name);
                var vv = view.GetMetadataValue(rm.Name);
                Assert.Equal(rv, vv);

                var grm = real.GetMetadata(rm.Name);
                var gvm = view.GetMetadata(rm.Name);

                Verify(gvm, grm, context);
            }

            VerifyLinkedNotNull(view.Project);
            VerifyNotLinkedNotNull(real.Project);
            if (context?.Pair != null)
            {
                Assert.Same(context?.Pair.View, view.Project);
                Assert.Same(context?.Pair.Real, real.Project);
            }
        }

        // public static void Verify(ProjectItem view, ProjectItem real) => Verify(view, real, null);
        public static void Verify(ProjectItem view, ProjectItem real, ValidationContext context = null)
        {
            if (view == null && real == null) return;
            VerifyLinkedNotNull(view);
            VerifyNotLinkedNotNull(real);

            Verify(view.Xml, real.Xml);

            Assert.Equal(real.ItemType, view.ItemType);
            Assert.Equal(real.UnevaluatedInclude, view.UnevaluatedInclude);
            Assert.Equal(real.EvaluatedInclude, view.EvaluatedInclude);
            Assert.Equal(real.IsImported, view.IsImported);

            Assert.Equal(real.DirectMetadataCount, view.DirectMetadataCount);
            Verify(view.DirectMetadata, real.DirectMetadata, Verify, context);

            Assert.Equal(real.MetadataCount, view.MetadataCount);
            Verify(view.Metadata, real.Metadata, Verify, context);

            foreach (var rm in real.Metadata)
            {
                var rv = real.GetMetadataValue(rm.Name);
                var vv = view.GetMetadataValue(rm.Name);
                Assert.Equal(rv, vv);

                var grm = real.GetMetadata(rm.Name);
                var gvm = view.GetMetadata(rm.Name);

                Verify(gvm, grm, context);

                Assert.Equal(real.HasMetadata(rm.Name), view.HasMetadata(rm.Name));
            }

            Assert.Equal(real.HasMetadata("random non existent"), view.HasMetadata("random non existent"));
            Assert.Equal(real.GetMetadataValue("random non existent"), view.GetMetadataValue("random non existent"));

            VerifyLinkedNotNull(view.Project);
            VerifyNotLinkedNotNull(real.Project);
            if (context?.Pair != null)
            {
                Assert.Same(context.Pair.View, view.Project);
                Assert.Same(context.Pair.Real, real.Project);
            }
        }


        private static void Verify(SdkReference view, SdkReference real, ValidationContext context = null)
        {
            if (view == null && real == null) return;
            Assert.NotNull(view);
            Assert.NotNull(real);

            Assert.Equal(real.Name, view.Name);
            Assert.Equal(real.Version, view.Version);
            Assert.Equal(real.MinimumVersion, view.MinimumVersion);
        }

        private static void Verify(SdkResult view, SdkResult real, ValidationContext context = null)
        {
            if (view == null && real == null) return;
            Assert.NotNull(view);
            Assert.NotNull(real);
            Assert.Equal(real.Success, view.Success);
            Assert.Equal(real.Path, view.Path);
            Verify(view.SdkReference, real.SdkReference, context);
        }

        private static void Verify(ResolvedImport view, ResolvedImport real, ValidationContext context = null)
        {
            Assert.Equal(real.IsImported, view.IsImported);
            Verify(view.ImportingElement, real.ImportingElement);
            Verify(view.ImportedProject, real.ImportedProject);
            Verify(view.SdkResult, real.SdkResult, context);
        }

        private static void Verify(List<string> viewProps, List<string> realProps, ValidationContext context = null)
        {
            if (viewProps == null && realProps == null) return;
            Assert.NotNull(viewProps);
            Assert.NotNull(realProps);
            Assert.Equal(realProps.Count, viewProps.Count);

            for (int i = 0; i< realProps.Count; i++)
            {
                Assert.Equal(realProps[i], viewProps[i]);
            }
        }

        public static void Verify(Project view, Project real, ValidationContext context = null)
        {
            if (view == null && real == null) return;
            var pair = new ProjectPair(view, real);
            Verify(pair, context);
        }

        public static void Verify(ProjectPair pair, ValidationContext context = null)
        {
            if (pair == null) return;
            var real = pair.Real;
            var view = pair.View;
            context ??= new ValidationContext();
            context.Pair = pair;


            Verify(view.Xml, real.Xml);

            Verify(view.ItemsIgnoringCondition, real.ItemsIgnoringCondition, Verify, context);
            Verify(view.Items, real.Items, Verify, context);
            Verify(view.ItemDefinitions, real.ItemDefinitions, Verify, context);
            Verify(view.ConditionedProperties, real.ConditionedProperties, Verify, context);
            Verify(view.Properties, real.Properties, Verify, context);
            Verify(view.GlobalProperties, real.GlobalProperties, (a, b, p) => Assert.Equal(b, a), context);
            Verify(view.Imports, real.Imports, Verify, context);
            Verify(view.ItemTypes, real.ItemTypes, (a, b, p) => Assert.Equal(b, a), context);

            // this can only be used if project is loaded with ProjectLoadSettings.RecordDuplicateButNotCircularImports
            // or it throws otherwise. Slightly odd and inconvenient API design, but thats how it is.
            bool isImportsIncludingDuplicatesAvailable = false;
            try
            {
                var testLoadSettings = real.ImportsIncludingDuplicates;
                isImportsIncludingDuplicatesAvailable = true;
            }
            catch { }

            if (isImportsIncludingDuplicatesAvailable)
            {
                Verify(view.ImportsIncludingDuplicates, real.ImportsIncludingDuplicates, Verify, context);
            }

            Verify(view.AllEvaluatedProperties, real.AllEvaluatedProperties, Verify, context);
            Verify(view.AllEvaluatedItemDefinitionMetadata, real.AllEvaluatedItemDefinitionMetadata, Verify, context);
            Verify(view.AllEvaluatedItems, real.AllEvaluatedItems, Verify, context);

            Assert.NotSame(view.ProjectCollection, real.ProjectCollection);
            Assert.Equal(real.ToolsVersion, view.ToolsVersion);
            Assert.Equal(real.SubToolsetVersion, view.SubToolsetVersion);
            Assert.Equal(real.DirectoryPath, view.DirectoryPath);
            Assert.Equal(real.FullPath, view.FullPath);
            Assert.Equal(real.SkipEvaluation, view.SkipEvaluation);
            Assert.Equal(real.ThrowInsteadOfSplittingItemElement, view.ThrowInsteadOfSplittingItemElement);
            Assert.Equal(real.IsDirty, view.IsDirty);
            Assert.Equal(real.DisableMarkDirty, view.DisableMarkDirty);
            Assert.Equal(real.IsBuildEnabled, view.IsBuildEnabled);

            VerifySameLocation(real.ProjectFileLocation, view.ProjectFileLocation, context);

            // we currently dont support "Execution" remoting.
            // Verify(view.Targets, real.Targets, Verify, view, real);
            Assert.Equal(real.EvaluationCounter, view.EvaluationCounter);
            Assert.Equal(real.LastEvaluationId, view.LastEvaluationId);
        }
    }
}
