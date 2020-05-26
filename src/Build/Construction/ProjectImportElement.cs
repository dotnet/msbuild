// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Diagnostics;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction
{
    /// <summary>
    ///     Initializes a ProjectImportElement instance.
    /// </summary>
    [DebuggerDisplay("Project={Project} Condition={Condition}")]
    public class ProjectImportElement : ProjectElement, ISdkReferenceMutableSource
    {
        private static readonly SdkReferenceAttribute NameAttributeFactory =
            new SdkReferenceAttribute(
                XMakeAttributes.sdk, "Set Import Sdk {0}"
            );

        private static readonly SdkReferenceAttribute VersionAttributeFactory =
            new SdkReferenceAttribute(
                XMakeAttributes.sdkVersion, "Set Import Version {0}"
            );

        private static readonly SdkReferenceAttribute MinimumVersionAttributeFactory =
            new SdkReferenceAttribute(
                XMakeAttributes.sdkMinimumVersion, "Set Import Minimum Version {0}"
            );

        internal ProjectImportElementLink? ImportLink => (ProjectImportElementLink) Link;

        private ImplicitImportLocation _implicitImportLocation = ImplicitImportLocation.None;
        private ProjectElement? _originalElement;
        private readonly ISdkReferenceSource _sdkReferenceSource;

        /// <summary>
        ///     External projects support
        /// </summary>
        internal ProjectImportElement(ProjectImportElementLink link)
            : base(link) =>
            _sdkReferenceSource = this;

        /// <summary>
        ///     Initialize a parented ProjectImportElement
        /// </summary>
        internal ProjectImportElement(XmlElementWithLocation xmlElement, ProjectElementContainer parent,
                                      ProjectRootElement containingProject)
            : this(xmlElement, parent, containingProject, null)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, nameof(parent));
        }

        /// <summary>
        ///     Initialize an unparented ProjectImportElement
        /// </summary>
        internal ProjectImportElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
            : this(xmlElement, null, containingProject, null)
        {
        }

        private ProjectImportElement(XmlElement xmlElement, ProjectElementContainer? parent,
                                     ProjectRootElement containingProject, ISdkReferenceSource? referenceSource)
            : base(xmlElement, parent, containingProject)
        {
            _sdkReferenceSource = referenceSource ?? this;

            if (referenceSource is SdkReferenceConstantSource source)
            {
                SetValue(this, NameAttributeFactory, source.SdkReference.Reference.Name);
                SetValue(this, VersionAttributeFactory, source.SdkReference.Reference.Version);
                SetValue(this, MinimumVersionAttributeFactory, source.SdkReference.Reference.MinimumVersion);
            }
        }

#nullable restore

        /// <summary>
        ///     Gets or sets the Project value.
        /// </summary>
        public string Project
        {
            get => FileUtilities.FixFilePath(GetAttributeValue(XMakeAttributes.project));
            set
            {
                ErrorUtilities.VerifyThrowArgumentLength(value, XMakeAttributes.project);

                SetOrRemoveAttribute(XMakeAttributes.project, value, "Set Import Project {0}", value);
            }
        }

        /// <summary>
        ///     Location of the project attribute
        /// </summary>
        public ElementLocation ProjectLocation => GetAttributeLocation(XMakeAttributes.project);

#nullable enable

        /// <summary>
        ///     Gets or sets the SDK that contains the import.
        /// </summary>
        public string? Sdk
        {
            get
            {
                switch (_sdkReferenceSource)
                {
                    case ISdkReferenceMutableSource mutableSource:
                        var query = mutableSource.SdkReferenceNameQuery;
                        return GetValue(query.Element, query.Factory);
                    case SdkReferenceConstantSource constantSource:
                        return constantSource.SdkReference.Reference.Name;
                    default:
                        throw CreateUnknownSourceException();
                }
            }
            set
            {
                ErrorUtilities.VerifyThrowArgumentLength(value, nameof(value));

                if (_sdkReferenceSource is ISdkReferenceMutableSource source)
                {
                    var query = source.SdkReferenceNameQuery;
                    Mutate(in query, NameAttributeFactory, value);
                }
                else
                {
                    PushValueFromImmutable(Sdk, value, NameAttributeFactory);
                }
            }
        }

        /// <summary>
        ///     Gets or sets the version associated with this SDK import
        /// </summary>
        public string? Version
        {
            get
            {
                switch (_sdkReferenceSource)
                {
                    case ISdkReferenceMutableSource mutableSource:
                        var query = mutableSource.SdkReferenceVersionQuery;
                        return GetValue(query.Element, query.Factory);
                    case SdkReferenceConstantSource constantSource:
                        return constantSource.SdkReference.Reference.Version;
                    default:
                        throw CreateUnknownSourceException();
                }
            }
            set
            {
                if (_sdkReferenceSource is ISdkReferenceMutableSource source)
                {
                    var query = source.SdkReferenceVersionQuery;
                    Mutate(in query, VersionAttributeFactory, value);
                }
                else
                {
                    PushValueFromImmutable(Version, value, VersionAttributeFactory);
                }
            }
        }

        /// <summary>
        ///     Gets or sets the minimum SDK version required by this import.
        /// </summary>
        public string? MinimumVersion
        {
            get
            {
                switch (_sdkReferenceSource)
                {
                    case ISdkReferenceMutableSource mutableSource:
                        var query = mutableSource.SdkReferenceMinimumVersionQuery;
                        return GetValue(query.Element, query.Factory);
                    case SdkReferenceConstantSource constantSource:
                        return constantSource.SdkReference.Reference.MinimumVersion;
                    default:
                        throw CreateUnknownSourceException();
                }
            }
            set
            {
                if (_sdkReferenceSource is ISdkReferenceMutableSource source)
                {
                    var query = source.SdkReferenceMinimumVersionQuery;
                    Mutate(in query, MinimumVersionAttributeFactory, value);
                }
                else
                {
                    PushValueFromImmutable(MinimumVersion, value, MinimumVersionAttributeFactory);
                }
            }
        }

        /// <summary>
        ///     Location of the Sdk attribute
        /// </summary>
        public ElementLocation? SdkLocation => SdkReferenceOrigin?.Name as ElementLocation;

        /// <summary>
        ///     Gets the <see cref="ImplicitImportLocation" /> of the import.  This indicates if the import was implicitly
        ///     added because of the <see cref="ProjectRootElement.Sdk" /> attribute and the location where the project was
        ///     imported.
        /// </summary>
        public ImplicitImportLocation ImplicitImportLocation
        {
            get => ImportLink?.ImplicitImportLocation ?? _implicitImportLocation;
            internal set => _implicitImportLocation = value;
        }

        /// <summary>
        ///     If the import is an implicit one (<see cref="ImplicitImportLocation" /> != None) then this element points
        ///     to the original element which generated this implicit import.
        /// </summary>
        public ProjectElement? OriginalElement
        {
            get => ImportLink != null ? ImportLink.OriginalElement : _originalElement;
            internal set => _originalElement = value;
        }

        /// <summary>
        ///     <see cref="Framework.SdkReference" /> if applicable to this import element.
        /// </summary>
        internal SdkReference? SdkReference => _sdkReferenceSource switch
        {
            ISdkReferenceMutableSource mutableSource => ComputeSdkReference(mutableSource),
            SdkReferenceConstantSource constantSource => constantSource.SdkReference.Reference,
            _ => throw CreateUnknownSourceException()
        };

        internal SdkReferenceWithOrigin? SdkReferenceWithOrigin => _sdkReferenceSource switch
        {
            ISdkReferenceMutableSource mutableSource => ComputeSdkReferenceWithOrigin(mutableSource),
            SdkReferenceConstantSource constantSource => constantSource.SdkReference,
            _ => throw CreateUnknownSourceException()
        };

        private SdkReferenceOrigin? SdkReferenceOrigin => _sdkReferenceSource switch
        {
            ISdkReferenceMutableSource mutableSource => ComputeSdkReferenceOrigin(mutableSource),
            SdkReferenceConstantSource constantSource => constantSource.SdkReference.Origin,
            _ => throw CreateUnknownSourceException()
        };

        /// <summary>
        ///     Creates an unparented ProjectImportElement, wrapping an unparented XmlElement.
        ///     Validates the project value.
        ///     Caller should then ensure the element is added to a parent
        /// </summary>
        internal static ProjectImportElement CreateDisconnected(string project, ProjectRootElement containingProject)
        {
            XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.import);
            return new ProjectImportElement(element, containingProject)
            {
                Project = project
            };
        }

        /// <summary>
        ///     Creates an implicit ProjectImportElement as if it was in the project.
        /// </summary>
        internal static ProjectImportElement CreateImplicit(
            string project,
            ProjectRootElement containingProject,
            ImplicitImportLocation implicitImportLocation,
            ISdkReferenceSource? sdkReferenceSource,
            ProjectElement originalElement)
        {
            XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.import);
            return new ProjectImportElement(element, null, containingProject, sdkReferenceSource)
            {
                Project = project,
                ImplicitImportLocation = implicitImportLocation,
                OriginalElement = originalElement
            };
        }

#nullable restore

        /// <inheritdoc />
        internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent,
                                                                             ProjectElement previousSibling,
                                                                             ProjectElement nextSibling) =>
            ErrorUtilities.VerifyThrowInvalidOperation(
                parent is ProjectRootElement || parent is ProjectImportGroupElement,
                "OM_CannotAcceptParent"
            );

        /// <inheritdoc />
        protected override ProjectElement CreateNewInstance(ProjectRootElement owner) =>
            owner.CreateImportElement(Project);

#nullable enable

        /// <summary>
        ///     Ensure that the value set on this element is equal to the value from the immutable source.
        /// </summary>
        /// <param name="current">The current attribute value</param>
        /// <param name="candidate">The value expected to be set after mutation</param>
        /// <param name="attribute">The <see cref="SdkReferenceAttribute" /> on which the mutation is performed</param>
        /// <exception cref="NotSupportedException">Exception thrown when candidate value isn't equal to the current value</exception>
        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
        private void PushValueFromImmutable(string? current, string? candidate, SdkReferenceAttribute attribute)
        {
            if (!string.Equals(current, candidate))
                throw CreateImmutableSourceException();

            SetValue(this, attribute, candidate);
        }

        /// <summary>
        ///     Apply the attribute mutation, both to the data source and this element.
        /// </summary>
        /// <param name="query">The <see cref="ISdkReferenceMutableSource" /> data source context</param>
        /// <param name="attribute">The <see cref="SdkReferenceAttribute" /> on which the mutation is performed</param>
        /// <param name="value">The new value to be set</param>
        private void Mutate(in SdkReferenceSourceQuery query, SdkReferenceAttribute attribute, string? value)
        {
            SetValue(query.Element, query.Factory, value);

            if (!ReferenceEquals(query.Element, this))
                SetValue(this, attribute, value);
        }

        private static SdkReferenceWithOrigin? ComputeSdkReferenceWithOrigin(ISdkReferenceMutableSource source)
        {
            var query = source.SdkReferenceFullQuery;

            GetValueLocation(query.Element, query.Sdk, out var name, out var nameLocation);

            if (name == null)
                return null;

            GetValueLocation(query.Element, query.Version, out var version, out var versionLocation);
            GetValueLocation(query.Element, query.MinimumVersion,
                             out var minimumVersion, out var minimumVersionLocation);

            return new SdkReferenceWithOrigin(
                new SdkReference(name, version, minimumVersion),
                new SdkReferenceOrigin(nameLocation, versionLocation, minimumVersionLocation)
            );
        }

        private static SdkReference? ComputeSdkReference(ISdkReferenceMutableSource source)
        {
            var query = source.SdkReferenceFullQuery;

            var name = GetValue(query.Element, query.Sdk);

            if (name == null)
                return null;

            var version = GetValue(query.Element, query.Version);
            var minimumVersion = GetValue(query.Element, query.MinimumVersion);

            return new SdkReference(name, version, minimumVersion);
        }

        private static SdkReferenceOrigin? ComputeSdkReferenceOrigin(ISdkReferenceMutableSource source)
        {
            var query = source.SdkReferenceFullQuery;

            GetValueLocation(query.Element, query.Sdk, out var name, out var nameLocation);

            if (name == null)
                return null;

            var versionLocation = GetLocation(query.Element, query.Version);
            var minimumVersionLocation = GetLocation(query.Element, query.MinimumVersion);

            return new SdkReferenceOrigin(nameLocation, versionLocation, minimumVersionLocation);
        }

        private static ArgumentOutOfRangeException CreateUnknownSourceException() =>
            new ArgumentOutOfRangeException(nameof(_sdkReferenceSource));

        private static Exception CreateImmutableSourceException() =>
            new NotSupportedException();

        private static string? GetValue(ProjectElement element, SdkReferenceAttribute factory) =>
            element.GetAttributeValue(factory.AttributeName, true);

        private static void SetValue(ProjectElement element, SdkReferenceAttribute factory, string? value)
        {
            var attributeName = factory.AttributeName;

            if (string.Equals(element.GetAttributeValue(attributeName, true), value))
                return;

            element.SetOrRemoveAttribute(attributeName, value, factory.ChangeReasonMessage, value);
        }

        private static IElementLocation? GetLocation(ProjectElement element, SdkReferenceAttribute factory) =>
            element.GetAttributeLocation(factory.AttributeName);

        private static void GetValueLocation(ProjectElement element, SdkReferenceAttribute factory,
                                             out string? value, out IElementLocation? location)
        {
            var attributeName = factory.AttributeName;

            if (element.Link == null &&
                element.XmlElement?.GetAttributeNode(attributeName) is XmlAttributeWithLocation attribute)
            {
                value = attribute.Value;
                location = attribute.Location;
            }
            else
            {
                value = element.GetAttributeValue(attributeName, true);
                location = element.GetAttributeLocation(attributeName);
            }
        }

        SdkReferenceSourceQuery ISdkReferenceMutableSource.SdkReferenceNameQuery =>
            new SdkReferenceSourceQuery(this, NameAttributeFactory);

        SdkReferenceSourceQuery ISdkReferenceMutableSource.SdkReferenceVersionQuery =>
            new SdkReferenceSourceQuery(this, VersionAttributeFactory);

        SdkReferenceSourceQuery ISdkReferenceMutableSource.SdkReferenceMinimumVersionQuery =>
            new SdkReferenceSourceQuery(this, MinimumVersionAttributeFactory);

        SdkReferenceSourceFullQuery ISdkReferenceMutableSource.SdkReferenceFullQuery =>
            new SdkReferenceSourceFullQuery(
                this, NameAttributeFactory, VersionAttributeFactory, MinimumVersionAttributeFactory
            );
    }
}
