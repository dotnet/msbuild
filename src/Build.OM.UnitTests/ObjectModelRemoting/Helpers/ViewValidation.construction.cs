// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Build.Construction;
    using Microsoft.Build.ObjectModelRemoting;
    using Microsoft.Build.Evaluation;
    using Xunit;
    using System.Runtime.ExceptionServices;
    using System.Xml.Schema;
    using System.Collections;

    internal class ElementLinkPair<T> : LinkPair<T>
        where T : ProjectElement
    {
        public ProjectXmlPair PRE { get; protected set; }

        public ElementLinkPair(ProjectXmlPair pre, T view, T real) : base(view, real) { this.PRE = pre; }

        // the PRE.CreateXX(), AppendChild, way.
        public ElementLinkPair<CT> AppendNewChaildWithVerify<CT>(ObjectType where, string id, Func<ProjectRootElement, string, CT> adder, Func<CT, string, bool> matcher)
            where CT : ProjectElement
        {
            var appendWhere = this.Get(where) as ProjectElementContainer;
            Assert.NotNull(appendWhere);

            var c1Where = adder(this.PRE.Get(where), id);
            Assert.NotNull(c1Where);
            appendWhere.AppendChild(c1Where);

            var c1 = this.QuerySingleChildrenWithValidation<CT>((t) => matcher(t, id));
            Assert.Same(c1Where, c1.Get(where));

            return c1;
        }

        public ElementLinkPair<CT> AppendNewNamedChaildWithVerify<CT>(ObjectType where, string name, Func<ProjectRootElement, string, CT> adder)
            where CT : ProjectElement
            => AppendNewChaildWithVerify(where, name, adder, (c, n) => string.Equals(c.ElementName, n));

        public ElementLinkPair<CT> AppendNewLabeledChaildWithVerify<CT>(ObjectType where, string label, Func<ProjectRootElement, string, CT> adder)
            where CT : ProjectElement
            => AppendNewChaildWithVerify(where, label,
                (t, l) =>
                {
                    var ct = adder(t, l);
                    Assert.NotNull(ct);
                    ct.Label = l;
                    return ct;
                },
                (c, l) => string.Equals(c.Label, l));

        // if the element has a dedicated "addX" way.
        public ElementLinkPair<CT> AddNewChaildWithVerify<CT>(ObjectType where, string id, Func<T, string, CT> adder, Func<CT, string, bool> matcher)
            where CT : ProjectElement
        {
            var c1Where = adder(this.Get(where), id);
            Assert.NotNull(c1Where);

            var c1 = this.QuerySingleChildrenWithValidation<CT>((t) => matcher(t, id));
            Assert.Same(c1Where, c1.Get(where));

            return c1;
        }

        public ElementLinkPair<CT> AddNewNamedChaildWithVerify<CT>(ObjectType where, string name, Func<T, string, CT> adder)
            where CT : ProjectElement
            => AddNewChaildWithVerify(where, name, adder, (c, n) => string.Equals(c.ElementName, n));

        public ElementLinkPair<CT> AddNewLabeledChaildWithVerify<CT>(ObjectType where, string label, Func<T, string, CT> adder)
            where CT : ProjectElement
            => AddNewChaildWithVerify(where, label,
                (t, l)=>
                {
                    var ct = adder(t, l);
                    Assert.NotNull(ct);
                    ct.Label = l;
                    return ct;
                },
                (c, l) => string.Equals(c.Label, l));

        public void Append2NewChildrenWithVerify<CT>(string id, Func<ProjectRootElement, string, CT> adder, Func<CT, string, bool> matcher, out ElementLinkPair<CT> c1, out ElementLinkPair<CT> c2)
            where CT : ProjectElement
        {
            c1 = AppendNewChaildWithVerify(ObjectType.View, id.Ver(1), adder, matcher);
            c2 = AppendNewChaildWithVerify(ObjectType.Real, id.Ver(2), adder, matcher);
        }

        public void Append2NewNamedChildrenWithVerify<CT>(string name, Func<ProjectRootElement, string, CT> adder, out ElementLinkPair<CT> c1, out ElementLinkPair<CT> c2)
            where CT : ProjectElement
        {
            c1 = AppendNewNamedChaildWithVerify(ObjectType.View, name.Ver(1), adder);
            c2 = AppendNewNamedChaildWithVerify(ObjectType.Real, name.Ver(2), adder);
        }

        public void Append2NewLabeledChildrenWithVerify<CT>(string label, Func<ProjectRootElement, string, CT> adder, out ElementLinkPair<CT> c1, out ElementLinkPair<CT> c2)
            where CT : ProjectElement
        {
            c1 = AppendNewLabeledChaildWithVerify(ObjectType.View, label.Ver(1), adder);
            c2 = AppendNewLabeledChaildWithVerify(ObjectType.Real, label.Ver(2), adder);
        }

        public void Add2NewChildrenWithVerify<CT>(string id, Func<T, string, CT> adder, Func<CT, string, bool> matcher, out ElementLinkPair<CT> c1, out ElementLinkPair<CT> c2)
            where CT : ProjectElement
        {
            c1 = AddNewChaildWithVerify(ObjectType.View, id.Ver(1), adder, matcher);
            c2 = AddNewChaildWithVerify(ObjectType.Real, id.Ver(2), adder, matcher);
        }

        public void Add2NewNamedChildrenWithVerify<CT>(string name, Func<T, string, CT> adder, out ElementLinkPair<CT> c1, out ElementLinkPair<CT> c2)
            where CT : ProjectElement
        {
            c1 = AddNewNamedChaildWithVerify(ObjectType.View, name.Ver(1), adder);
            c2 = AddNewNamedChaildWithVerify(ObjectType.Real, name.Ver(2), adder);
        }

        public void Add2NewLabeledChildrenWithVerify<CT>(string label, Func<T, string, CT> adder, out ElementLinkPair<CT> c1, out ElementLinkPair<CT> c2)
            where CT : ProjectElement
        {
            c1 = AddNewLabeledChaildWithVerify(ObjectType.View, label.Ver(1), adder);
            c2 = AddNewLabeledChaildWithVerify(ObjectType.Real, label.Ver(2), adder);
        }


        public ICollection<ElementLinkPair<CT>> QueryChildrenWithValidation<CT>(Func<CT, bool> matcher, int expectedCount)
            where CT : ProjectElement
        {
            var result = QueryChildrenWithValidation(matcher);
            Assert.Equal(expectedCount, result.Count);
            return result;
        }

        public ICollection<ElementLinkPair<CT>> QueryChildrenWithValidation<CT>(Func<T, IEnumerable> getter, Func<CT, bool> matcher, int expectedCount)
            where CT : ProjectElement
        {
            var result = QueryChildrenWithValidation(getter, matcher);
            Assert.Equal(expectedCount, result.Count);
            return result;
        }

        public ICollection<ElementLinkPair<CT>> QueryChildrenWithValidation<CT>(Func<T,IEnumerable> getter,  Func<CT, bool> matcher)
            where CT : ProjectElement
        {
            var viewResult = new List<CT>();
            var realResult = new List<CT>();
            var finalResult = new List<ElementLinkPair<CT>>();

            foreach (var v in getter(this.View))
            {
                if (v is CT vt)
                {
                    if (matcher(vt))
                    {
                        viewResult.Add(vt);
                    }
                }
            }

            foreach (var r in getter(this.Real))
            {
                if (r is CT rt)
                {
                    if (matcher(rt))
                    {
                        realResult.Add(rt);
                    }
                }
            }
            // slow form view VerifyFindType, since we dont know the T.
            ViewValidation.Verify(viewResult, realResult);

            for (int i = 0; i < viewResult.Count; i++)
            {
                finalResult.Add(new ElementLinkPair<CT>(this.PRE, viewResult[i], realResult[i]));
            }

            return finalResult;
        }

        public ICollection<ElementLinkPair<CT>> QueryChildrenWithValidation<CT>(Func<CT, bool> matcher)
            where CT : ProjectElement
        {
            Assert.True(this.View is ProjectElementContainer);
            Assert.True(this.Real is ProjectElementContainer);

            return QueryChildrenWithValidation((t) => (t as ProjectElementContainer).AllChildren, matcher);
        }

        public ElementLinkPair<CT> QuerySingleChildrenWithValidation<CT>(Func<CT, bool> matcher)
            where CT : ProjectElement
        {
            var result = QueryChildrenWithValidation(matcher, 1);
            return result.FirstOrDefault();
        }

        public ElementLinkPair<CT> QuerySingleChildrenWithValidation<CT>(Func<T, IEnumerable> getter, Func<CT, bool> matcher)
            where CT : ProjectElement
        {
            var result = QueryChildrenWithValidation(getter, matcher, 1);
            return result.FirstOrDefault();
        }
    }

    internal class ProjectXmlPair : ElementLinkPair<ProjectRootElement>
    {
        ProjectPair Project { get; }
        public ProjectXmlPair(ProjectPair pair) : base(null, pair.View.Xml, pair.Real.Xml) { this.Project = pair; this.PRE = this; }
        public ProjectXmlPair(ProjectRootElement viewXml, ProjectRootElement realXml) : base(null, viewXml, realXml) { this.PRE = this; }

        public ElementLinkPair<CT> CreateWithVerify<CT>(Func<ProjectRootElement, CT> creator)
            where CT : ProjectElement
        {
            var view = creator(this.View);
            Assert.NotNull(view);
            var real = creator(this.Real);
            Assert.NotNull(real);
            ViewValidation.VerifyFindType(view, real);
            return new ElementLinkPair<CT>(this, view, real);
        }

        public ElementLinkPair<CT> CreateFromView<CT>(CT view)
            where CT : ProjectElement
            => CreateFromView(view, this);
        public static ElementLinkPair<CT> CreateFromView<CT>(CT view, ProjectXmlPair pre = null)
            where CT : ProjectElement
        {
            var real = ViewValidation.GetRealObject(view);
            return new ElementLinkPair<CT>(pre, view, real);
        }
    }


    internal static partial class ViewValidation
    {
        public static string Ver(this string str, int ver) => $"{str}_{ver}";
        public static string Ver(this string str, int ver, int subver) => $"{str}_{ver}_{subver}";

        public static void VerifySameLocationWithException(Func<ElementLocation> expectedGetter, Func<ElementLocation> actualGetter, ValidationContext context = null)
        {
            Assert.Equal(GetWithExceptionCheck(expectedGetter, out var expected), GetWithExceptionCheck(actualGetter, out var actual));
            VerifySameLocation(expected, actual, context);
        }

        public static void VerifySameLocation(ElementLocation expected, ElementLocation actual, ValidationContext context = null)
        {
            if (object.ReferenceEquals(expected, actual)) return;

            if (context?.ValidateLocation != null)
            {
                context.ValidateLocation(expected, actual);
                return;
            }

            Assert.NotNull(expected);
            Assert.NotNull(actual);

            Assert.Equal(expected.File, actual.File);
            Assert.Equal(expected.Line, actual.Line);
            Assert.Equal(expected.Column, actual.Column);
        }

        public static bool IsLinkedObject(object obj)
        {
            return LinkedObjectsFactory.GetLink(obj) != null;
        }

        private static bool dbgIgnoreLinked = false; 
        public static void VerifyNotLinked(object obj)
        {
            if (dbgIgnoreLinked) return;
            Assert.True(obj == null || !IsLinkedObject(obj));
        }

        // note this is a cheat, it relies on Mock implementation knowledge and that we are in the same process.
        public static T GetRealObject<T>(T view)
            where T : class
        {
            if (view == null) return null;
            if (!IsLinkedObject(view)) return view;

            var link = LinkedObjectsFactory.GetLink(view) as ILinkMock;
            Assert.NotNull(link);
            var remoter = link.Remoter as IRemoterSource;
            Assert.NotNull(remoter);
            var real = remoter.RealObject as T;
            Assert.NotNull(real);
            VerifyFindType(view, real);
            return real;
        }

        public static void VerifyLinked(object obj)
        {
            if (dbgIgnoreLinked) return;
            Assert.True(obj == null || IsLinkedObject(obj));
        }


        public static void VerifyNotNull(object obj, bool linked)
        {
            Assert.NotNull(obj);
            if (dbgIgnoreLinked) return;
            Assert.Equal(linked, IsLinkedObject(obj));
        }

        public static void VerifyNotLinkedNotNull(object obj)
        {
            Assert.NotNull(obj);
            if (dbgIgnoreLinked) return;
            Assert.True(!IsLinkedObject(obj));
        }

        public static void VerifyLinkedNotNull(object obj)
        {
            Assert.NotNull(obj);
            if (dbgIgnoreLinked) return;
            Assert.True(IsLinkedObject(obj));
        }

        public static bool GetWithExceptionCheck<T>(Func<T> getter, out T result)
        {
            try
            {
                result = getter();
                return true;
            }
            catch
            {
                result = default(T);
                return false;
            }
        }

        public static void ValidateEqualWithException<T>(Func<T> viewGetter, Func<T> realGetter, ValidationContext context = null)
        {
            bool viewOk = GetWithExceptionCheck(viewGetter, out T viewValue);
            bool realOk = GetWithExceptionCheck(realGetter, out T realValue);
            Assert.Equal(realOk, viewOk);
            Assert.Equal(realValue, viewValue);
        }


        private static void VerifyProjectElementViewInternal(ProjectElement viewXml, ProjectElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;

            VerifyLinkedNotNull(viewXml);
            VerifyNotLinkedNotNull(realXml);

            Assert.Equal(realXml.OuterElement, viewXml.OuterElement);

            VerifySameLocation(realXml.LabelLocation, viewXml.LabelLocation, context);

            VerifySameLocationWithException(()=>realXml.ConditionLocation, ()=>viewXml.ConditionLocation, context);

            VerifyNotLinked(realXml.ContainingProject);
            VerifyLinked(viewXml.ContainingProject);

            VerifyNotLinked(realXml.NextSibling);
            VerifyLinked(viewXml.NextSibling);

            VerifyNotLinked(realXml.PreviousSibling);
            VerifyLinked(viewXml.PreviousSibling);

            // skip AllParents, parent validation should cover it.
            VerifySameLocation(realXml.Location, viewXml.Location, context);

            Assert.Equal(realXml.ElementName, viewXml.ElementName);
            Assert.Equal(realXml.Label, viewXml.Label);

            ValidateEqualWithException(() => viewXml.Condition, () => realXml.Condition);

            VerifyNotLinked(realXml.Parent);
            VerifyLinked(viewXml.Parent);
        }


        private static void VerifyProjectElementContainerView(ProjectElementContainer viewXml, ProjectElementContainer realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;
            VerifyProjectElementViewInternal(viewXml, realXml, context);

            Assert.Equal(realXml.Count, viewXml.Count);

            VerifyNotLinked(realXml.FirstChild);
            VerifyLinked(viewXml.FirstChild);

            VerifyNotLinked(realXml.LastChild);
            VerifyLinked(viewXml.LastChild);

            var realChild = realXml.FirstChild;
            var viewChild = viewXml.FirstChild;

            while (realChild != null )
            {
                Assert.NotNull(viewChild);
                Assert.Same(realChild.Parent, realXml);

                if (!object.ReferenceEquals(viewChild.Parent, viewXml))
                {
                    var lm = LinkedObjectsFactory.GetLink(viewXml) as ILinkMock;
                    lm.Linker.ValidateNoDuplicates();
                }

                Assert.Same(viewChild.Parent, viewXml);

                if (realChild is ProjectElementContainer realChildContainer)
                {
                    Assert.True(viewChild is ProjectElementContainer);

                    VerifyProjectElementContainerView((ProjectElementContainer)viewChild, realChildContainer, context);
                }
                else
                {
                    Assert.False(viewChild is ProjectElementContainer);
                    VerifyProjectElementViewInternal(viewChild, realChild, context);
                }

                realChild = realChild.NextSibling;
                viewChild = viewChild.NextSibling;
            }

            Assert.Null(viewChild);
        }

        public static void VerifyProjectCollectionLinks(this ProjectCollectionLinker linker, string path, int expectedLocal, int expectedLinks)
            => VerifyProjectCollectionLinks(linker.Collection, path, expectedLocal, expectedLinks);

        public static void VerifyProjectCollectionLinks(this ProjectCollection collection, string path, int expectedLocal, int expectedLinks)
            => VerifyProjectCollectionLinks(collection.GetLoadedProjects(path), expectedLocal, expectedLinks);

        public static void VerifyProjectCollectionLinks(IEnumerable<Project> projects, int expectedLocal, int expectedLinks)
        {
            HashSet<Project> remotes = new HashSet<Project>();
            int actualLocal = 0;
            int actualLinks = 0;
            foreach (var prj in projects)
            {
                Assert.NotNull(prj);
                if (IsLinkedObject(prj))
                {
                    Assert.DoesNotContain(prj, remotes);
                    actualLinks++;
                    remotes.Add(prj);
                }
                else
                {
                    actualLocal++;
                }
            }

            Assert.Equal(expectedLocal, actualLocal);
            Assert.Equal(expectedLinks, actualLinks);
        }


        public static void VerifyProjectElement(ProjectElement viewXml, ProjectElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;

            if (viewXml is ProjectElementContainer viewContainer)
            {
                Assert.True(realXml is ProjectElementContainer);
                VerifyProjectElementContainerView(viewContainer, (ProjectElementContainer)realXml, context);
            }
            else
            {
                Assert.False(realXml is ProjectElementContainer);
                VerifyProjectElementViewInternal(viewXml, realXml, context);
            }
        }

        public static void Verify(ProjectRootElement viewXml, ProjectRootElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;
            VerifyProjectElement(viewXml, realXml, context);

            Assert.Equal(realXml.FullPath, viewXml.FullPath);
            Assert.Equal(realXml.DirectoryPath, viewXml.DirectoryPath);
            Assert.Equal(realXml.Encoding, viewXml.Encoding);
            Assert.Equal(realXml.DefaultTargets, viewXml.DefaultTargets);
            Assert.Equal(realXml.InitialTargets, viewXml.InitialTargets);
            Assert.Equal(realXml.Sdk, viewXml.Sdk);
            Assert.Equal(realXml.TreatAsLocalProperty, viewXml.TreatAsLocalProperty);
            Assert.Equal(realXml.ToolsVersion, viewXml.ToolsVersion);
            Assert.Equal(realXml.HasUnsavedChanges, viewXml.HasUnsavedChanges);
            Assert.Equal(realXml.PreserveFormatting, viewXml.PreserveFormatting);
            Assert.Equal(realXml.Version, viewXml.Version);
            Assert.Equal(realXml.TimeLastChanged, viewXml.TimeLastChanged);
            Assert.Equal(realXml.LastWriteTimeWhenRead, viewXml.LastWriteTimeWhenRead);

            ViewValidation.VerifySameLocation(realXml.ProjectFileLocation, viewXml.ProjectFileLocation, context);
            ViewValidation.VerifySameLocation(realXml.ToolsVersionLocation, viewXml.ToolsVersionLocation, context);
            ViewValidation.VerifySameLocation(realXml.DefaultTargetsLocation, viewXml.DefaultTargetsLocation, context);
            ViewValidation.VerifySameLocation(realXml.InitialTargetsLocation, viewXml.InitialTargetsLocation, context);
            ViewValidation.VerifySameLocation(realXml.SdkLocation, viewXml.SdkLocation, context);
            ViewValidation.VerifySameLocation(realXml.TreatAsLocalPropertyLocation, viewXml.TreatAsLocalPropertyLocation, context);

            ViewValidation.Verify(viewXml.ChooseElements, realXml.ChooseElements, Verify, context);
            ViewValidation.Verify(viewXml.ItemDefinitionGroups, realXml.ItemDefinitionGroups, Verify, context);
            ViewValidation.Verify(viewXml.ItemDefinitions, realXml.ItemDefinitions, Verify, context);
            ViewValidation.Verify(viewXml.ItemGroups, realXml.ItemGroups, Verify, context);
            ViewValidation.Verify(viewXml.Items, realXml.Items, Verify, context);
            ViewValidation.Verify(viewXml.ImportGroups, realXml.ImportGroups, Verify, context);
            ViewValidation.Verify(viewXml.Imports, realXml.Imports, Verify, context);
            ViewValidation.Verify(viewXml.PropertyGroups, realXml.PropertyGroups, Verify, context);
            ViewValidation.Verify(viewXml.Properties, realXml.Properties, Verify, context);
            ViewValidation.Verify(viewXml.Targets, realXml.Targets, Verify, context);
            ViewValidation.Verify(viewXml.UsingTasks, realXml.UsingTasks, Verify, context);
            ViewValidation.Verify(viewXml.ItemGroupsReversed, realXml.ItemGroupsReversed, Verify, context);
            ViewValidation.Verify(viewXml.ItemDefinitionGroupsReversed, realXml.ItemDefinitionGroupsReversed, Verify, context);
            ViewValidation.Verify(viewXml.ImportGroupsReversed, realXml.ImportGroupsReversed, Verify, context);
            ViewValidation.Verify(viewXml.PropertyGroupsReversed, realXml.PropertyGroupsReversed, Verify, context);
        }

        public static void Verify(ProjectChooseElement viewXml, ProjectChooseElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;
            VerifyProjectElement(viewXml, realXml, context);


            Verify(viewXml.WhenElements, realXml.WhenElements, Verify, context);
            Verify(viewXml.OtherwiseElement, realXml.OtherwiseElement, context);
        }

        public static void Verify(ProjectWhenElement viewXml, ProjectWhenElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;
            VerifyProjectElement(viewXml, realXml, context);


            Verify(viewXml.ChooseElements, realXml.ChooseElements, context);
            Verify(viewXml.ItemGroups, realXml.ItemGroups, context);
            Verify(viewXml.PropertyGroups, realXml.PropertyGroups, context);
        }

        public static void Verify(ProjectOtherwiseElement viewXml, ProjectOtherwiseElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;
            VerifyProjectElement(viewXml, realXml, context);


            Verify(viewXml.ChooseElements, realXml.ChooseElements, context);
            Verify(viewXml.ItemGroups, realXml.ItemGroups, context);
            Verify(viewXml.PropertyGroups, realXml.PropertyGroups, context);
        }

        public static void Verify(ProjectExtensionsElement viewXml, ProjectExtensionsElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;
            VerifyProjectElement(viewXml, realXml, context);

            Assert.Equal(realXml.Content, viewXml.Content);
        }

        public static void Verify(ProjectMetadataElement viewXml, ProjectMetadataElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;
            VerifyProjectElement(viewXml, realXml, context);

            Assert.Equal(realXml.Name, viewXml.Name);
            Assert.Equal(realXml.Value, viewXml.Value);
            Assert.Equal(realXml.ExpressedAsAttribute, viewXml.ExpressedAsAttribute);
        }

        public static void Verify(ProjectTaskElement viewXml, ProjectTaskElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;
            VerifyProjectElement(viewXml, realXml, context);

            Assert.Equal(realXml.Name, viewXml.Name);

            Assert.Equal(realXml.ContinueOnError, viewXml.ContinueOnError);
            ViewValidation.VerifySameLocation(realXml.ContinueOnErrorLocation, viewXml.ContinueOnErrorLocation, context);
            Assert.Equal(realXml.MSBuildRuntime, viewXml.MSBuildRuntime);
            ViewValidation.VerifySameLocation(realXml.MSBuildRuntimeLocation, viewXml.MSBuildRuntimeLocation, context);

            Assert.Equal(realXml.MSBuildArchitecture, viewXml.MSBuildArchitecture);
            ViewValidation.VerifySameLocation(realXml.MSBuildArchitectureLocation, viewXml.MSBuildArchitectureLocation, context);

            ViewValidation.Verify(viewXml.Outputs, realXml.Outputs, ViewValidation.Verify, context);

            var realParams = realXml.Parameters;
            var viewParams = viewXml.Parameters;
            if (realParams == null)
            {
                Assert.Null(viewParams);
            }
            else
            {
                Assert.NotNull(viewParams);

                Assert.Equal(realParams.Count, viewParams.Count);
                foreach (var k in realParams.Keys)
                {
                    Assert.True(viewParams.ContainsKey(k));
                    Assert.Equal(realParams[k], viewParams[k]);
                }
            }

            var realParamsLoc = realXml.ParameterLocations;
            var viewParamsLoc = viewXml.ParameterLocations;
            if (realParamsLoc == null)
            {
                Assert.Null(viewParamsLoc);
            }
            else
            {
                Assert.NotNull(viewParamsLoc);

                var realPLocList = realParamsLoc.ToList();
                var viewPLocList = viewParamsLoc.ToList();

                Assert.Equal(realPLocList.Count, viewPLocList.Count);
                for (int li = 0; li < realPLocList.Count; li++)
                {
                    var rkvp = realPLocList[li];
                    var vkvp = viewPLocList[li];

                    Assert.Equal(rkvp.Key, vkvp.Key);
                    ViewValidation.VerifySameLocation(rkvp.Value, vkvp.Value, context);
                }
            }
        }

        public static void Verify(ProjectOutputElement viewXml, ProjectOutputElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;
            VerifyProjectElement(viewXml, realXml, context);

            Assert.Equal(realXml.TaskParameter, viewXml.TaskParameter);
            VerifySameLocation(realXml.TaskParameterLocation, viewXml.TaskParameterLocation, context);
            Assert.Equal(realXml.IsOutputItem, viewXml.IsOutputItem);
            Assert.Equal(realXml.IsOutputProperty, viewXml.IsOutputProperty);
            Assert.Equal(realXml.ItemType, viewXml.ItemType);
            Assert.Equal(realXml.PropertyName, viewXml.PropertyName);
            VerifySameLocation(realXml.PropertyNameLocation, viewXml.PropertyNameLocation, context);
        }

        public static void Verify(ProjectUsingTaskBodyElement viewXml, ProjectUsingTaskBodyElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;

            VerifyProjectElement(viewXml, realXml, context);

            Assert.Equal(realXml.TaskBody, viewXml.TaskBody);
            Assert.Equal(realXml.Evaluate, viewXml.Evaluate);
            VerifySameLocation(realXml.EvaluateLocation, viewXml.EvaluateLocation, context);
        }

        public static void Verify(ProjectUsingTaskParameterElement viewXml, ProjectUsingTaskParameterElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;

            VerifyProjectElement(viewXml, realXml, context);

            Assert.Equal(realXml.Name, viewXml.Name);
            Assert.Equal(realXml.ParameterType, viewXml.ParameterType);
            VerifySameLocation(realXml.ParameterTypeLocation, viewXml.ParameterTypeLocation, context);
            Assert.Equal(realXml.Output, viewXml.Output);
            VerifySameLocation(realXml.OutputLocation, viewXml.OutputLocation, context);
            Assert.Equal(realXml.Required, viewXml.Required);
            VerifySameLocation(realXml.RequiredLocation, viewXml.RequiredLocation, context);
        }

        public static void Verify(UsingTaskParameterGroupElement viewXml, UsingTaskParameterGroupElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;

            VerifyProjectElement(viewXml, realXml, context);

            Verify(viewXml.Parameters, realXml.Parameters, Verify, context);
        }

        public static void Verify(ProjectUsingTaskElement viewXml, ProjectUsingTaskElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;
            VerifyProjectElement(viewXml, realXml, context);


            Assert.Equal(realXml.AssemblyFile, viewXml.AssemblyFile);
            ViewValidation.VerifySameLocation(realXml.AssemblyFileLocation, viewXml.AssemblyFileLocation, context);

            Assert.Equal(realXml.AssemblyName, viewXml.AssemblyName);
            ViewValidation.VerifySameLocation(realXml.AssemblyNameLocation, viewXml.AssemblyNameLocation, context);

            Assert.Equal(realXml.TaskName, viewXml.TaskName);
            ViewValidation.VerifySameLocation(realXml.TaskNameLocation, viewXml.TaskNameLocation, context);

            Assert.Equal(realXml.TaskFactory, viewXml.TaskFactory);
            ViewValidation.VerifySameLocation(realXml.TaskFactoryLocation, viewXml.TaskFactoryLocation, context);

            Assert.Equal(realXml.Runtime, viewXml.Runtime);
            ViewValidation.VerifySameLocation(realXml.RuntimeLocation, viewXml.RuntimeLocation, context);

            Assert.Equal(realXml.Architecture, viewXml.Architecture);
            ViewValidation.VerifySameLocation(realXml.ArchitectureLocation, viewXml.ArchitectureLocation, context);

            ViewValidation.Verify(viewXml.TaskBody, realXml.TaskBody, context);
            ViewValidation.Verify(viewXml.ParameterGroup, realXml.ParameterGroup, context);
        }

        public static void Verify(ProjectTargetElement viewXml, ProjectTargetElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;
            VerifyProjectElement(viewXml, realXml, context);


            Assert.Equal(realXml.Name, viewXml.Name);
            ViewValidation.VerifySameLocation(realXml.NameLocation, viewXml.NameLocation, context);
            Assert.Equal(realXml.Inputs, viewXml.Inputs);
            ViewValidation.VerifySameLocation(realXml.InputsLocation, viewXml.InputsLocation, context);
            Assert.Equal(realXml.Outputs, viewXml.Outputs);
            ViewValidation.VerifySameLocation(realXml.OutputsLocation, viewXml.OutputsLocation, context);
            Assert.Equal(realXml.KeepDuplicateOutputs, viewXml.KeepDuplicateOutputs);
            ViewValidation.VerifySameLocation(realXml.KeepDuplicateOutputsLocation, viewXml.KeepDuplicateOutputsLocation, context);
            Assert.Equal(realXml.DependsOnTargets, viewXml.DependsOnTargets);
            ViewValidation.VerifySameLocation(realXml.DependsOnTargetsLocation, viewXml.DependsOnTargetsLocation, context);
            Assert.Equal(realXml.BeforeTargets, viewXml.BeforeTargets);
            ViewValidation.VerifySameLocation(realXml.BeforeTargetsLocation, viewXml.BeforeTargetsLocation, context);
            Assert.Equal(realXml.AfterTargets, viewXml.AfterTargets);
            ViewValidation.VerifySameLocation(realXml.AfterTargetsLocation, viewXml.AfterTargetsLocation, context);
            Assert.Equal(realXml.Returns, viewXml.Returns);
            ViewValidation.VerifySameLocation(realXml.ReturnsLocation, viewXml.ReturnsLocation, context);

            ViewValidation.Verify(viewXml.ItemGroups, realXml.ItemGroups, ViewValidation.Verify, context);
            ViewValidation.Verify(viewXml.PropertyGroups, realXml.PropertyGroups, ViewValidation.Verify, context);
            ViewValidation.Verify(viewXml.OnErrors, realXml.OnErrors, ViewValidation.Verify, context);
            ViewValidation.Verify(viewXml.Tasks, realXml.Tasks, ViewValidation.Verify, context);
        }

        public static void Verify(ProjectImportElement viewXml, ProjectImportElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;
            VerifyProjectElement(viewXml, realXml, context);


            Assert.Equal(realXml.Project, viewXml.Project);
            ViewValidation.VerifySameLocation(realXml.ProjectLocation, viewXml.ProjectLocation, context);

            // mostly test the remoting infrastructure. Sdk Imports are not really covered by simple samples for now.
            // Todo: add mock SDK import closure to SdtGroup?
            Assert.Equal(realXml.Sdk, viewXml.Sdk);
            Assert.Equal(realXml.Version, viewXml.Version);
            Assert.Equal(realXml.MinimumVersion, viewXml.MinimumVersion);
            ViewValidation.VerifySameLocation(realXml.SdkLocation, viewXml.SdkLocation, context);
            Assert.Equal(realXml.ImplicitImportLocation, viewXml.ImplicitImportLocation);
            ViewValidation.VerifyProjectElement(viewXml.OriginalElement, realXml.OriginalElement, context);
        }

        public static void Verify(ProjectImportGroupElement viewXml, ProjectImportGroupElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;
            VerifyProjectElement(viewXml, realXml, context);

            ViewValidation.Verify(viewXml.Imports, realXml.Imports, ViewValidation.Verify, context);
        }

        public static void Verify(ProjectItemDefinitionElement viewXml, ProjectItemDefinitionElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;
            VerifyProjectElement(viewXml, realXml, context);

            Assert.Equal(realXml.ItemType, viewXml.ItemType);
            ViewValidation.Verify(viewXml.Metadata, realXml.Metadata, ViewValidation.Verify, context);
        }

        public static void Verify(ProjectItemDefinitionGroupElement viewXml, ProjectItemDefinitionGroupElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;
            VerifyProjectElement(viewXml, realXml, context);

            ViewValidation.Verify(viewXml.ItemDefinitions, realXml.ItemDefinitions, ViewValidation.Verify, context);
        }

        public static void Verify(ProjectItemElement viewXml, ProjectItemElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;
            VerifyProjectElement(viewXml, realXml, context);

            Assert.Equal(realXml.ItemType, viewXml.ItemType);
            Assert.Equal(realXml.Include, viewXml.Include);
            Assert.Equal(realXml.Exclude, viewXml.Exclude);
            Assert.Equal(realXml.Remove, viewXml.Remove);
            Assert.Equal(realXml.Update, viewXml.Update);
            Assert.Equal(realXml.KeepMetadata, viewXml.KeepMetadata);
            Assert.Equal(realXml.RemoveMetadata, viewXml.RemoveMetadata);
            Assert.Equal(realXml.KeepDuplicates, viewXml.KeepDuplicates);
            Assert.Equal(realXml.HasMetadata, viewXml.HasMetadata);

           Verify(viewXml.Metadata, realXml.Metadata, ViewValidation.Verify, context);

           VerifySameLocation(realXml.IncludeLocation, viewXml.IncludeLocation, context);
           VerifySameLocation(realXml.ExcludeLocation, viewXml.ExcludeLocation, context);
           VerifySameLocation(realXml.RemoveLocation, viewXml.RemoveLocation, context);
           VerifySameLocation(realXml.UpdateLocation, viewXml.UpdateLocation, context);
           VerifySameLocation(realXml.KeepMetadataLocation, viewXml.KeepMetadataLocation, context);
           VerifySameLocation(realXml.RemoveMetadataLocation, viewXml.RemoveMetadataLocation, context);
           VerifySameLocation(realXml.KeepDuplicatesLocation, viewXml.KeepDuplicatesLocation, context);
        }

        public static void Verify(ProjectItemGroupElement viewXml, ProjectItemGroupElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;
            VerifyProjectElement(viewXml, realXml, context);

            Verify(viewXml.Items, realXml.Items, Verify, context);
        }

        public static void Verify(ProjectPropertyElement viewXml, ProjectPropertyElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;
            VerifyProjectElement(viewXml, realXml, context);

            Assert.Equal(realXml.Name, viewXml.Name);
            Assert.Equal(realXml.Value, viewXml.Value);
        }

        public static void Verify(ProjectPropertyGroupElement viewXml, ProjectPropertyGroupElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;
            VerifyProjectElement(viewXml, realXml, context);
            Verify(viewXml.Properties, realXml.Properties, Verify, context);
            Verify(viewXml.PropertiesReversed, realXml.PropertiesReversed, Verify, context);
        }

        public static void Verify(ProjectSdkElement viewXml, ProjectSdkElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;
            VerifyProjectElement(viewXml, realXml, context);
            Assert.Equal(realXml.Name, viewXml.Name);
            Assert.Equal(realXml.Version, viewXml.Version);
            Assert.Equal(realXml.MinimumVersion, viewXml.MinimumVersion);
        }

        public static void Verify(ProjectOnErrorElement viewXml, ProjectOnErrorElement realXml, ValidationContext context = null)
        {
            if (viewXml == null && realXml == null) return;
            VerifyProjectElement(viewXml, realXml, context);

            Assert.Equal(realXml.ExecuteTargetsAttribute, viewXml.ExecuteTargetsAttribute);
            VerifySameLocation(realXml.ExecuteTargetsLocation, viewXml.ExecuteTargetsLocation);
        }
    }
}
