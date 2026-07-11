// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Build.Evaluation;
    using Microsoft.Build.Framework;

    internal sealed class ProjectPair : LinkPair<Project>
    {
        public ProjectPair(Project view, Project real)
            : base(view, real)
        {
        }

        public void ValidatePropertyValue(string name, string value)
        {
            Assert.AreEqual(value, this.View.GetPropertyValue(name));
            Assert.AreEqual(value, this.Real.GetPropertyValue(name));
        }

        private ProjectItem VerifyAfterAddSingleItem(ObjectType where, ICollection<ProjectItem> added, IEnumerable<KeyValuePair<string, string>> metadata)
        {
            Assert.IsNotNull(added);
            Assert.AreEqual(1, added.Count);
            var result = added.First();
            Assert.IsNotNull(result);

            // validate there is exactly 1 item with this include in both view and real and it is the exact same object.
            Assert.AreSame(result, this.GetSingleItemWithVerify(where, result.EvaluatedInclude));


            if (metadata != null)
            {
                foreach (var m in metadata)
                {
                    Assert.IsTrue(result.HasMetadata(m.Key));
                    var md = result.GetMetadata(m.Key);
                    Assert.IsNotNull(md);
                    Assert.AreEqual(m.Value, md.UnevaluatedValue);
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
            if (viewItems == null || viewItems.Count == 0)
            {
                return null;
            }

            Assert.AreEqual(1, viewItems.Count);
            return which == ObjectType.View ? viewItems.First() : realItems.First();
        }

        public ProjectProperty SetPropertyWithVerify(ObjectType where, string name, string unevaluatedValue)
        {
            var toAdd = this.Get(where);
            var added = toAdd.SetProperty(name, unevaluatedValue);
            Assert.IsNotNull(added);
            Assert.AreSame(added, toAdd.GetProperty(name));
            Assert.AreEqual(unevaluatedValue, added.UnevaluatedValue);

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
            if (view == null && real == null)
            {
                return;
            }

            VerifyLinkedNotNull(view);
            VerifyNotLinkedNotNull(real);

            Assert.AreEqual(real.Name, view.Name);
            Assert.AreEqual(real.EvaluatedValue, view.EvaluatedValue);
            Assert.AreEqual(real.UnevaluatedValue, view.UnevaluatedValue);
            Assert.AreEqual(real.IsEnvironmentProperty, view.IsEnvironmentProperty);
            Assert.AreEqual(real.IsGlobalProperty, view.IsGlobalProperty);
            Assert.AreEqual(real.IsReservedProperty, view.IsReservedProperty);
            Assert.AreEqual(real.IsImported, view.IsImported);

            Verify(view.Xml, real.Xml);

            VerifyLinkedNotNull(view.Project);
            VerifyNotLinkedNotNull(real.Project);
            if (context?.Pair != null)
            {
                Assert.AreSame(context.Pair.View, view.Project);
                Assert.AreSame(context.Pair.Real, real.Project);
            }

            Verify(view.Predecessor, real.Predecessor, context);
        }

        // public static void Verify(ProjectMetadata view, ProjectMetadata real) => Verify(view, real, null);
        public static void Verify(ProjectMetadata view, ProjectMetadata real, ValidationContext context)
        {
            if (view == null && real == null)
            {
                return;
            }

            VerifyLinkedNotNull(view);
            VerifyNotLinkedNotNull(real);

            Assert.AreEqual(real.Name, view.Name);
            Assert.AreEqual(real.EvaluatedValue, view.EvaluatedValue);
            Assert.AreEqual(real.UnevaluatedValue, view.UnevaluatedValue);
            Assert.AreEqual(real.ItemType, view.ItemType);
            Assert.AreEqual(real.IsImported, view.IsImported);

            VerifySameLocation(real.Location, view.Location);
            VerifySameLocation(real.ConditionLocation, view.ConditionLocation);

            Verify(view.Xml, real.Xml);

            VerifyLinkedNotNull(view.Project);
            VerifyNotLinkedNotNull(real.Project);
            if (context?.Pair != null)
            {
                Assert.AreSame(context?.Pair.View, view.Project);
                Assert.AreSame(context?.Pair.Real, real.Project);
            }

            Verify(view.Predecessor, real.Predecessor, context);
        }

        // public static void Verify(ProjectItemDefinition view, ProjectItemDefinition real) => Verify(view, real, null);
        public static void Verify(ProjectItemDefinition view, ProjectItemDefinition real, ValidationContext context)
        {
            if (view == null && real == null)
            {
                return;
            }

            VerifyLinkedNotNull(view);
            VerifyNotLinkedNotNull(real);

            // note ItemDefinition does not have a XML element
            // this is since it is [or can be] a aggregation of multiple ProjectItemDefinitionElement's.
            // This is somewhat of deficiency of MSBuild API.
            // (for example SetMetadata will always create a new ItemDefinitionElement because of that, for new metadata).

            Assert.AreEqual(real.ItemType, view.ItemType);
            Assert.AreEqual(real.MetadataCount, view.MetadataCount);

            Verify(view.Metadata, real.Metadata, Verify, context);

            foreach (var rm in real.Metadata)
            {
                var rv = real.GetMetadataValue(rm.Name);
                var vv = view.GetMetadataValue(rm.Name);
                Assert.AreEqual(rv, vv);

                var grm = real.GetMetadata(rm.Name);
                var gvm = view.GetMetadata(rm.Name);

                Verify(gvm, grm, context);
            }

            VerifyLinkedNotNull(view.Project);
            VerifyNotLinkedNotNull(real.Project);
            if (context?.Pair != null)
            {
                Assert.AreSame(context?.Pair.View, view.Project);
                Assert.AreSame(context?.Pair.Real, real.Project);
            }
        }

        // public static void Verify(ProjectItem view, ProjectItem real) => Verify(view, real, null);
        public static void Verify(ProjectItem view, ProjectItem real, ValidationContext context = null)
        {
            if (view == null && real == null)
            {
                return;
            }

            VerifyLinkedNotNull(view);
            VerifyNotLinkedNotNull(real);

            Verify(view.Xml, real.Xml);

            Assert.AreEqual(real.ItemType, view.ItemType);
            Assert.AreEqual(real.UnevaluatedInclude, view.UnevaluatedInclude);
            Assert.AreEqual(real.EvaluatedInclude, view.EvaluatedInclude);
            Assert.AreEqual(real.IsImported, view.IsImported);

            Assert.AreEqual(real.DirectMetadataCount, view.DirectMetadataCount);
            Verify(view.DirectMetadata, real.DirectMetadata, Verify, context);

            Assert.AreEqual(real.MetadataCount, view.MetadataCount);
            Verify(view.Metadata, real.Metadata, Verify, context);

            foreach (var rm in real.Metadata)
            {
                var rv = real.GetMetadataValue(rm.Name);
                var vv = view.GetMetadataValue(rm.Name);
                Assert.AreEqual(rv, vv);

                var grm = real.GetMetadata(rm.Name);
                var gvm = view.GetMetadata(rm.Name);

                Verify(gvm, grm, context);

                Assert.AreEqual(real.HasMetadata(rm.Name), view.HasMetadata(rm.Name));
            }

            Assert.AreEqual(real.HasMetadata("random non existent"), view.HasMetadata("random non existent"));
            Assert.AreEqual(real.GetMetadataValue("random non existent"), view.GetMetadataValue("random non existent"));

            VerifyLinkedNotNull(view.Project);
            VerifyNotLinkedNotNull(real.Project);
            if (context?.Pair != null)
            {
                Assert.AreSame(context.Pair.View, view.Project);
                Assert.AreSame(context.Pair.Real, real.Project);
            }
        }


        private static void Verify(SdkReference view, SdkReference real, ValidationContext context = null)
        {
            if (view == null && real == null)
            {
                return;
            }

            Assert.IsNotNull(view);
            Assert.IsNotNull(real);

            Assert.AreEqual(real.Name, view.Name);
            Assert.AreEqual(real.Version, view.Version);
            Assert.AreEqual(real.MinimumVersion, view.MinimumVersion);
        }

        private static void Verify(SdkResult view, SdkResult real, ValidationContext context = null)
        {
            if (view == null && real == null)
            {
                return;
            }

            Assert.IsNotNull(view);
            Assert.IsNotNull(real);
            Assert.AreEqual(real.Success, view.Success);
            Assert.AreEqual(real.Path, view.Path);
            Verify(view.SdkReference, real.SdkReference, context);
        }

        private static void Verify(ResolvedImport view, ResolvedImport real, ValidationContext context = null)
        {
            Assert.AreEqual(real.IsImported, view.IsImported);
            Verify(view.ImportingElement, real.ImportingElement);
            Verify(view.ImportedProject, real.ImportedProject);
            Verify(view.SdkResult, real.SdkResult, context);
        }

        private static void Verify(List<string> viewProps, List<string> realProps, ValidationContext context = null)
        {
            if (viewProps == null && realProps == null)
            {
                return;
            }

            Assert.IsNotNull(viewProps);
            Assert.IsNotNull(realProps);
            Assert.AreEqual(realProps.Count, viewProps.Count);

            for (int i = 0; i < realProps.Count; i++)
            {
                Assert.AreEqual(realProps[i], viewProps[i]);
            }
        }

        public static void Verify(Project view, Project real, ValidationContext context = null)
        {
            if (view == null && real == null)
            {
                return;
            }

            var pair = new ProjectPair(view, real);
            Verify(pair, context);
        }

        public static void Verify(ProjectPair pair, ValidationContext context = null)
        {
            if (pair == null)
            {
                return;
            }

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
            Verify(view.GlobalProperties, real.GlobalProperties, (a, b, p) => Assert.AreEqual(b, a), context);
            Verify(view.Imports, real.Imports, Verify, context);
            Verify(view.ItemTypes, real.ItemTypes, (a, b, p) => Assert.AreEqual(b, a), context);

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

            Assert.AreNotSame(view.ProjectCollection, real.ProjectCollection);
            Assert.AreEqual(real.ToolsVersion, view.ToolsVersion);
            Assert.AreEqual(real.SubToolsetVersion, view.SubToolsetVersion);
            Assert.AreEqual(real.DirectoryPath, view.DirectoryPath);
            Assert.AreEqual(real.FullPath, view.FullPath);
            Assert.AreEqual(real.SkipEvaluation, view.SkipEvaluation);
            Assert.AreEqual(real.ThrowInsteadOfSplittingItemElement, view.ThrowInsteadOfSplittingItemElement);
            Assert.AreEqual(real.IsDirty, view.IsDirty);
            Assert.AreEqual(real.DisableMarkDirty, view.DisableMarkDirty);
            Assert.AreEqual(real.IsBuildEnabled, view.IsBuildEnabled);

            VerifySameLocation(real.ProjectFileLocation, view.ProjectFileLocation, context);

            // we currently dont support "Execution" remoting.
            // Verify(view.Targets, real.Targets, Verify, view, real);
            Assert.AreEqual(real.EvaluationCounter, view.EvaluationCounter);
            Assert.AreEqual(real.LastEvaluationId, view.LastEvaluationId);
        }
    }
}
