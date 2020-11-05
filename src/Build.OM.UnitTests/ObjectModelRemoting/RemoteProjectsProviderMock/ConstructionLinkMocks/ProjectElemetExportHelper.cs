// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Build.Construction;


    /// <summary>
    /// We need to know the actual type of ProjectElements in order to do a proper remoting.
    /// Unless we do some explicit ProjectElement.GetXMLType() thing we need to use heuristic.
    ///
    /// Most of the types has a single implementation, but few has a wrapper classes. They are also internal for MSbuild.
    /// </summary>
    internal static class ProjectElemetExportHelper
    {
        delegate MockProjectElementLinkRemoter ExporterFactory(ProjectCollectionLinker exporter, ProjectElement xml);
        private class ElementInfo
        {
            public static ElementInfo New<T, RMock>()
                where RMock : MockProjectElementLinkRemoter, new()
            {
                return new ElementInfo(typeof(T), IsOfType<T>, Export<RMock>);
            }

            public ElementInfo(Type type, Func<ProjectElement, bool> checker, ExporterFactory factory)
            {
                this.CanonicalType = type;
                this.Checker = checker;
                this.ExportFactory = factory;
            }


            public ElementInfo(Func<ProjectElement, bool> checker, ExporterFactory factory)
            {
                this.Checker = checker;
                this.ExportFactory = factory;
            }

            public Type CanonicalType { get; }
            public Func<ProjectElement, bool> Checker { get; }
            public ExporterFactory ExportFactory { get; }
        }

        private static List<ElementInfo> canonicalTypes = new List<ElementInfo>()
        {
            ElementInfo.New<ProjectRootElement               , MockProjectRootElementLinkRemoter>(),
            ElementInfo.New<ProjectChooseElement             , MockProjectChooseElementLinkRemoter>(),
            ElementInfo.New<ProjectExtensionsElement         , MockProjectExtensionsElementLinkRemoter>(),
            ElementInfo.New<ProjectImportElement             , MockProjectImportElementLinkRemoter>(),
            ElementInfo.New<ProjectImportGroupElement        , MockProjectImportGroupElementLinkRemoter>(),
            ElementInfo.New<ProjectItemDefinitionGroupElement, MockProjectItemDefinitionGroupElementLinkRemoter>(),
            ElementInfo.New<ProjectItemElement               , MockProjectItemElementLinkRemoter>(),
            ElementInfo.New<ProjectItemGroupElement          , MockProjectItemGroupElementLinkRemoter>(),
            ElementInfo.New<ProjectMetadataElement           , MockProjectMetadataElementLinkRemoter>(),
            ElementInfo.New<ProjectOnErrorElement            , MockProjectOnErrorElementLinkRemoter>(),
            ElementInfo.New<ProjectOtherwiseElement          , MockProjectOtherwiseElementLinkRemoter>(),
            ElementInfo.New<ProjectOutputElement             , MockProjectOutputElementLinkRemoter>(),
            ElementInfo.New<ProjectPropertyElement           , MockProjectPropertyElementLinkRemoter>(),
            ElementInfo.New<ProjectPropertyGroupElement      , MockProjectPropertyGroupElementLinkRemoter>(),
            ElementInfo.New<ProjectSdkElement                , MockProjectSdkElementLinkRemoter>(),
            ElementInfo.New<ProjectTargetElement             , MockProjectTargetElementLinkRemoter>(),
            ElementInfo.New<ProjectTaskElement               , MockProjectTaskElementLinkRemoter>(),
            ElementInfo.New<ProjectUsingTaskBodyElement      , MockProjectUsingTaskBodyElementLinkRemoter>(),
            ElementInfo.New<ProjectItemDefinitionElement     , MockProjectItemDefinitionElementLinkRemoter>(),
            ElementInfo.New<ProjectUsingTaskElement          , MockProjectUsingTaskElementLinkRemoter>(),
            ElementInfo.New<ProjectUsingTaskParameterElement , MockProjectUsingTaskParameterElementLinkRemoter>(),
            ElementInfo.New<ProjectWhenElement               , MockProjectWhenElementLinkRemoter>(),
            ElementInfo.New<UsingTaskParameterGroupElement   , MockUsingTaskParameterGroupElementLinkRemoter>(),
        };

        private static MockProjectElementLinkRemoter Export<RMock>(ProjectCollectionLinker exporter, ProjectElement xml)
            where RMock : MockProjectElementLinkRemoter, new()
        {
            return exporter.Export<ProjectElement, RMock>(xml);
        }

        private static bool IsOfType<T> (ProjectElement xml) { return xml is T; }

        private static Dictionary<Type, ExporterFactory> knownTypes = new Dictionary<Type, ExporterFactory>();

        static ProjectElemetExportHelper()
        {
            foreach (var v in canonicalTypes)
            {
                knownTypes.Add(v.CanonicalType, v.ExportFactory);
            }
        }


        private static MockProjectElementLinkRemoter NotImplemented(ProjectCollectionLinker exporter, ProjectElement xml)
        {
            throw new NotImplementedException();
        }

        public static MockProjectElementLinkRemoter ExportElement(this ProjectCollectionLinker exporter, ProjectElement xml)
        {
            if (xml == null)
            {
                return null;
            }

            var implType = xml.GetType();
            if (knownTypes.TryGetValue(implType, out var factory))
            {
                return factory(exporter, xml);
            }

            factory = NotImplemented;

            foreach (var t in canonicalTypes)
            {
                if (t.Checker(xml))
                {
                    factory = t.ExportFactory;
                    break;
                }
            }

            lock (knownTypes)
            {
                var newKnown = new Dictionary<Type, ExporterFactory>(knownTypes);
                newKnown[implType] = factory;
                knownTypes = newKnown;
            }

            return factory(exporter, xml);
        }
    }
}
