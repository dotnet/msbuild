// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Data;
using System.Xml;
using System.IO;
using System.Text;
using System.Collections;
using System.Diagnostics;
using System.Globalization;

using Microsoft.Build.BuildEngine.Shared;

using error = Microsoft.Build.BuildEngine.Shared.ErrorUtilities;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// A BuildPropertyGroup is a collection of BuildProperty objects. This could be represented by a persisted &lt;PropertyGroup&gt;
    /// element in the project file, or it could be a virtual collection of properties, such as in the case of global properties,
    /// environment variable properties, or the final evaluated properties of a project. These two types of PropertyGroups
    /// (persisted and virtual) are handled differently by many of the methods in this class, but in order to reduce the number of
    /// concepts for the consumer of the OM, we've merged them into a single class.
    /// </summary>
    /// <owner>RGoel</owner>
    [DebuggerDisplay("BuildPropertyGroup (Count = { Count }, Condition = { Condition })")]
    public class BuildPropertyGroup : IItemPropertyGrouping, IEnumerable
    {
        #region Member Data

        // This is the XML element representing the <PropertyGroup> in the XMake
        // project file.  If this BuildPropertyGroup object doesn't represent an
        // actual <PropertyGroup> element in the XMake project file, it's 
        // okay if this remains null throughout the life of this object.
        private XmlElement propertyGroupElement = null;

        // If this is a persisted <PropertyGroup>, this boolean tells us whether
        // it came from the main project file, or an imported project file.
        private bool importedFromAnotherProject = false;

        // the ownerDocument is used for creating and adding new properties
        private XmlDocument ownerDocument = null;

        // This is the "Condition" attribute on the <PropertyGroup> element above.
        private XmlAttribute conditionAttribute = null;

        // If this is a persisted BuildPropertyGroup, it has a parent Project object.
        private Project parentProject = null;

        // Collection property belongs to.
        private GroupingCollection parentCollection = null;

        // This is a table of BuildProperty objects, hashed by property name, so
        // that specific properties can be found easily.  Obviously, there
        // can only be one property with a given name in this table.  This
        // table is only valid (non-null) for virtual property groups (i.e.,
        // those not persisted in a <PropertyGroup> element in the project).
        private CopyOnWriteHashtable propertyTableByName = null;

        // holds properties that have been overridden by output properties from tasks, so that we can restore them on demand
        private CopyOnWriteHashtable propertiesOverriddenByOutputProperties;

        // This is the raw list of all the properties in this property group,
        // in the order that they appear in the project file.  In this list,
        // there can be multiple properties of the same name.  This member
        // is only valid (non-null) if this is a persisted <PropertyGroup>.
        // For virtual property groups (e.g., evaluated property groups, 
        // global property groups, etc.), this will remain null.
        private ArrayList propertyList = null;

        // Contains the name of the project file this property group was
        // imported from.  This string will only be set if the property group
        // was created by the IDE adding a new property.
        private string importedFromFilename = null;

        #endregion

        #region CustomSerializationToStream

        internal void WriteToStream(BinaryWriter writer)
        {
            if (propertyTableByName == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write((Int32)propertyTableByName.Count);
                foreach (string key in propertyTableByName.Keys)
                {
                    writer.Write(key);
                    if (propertyTableByName[key] == null)
                    {
                        writer.Write((byte)0);
                    }
                    else
                    {
                        writer.Write((byte)1);
                        ((BuildProperty)propertyTableByName[key]).WriteToStream(writer);
                    }
                }
            }
        }

        internal void CreateFromStream(BinaryReader reader)
        {
            if (reader.ReadByte() == 0)
            {
                propertyTableByName = null;
            }
            else
            {
                // Write Number of HashItems
                int numberOfHashKeyValuePairs = reader.ReadInt32();
                propertyTableByName = new CopyOnWriteHashtable(numberOfHashKeyValuePairs, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < numberOfHashKeyValuePairs; i++)
                {
                    string key = reader.ReadString();
                    BuildProperty value = null;
                    if (reader.ReadByte() == 1)
                    {
                        value = BuildProperty.CreateFromStream(reader);
                    }
                    propertyTableByName.Add(key, value);
                }
            }
        }
        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor, that creates an empty virtual (non-persisted) BuildPropertyGroup.
        /// </summary>
        public BuildPropertyGroup()
            : this(null, 0)
        {
        }

        /// <summary>
        /// Default constructor, that creates an empty virtual (non-persisted) BuildPropertyGroup.
        /// </summary>
        public BuildPropertyGroup(Project parentProject)
            : this(parentProject, 0)
        {
        }

        /// <summary>
        /// Constructor for empty virtual (non-persisted) BuildPropertyGroup. Use this constructor
        /// when the initial number of properties can be estimated, to reduce re-sizing of the list.
        /// </summary>
        private BuildPropertyGroup(Project parentProject, int capacity)
        {
            this.parentProject = parentProject;
            this.propertyGroupElement = null;
            this.importedFromAnotherProject = false;
            this.conditionAttribute = null;

            this.propertyTableByName = new CopyOnWriteHashtable(capacity, StringComparer.OrdinalIgnoreCase);
            this.propertyList = null;
        }

        /// <summary>
        /// Constructor, from an existing &lt;PropertyGroup&gt; XML element.
        /// </summary>
        /// <param name="importedFilename"></param>
        /// <param name="condition"></param>
        /// <owner>RGoel</owner>
        internal BuildPropertyGroup
        (
            Project parentProject,
            string importedFilename,
            string condition
        ) : this(parentProject, Engine.GlobalDummyXmlDoc, true)
        {
            // Set the new "Condition" attribute on the <PropertyGroup> element.
            this.propertyGroupElement.SetAttribute(XMakeAttributes.condition, condition);
            this.conditionAttribute = this.propertyGroupElement.Attributes[XMakeAttributes.condition];
            this.importedFromFilename = importedFilename;
        }

        /// <summary>
        /// Constructor, from an existing &lt;PropertyGroup&gt; XML element in the
        /// main project file.
        /// </summary>
        internal BuildPropertyGroup(Project parentProject, XmlElement propertyGroupElement)
            : this(parentProject, propertyGroupElement, PropertyType.NormalProperty)
        {
        }

        /// <summary>
        /// Constructor, from an existing &lt;PropertyGroup&gt; XML element, which may
        /// be imported
        /// </summary>
        internal BuildPropertyGroup(Project parentProject, XmlElement propertyGroupElement, bool isImported)
            : this(parentProject, propertyGroupElement, (isImported ? PropertyType.ImportedProperty : PropertyType.NormalProperty))
        {
        }

        /// <summary>
        /// Constructor, from an existing &lt;PropertyGroup&gt; XML element.
        /// </summary>
        internal BuildPropertyGroup(Project parentProject, XmlElement propertyGroupElement, PropertyType propertyType)
        {
            error.VerifyThrow(propertyGroupElement != null, "Need valid <PropertyGroup> element.");

            // Make sure this really is the <PropertyGroup> node.
            ProjectXmlUtilities.VerifyThrowElementName(propertyGroupElement, XMakeElements.propertyGroup);

            this.parentProject = parentProject;

            this.propertyGroupElement = propertyGroupElement;
            this.importedFromAnotherProject = (propertyType == PropertyType.ImportedProperty);
            this.conditionAttribute = null;

            this.propertyTableByName = null;
            this.propertyList = new ArrayList();

            this.ownerDocument = propertyGroupElement.OwnerDocument;

            // This <PropertyGroup> is coming from an existing XML element, so
            // walk through all the attributes and child elements, creating the
            // necessary BuildProperty objects.

            // Loop through the list of attributes on the <PropertyGroup> element.
            foreach (XmlAttribute propertyGroupAttribute in this.propertyGroupElement.Attributes)
            {
                switch (propertyGroupAttribute.Name)
                {
                    // Process the "condition" attribute.
                    case XMakeAttributes.condition:
                        this.conditionAttribute = propertyGroupAttribute;
                        break;

                    // only recognized by the new OM:
                    // just ignore here
                    case XMakeAttributes.label:
                        // do nothing
                        break;

                    // Unrecognized attribute.
                    default:
                        ProjectXmlUtilities.ThrowProjectInvalidAttribute(propertyGroupAttribute); 
                        break;
                }
            }

            // Loop through the child nodes of the <PropertyGroup> element.
            foreach (XmlNode propertyGroupChildNode in this.propertyGroupElement)
            {
                switch (propertyGroupChildNode.NodeType)
                {
                    // Handle XML comments under the <PropertyGroup> node (just ignore them).
                    case XmlNodeType.Comment:
                        // fall through
                    case XmlNodeType.Whitespace:
                        // ignore whitespace
                        break;

                    case XmlNodeType.Element:
                        // The only type of child node that a <PropertyGroup> element can contain
                        // is a property element.

                        // Make sure the property doesn't have a custom namespace
                        ProjectXmlUtilities.VerifyThrowProjectValidNamespace((XmlElement)propertyGroupChildNode);

                        // Send the property element to another class for processing.
                        BuildProperty newProperty = new BuildProperty((XmlElement)propertyGroupChildNode, propertyType);
                        newProperty.ParentPersistedPropertyGroup = this;
                        this.propertyList.Add(newProperty);
                        break;

                    default:
                        // Unrecognized child element.
                        ProjectXmlUtilities.ThrowProjectInvalidChildElement(propertyGroupChildNode);
                        break;
                }
            }
        }

        /// <summary>
        /// Constructor which creates a new &lt;PropertyGroup&gt; in the XML document
        /// specified.
        /// </summary>
        /// <param name="ownerDocument"></param>
        /// <param name="importedFromAnotherProject"></param>
        /// <owner>RGoel</owner>
        internal BuildPropertyGroup
        (
            Project parentProject,
            XmlDocument ownerDocument,
            bool importedFromAnotherProject
        )
        {
            error.VerifyThrow(ownerDocument != null, "Need valid XmlDocument owner for this property group.");

            this.parentProject = parentProject;

            // Create the new <PropertyGroup> XML element.
            this.propertyGroupElement = ownerDocument.CreateElement(XMakeElements.propertyGroup, XMakeAttributes.defaultXmlNamespace);
            this.importedFromAnotherProject = importedFromAnotherProject;

            this.ownerDocument = ownerDocument;

            this.conditionAttribute = null;

            this.propertyTableByName = null;
            this.propertyList = new ArrayList();
        }

        #endregion

        #region Properties

        /// <summary>
        /// This returns a boolean telling you whether this particular property
        /// group was imported from another project, or whether it was defined
        /// in the main project.  For virtual property groups which have no
        /// persistence, this is false.
        /// </summary>
        /// <owner>RGoel</owner>
        public bool IsImported
        {
            get
            {
                return this.importedFromAnotherProject;
            }
        }

        /// <summary>
        /// Accessor for the condition on the property group.
        /// </summary>
        /// <owner>RGoel</owner>
        public string Condition
        {
            get
            {
                return (this.conditionAttribute == null) ? String.Empty : this.conditionAttribute.Value;
            }

            set
            {
                // If this BuildPropertyGroup object is not actually represented by a 
                // <PropertyGroup> element in the project file, then do not allow
                // the caller to set the condition.
                MustBePersisted("CannotSetCondition", null);

                // If this BuildPropertyGroup was imported from another project, we don't allow modifying it.
                error.VerifyThrowInvalidOperation(!this.importedFromAnotherProject,
                    "CannotModifyImportedProjects");

                this.conditionAttribute = ProjectXmlUtilities.SetOrRemoveAttribute(propertyGroupElement, XMakeAttributes.condition, value);

                this.MarkPropertyGroupAsDirty();
            }
        }

        /// <summary>
        /// Allows setting the condition for imported property groups. Changes will not be persisted.
        /// </summary>
        public void SetImportedPropertyGroupCondition(string condition)
        {
            // If this BuildPropertyGroup object is not actually represented by a 
            // <PropertyGroup> element in the project file, then do not allow
            // the caller to set the condition.
            MustBePersisted("CannotSetCondition", null);

            this.conditionAttribute = ProjectXmlUtilities.SetOrRemoveAttribute(propertyGroupElement, XMakeAttributes.condition, condition);

            this.MarkPropertyGroupAsDirty();
        }

        /// <summary>
        /// Read-only accessor for accessing the XML attribute for "Condition".  Callers should
        /// never try and modify this.  Go through this.Condition to change the condition.
        /// </summary>
        /// <owner>RGoel</owner>
        internal XmlAttribute ConditionAttribute
        {
            get
            {
                return this.conditionAttribute;
            }
        }

        /// <summary>
        /// Accessor for the XmlElement representing this property group.  This is 
        /// internal to MSBuild, and is read-only.
        /// </summary>
        /// <owner>RGoel</owner>
        internal XmlElement PropertyGroupElement
        {
            get
            {
                return this.propertyGroupElement;
            }
        }

        /// <summary>
        /// Accessor for the parent Project object.
        /// </summary>
        internal Project ParentProject
        {
            get { return parentProject; }
            set { parentProject = value; }
        }

        /// <summary>
        /// Setter for parent project field that makes explicit that's it's only for clearing it.
        /// </summary>
        internal void ClearParentProject()
        {
            parentProject = null;
        }

        /// <summary>
        /// Accessor for the ParentCollection
        /// </summary>
        /// <returns>Collection BuildPropertyGroup belongs to</returns>
        /// <owner>davidle</owner>
        internal GroupingCollection ParentCollection
        {
            get
            {
                return this.parentCollection;
            }

            set
            {
                this.parentCollection = value;
            }
        }

        /// <summary>
        /// Accessor for the XML parent element
        /// </summary>
        /// <returns>Collection BuildPropertyGroup belongs to</returns>
        /// <owner>davidle</owner>
        internal XmlElement ParentElement
        {
            get
            {
                if (this.propertyGroupElement != null)
                {
                    if (this.propertyGroupElement.ParentNode is XmlElement)
                    {
                        return (XmlElement) this.propertyGroupElement.ParentNode;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Returns the number of properties contained in this BuildPropertyGroup.
        /// </summary>
        /// <owner>RGoel</owner>
        public int Count
        {
            get
            {
                // If this is a persisted <PropertyGroup>, return the count on the ArrayList.
                if (this.propertyList != null)
                {
                    error.VerifyThrow(this.propertyTableByName == null, "Did not expect a property hash table.");

                    return this.propertyList.Count;
                }

                // If this is a virtual BuildPropertyGroup, return the count on the HashTable.
                if (this.propertyTableByName != null)
                {
                    return this.propertyTableByName.Count;
                }

                // If this is neither, we have an internal failure.
                error.VerifyThrow(false, "Expected either a hash table or an array list of properties.");

                return 0;
            }
        }

        /// <summary>
        /// Implements logic to get filename from propertygroups that doesn't have an
        /// XmlElement behind it.
        /// </summary>
        /// <returns>string containing filename of import file</returns>
        /// <owner>davidle</owner>
        internal string ImportedFromFilename
        {
            get
            {
                if (!this.importedFromAnotherProject)
                    return string.Empty;
                if (this.importedFromFilename != null)
                    return this.importedFromFilename;
                if (this.PropertyGroupElement != null)
                    return XmlUtilities.GetXmlNodeFile(this.PropertyGroupElement, string.Empty);
                ErrorUtilities.VerifyThrow(false, "BuildPropertyGroup is imported, doesn't have an ownerDocument, and importedFilename is null.");
                return string.Empty;
            }

            set
            {
                ErrorUtilities.VerifyThrow(this.importedFromAnotherProject, "You can't set the imported filename on a non-imported PropertyGroup.");
                this.importedFromFilename = value;
            }
        }
        #endregion

        #region Operators

        /// <summary>
        /// This is the indexer for the BuildPropertyGroup class, which allows the caller to set or get the property data using simple
        /// array indexer [] notation. The caller passes in the property name inside the [], and out comes the  BuildProperty object,
        /// which can be typecast to a string in order to get just the property value. Or if it's used on the left of the "="
        /// sign, the  same notation can set a new BuildProperty object, overwriting.
        /// Getting a value requires the property group be virtual.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <param name="propertyName"></param>
        /// <returns>The property with the given name, or null if it does not exist in this group</returns>
        public BuildProperty this[string propertyName]
        {
            get 
            {
                // We don't support this method for PropertyGroups that are persisted.
                // This is because persisted PropertyGroups can contain multiple 
                // properties with the same name, so you can't index by name.
                MustBeVirtual("CannotAccessPropertyByName");

                // Do the lookup in the hash table using the hash table's 
                // indexer method.  Get back the property data object, 
                // which will be "null" if the property hasn't been set. Note
                // that we key off property names in a case-insensitive fashion.
                return (BuildProperty)propertyTableByName[propertyName];
            }

            set
            {
                error.VerifyThrowArgument(value != null, "CannotSetPropertyToNull");

                // Make sure that the property name passed into the indexer matches
                // the property name on the BuildProperty object.
                error.VerifyThrowArgument(0 == String.Compare(propertyName, value.Name, StringComparison.OrdinalIgnoreCase),
                    "PropertyNamesDoNotMatch", "BuildProperty");

                this.SetProperty(value);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// This IEnumerable method returns an IEnumerator object, which allows
        /// the caller to enumerate through the BuildProperty objects contained in
        /// this BuildPropertyGroup.
        /// </summary>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        public IEnumerator GetEnumerator
            (
            )
        {
            // If this is a persisted <PropertyGroup>, return the enumerator on the ArrayList.
            if (this.propertyList != null)
            {
                error.VerifyThrow(this.propertyTableByName == null, "Did not expect a property hash table.");

                return this.propertyList.GetEnumerator();
            }

            // If this is a virtual BuildPropertyGroup, return the enumerator on the HashTable.
            if (this.propertyTableByName != null)
            {
                // Unfortunately, hash tables return a bunch DictionaryEntry's, and the
                // real BuildProperty object is stored in DictionaryEntry.Value.  In order to
                // make life a little easier for the caller, we give him an enumerator over just the values.
                return this.propertyTableByName.Values.GetEnumerator();
            }

            // If this is neither, we have an internal failure.
            error.VerifyThrow(false, "Expected either a hash table or an array list of properties.");

            return null;
        }

        /// <summary>
        /// Does a shallow clone, creating a new group with pointers to the same properties as the old group.
        /// </summary>
        internal BuildPropertyGroup ShallowClone()
        {
            return Clone(false /* shallow */);
        }

        /// <summary>
        /// This method creates a copy of the BuildPropertyGroup. A shallow clone will reference the same BuildProperty objects as the
        /// original. A deep clone will deep clone the BuildProperty objects themselves. If this is a persisted BuildPropertyGroup, only
        /// deep clones are allowed, because you can't have the same XML element belonging to two parents.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <param name="deepClone"></param>
        /// <returns>The cloned BuildPropertyGroup.</returns>
        public BuildPropertyGroup Clone
        (
            bool deepClone
        )
        {
            BuildPropertyGroup clone;

            if (IsVirtual)
            {
                // This is a virtual BuildPropertyGroup.
                MustBeVirtual("NeedVirtualPropertyGroup");

                if (deepClone)
                {
                    // Loop through every BuildProperty in our collection, and add those same properties 
                    // to the cloned collection.

                    // Create a new virtual BuildPropertyGroup.
                    // Do not set the ParentProject on the new BuildPropertyGroup, because it isn't really
                    // part of the project
                    clone = new BuildPropertyGroup(null, propertyTableByName.Count);
                    
                    foreach (DictionaryEntry propertyEntry in this.propertyTableByName)
                    {
                        // If the caller requested a deep clone, then deep clone the BuildProperty object,
                        // and add the new BuildProperty to the new BuildPropertyGroup.
                        clone.propertyTableByName.Add(propertyEntry.Key, ((BuildProperty)propertyEntry.Value).Clone(true /* deep clone */));
                    }

                    // also clone over any overridden non-output properties
                    if (this.propertiesOverriddenByOutputProperties != null)
                    {
                        clone.propertiesOverriddenByOutputProperties = new CopyOnWriteHashtable(propertiesOverriddenByOutputProperties.Count, StringComparer.OrdinalIgnoreCase);

                        foreach (DictionaryEntry propertyEntry in this.propertiesOverriddenByOutputProperties)
                        {
                            if (propertyEntry.Value != null)
                            {
                                clone.propertiesOverriddenByOutputProperties.Add(propertyEntry.Key, ((BuildProperty)propertyEntry.Value).Clone(true /* deep clone */));
                            }
                            else
                            {
                                clone.propertiesOverriddenByOutputProperties.Add(propertyEntry.Key, null);
                            }
                        }
                    }
                }
                else
                {
                    // shallow cloning is easy, we only clone the Hashtables
                    clone = new BuildPropertyGroup();
                    clone.propertyTableByName = (CopyOnWriteHashtable)this.propertyTableByName.Clone();

                    if (this.propertiesOverriddenByOutputProperties != null)
                    {
                        clone.propertiesOverriddenByOutputProperties = (CopyOnWriteHashtable)this.propertiesOverriddenByOutputProperties.Clone();
                    }
                }
            }
            else
            {
                // This is a persisted <PropertyGroup>.
                MustBePersisted("NeedPersistedPropertyGroup", XMakeElements.propertyGroup);

                // Only deep clones are permitted when dealing with a persisted <PropertyGroup>.
                // This is because a shallow clone would attempt to add the same property
                // elements to two different parent <PropertyGroup> elements, and this is
                // not allowed.
                error.VerifyThrowInvalidOperation(deepClone, "ShallowCloneNotAllowed");

                // Create a new persisted <PropertyGroup>.  It won't actually get added
                // to any XmlDocument, but it will be associated with the same XmlDocument
                // as the current BuildPropertyGroup.
                clone = new BuildPropertyGroup
                   (
                    null, /* Do not set the ParentProject on the cloned BuildPropertyGroup, because it isn't really
                           part of the project */
                    ownerDocument,
                    this.importedFromAnotherProject
                    );

                // Loop through every BuildProperty in our collection, and add those same properties 
                // to the cloned collection.
                foreach (BuildProperty property in this)
                {
                    // If the caller requested a deep clone, then deep clone the BuildProperty object,
                    // and add the new BuildProperty to the new BuildPropertyGroup.  Otherwise, just add
                    // a reference to the existing BuildProperty object to the new BuildPropertyGroup.
                    clone.AddProperty(property.Clone(true));
                }

                clone.Condition = this.Condition;
            }

            // Return the cloned collection to the caller.
            return clone;
        }

        /// <summary>
        /// ImportInitialProperties is used when setting up an evaluated BuildProperty
        /// Group with the initial set of properties from MSBuild reserved properties,
        /// environment variables, tools version dependent properties, and global 
        /// properties.  After this virtual BuildPropertyGroup has been populated with 
        /// these, we can continue to read in the properties from the project file.
        /// </summary>
        /// <param name="environmentProperties"></param>
        /// <param name="reservedProperties"></param>
        /// <param name="toolsVersionDependentProperties"></param>
        /// <param name="globalProperties"></param>
        /// <owner>RGoel</owner>
        internal void ImportInitialProperties
        (
            BuildPropertyGroup environmentProperties, 
            BuildPropertyGroup reservedProperties, 
            BuildPropertyGroup toolsVersionDependentProperties, 
            BuildPropertyGroup globalProperties
        )
        {
            // The consumer of the OM has the ability to add new properties to the 
            // GlobalProperties BuildPropertyGroup, and the OM doesn't expose the 
            // property type, because that would be too dangerous.  So all properties
            // created by the OM consumer will be "normal" properties, even those
            // set in the GlobalProperties BuildPropertyGroup.  But in order to make
            // property precedence work correctly, we should now go through and
            // make sure that all the global properties really are of type "global".
            // However, we don't want to modify the original global property group,
            // so we clone it here.
            BuildPropertyGroup clonedGlobalProperties = globalProperties.Clone(true);

            foreach (BuildProperty globalProperty in clonedGlobalProperties)
            {
                globalProperty.Type = PropertyType.GlobalProperty;
            }

            // Import the environment variables into this virtual BuildPropertyGroup.
            this.ImportProperties(environmentProperties);

            // Import the XMake reserved properties into this virtual BuildPropertyGroup.
            this.ImportProperties(reservedProperties);

            // Import the tools version dependent properties into this virtual BuildPropertyGroup.
            this.ImportProperties(toolsVersionDependentProperties);

            // Import the global properties into this virtual BuildPropertyGroup.
            this.ImportProperties(clonedGlobalProperties);
        }

        /// <summary>
        /// Sets a property. 
        ///
        /// Either overrides the value of the property with the given name, or adds it if it
        /// doesn't already exist. Setting to the same value as before does nothing.
        ///
        /// This method will take into account property precedence rules, so that for
        /// example, a reserved MSBuild property cannot be overridden by a normal property.
        ///
        /// PropertyGroup must be virtual.
        /// </summary>
        /// <param name="newProperty"></param>
        internal void SetProperty
        (
            BuildProperty newProperty
        )
        {
            // We don't support this method for PropertyGroups that are
            // represented by an actual <PropertyGroup> element.  This is because
            // persisted PropertyGroups can contain multiple properties with the same 
            // name, so the behavior of SetProperty becomes ambiguous.
            MustBeVirtual("NeedVirtualPropertyGroup");

            // If a property with this name already exists in our collection, then we have
            // to override it, taking into account the precedence rules for properties.
            BuildProperty existingProperty = (BuildProperty)propertyTableByName[newProperty.Name];

            bool isEquivalentToExistingProperty = false;

            if (existingProperty != null)
            {
                // If the existing property is an XMake reserved property, we may have an 
                // invalid project file, because reserved properties are not allowed to
                // be set.
                // Don't fail if the new property is itself a "reserved" property.  We 
                // want to be able to override reserved properties with new reserved 
                // properties, otherwise the engine itself would never be allowed to 
                // change the value of a reserved property.
                ProjectErrorUtilities.VerifyThrowInvalidProject(
                    (existingProperty.Type != PropertyType.ReservedProperty) ||
                    (newProperty.Type == PropertyType.ReservedProperty),
                    newProperty.PropertyElement, "CannotModifyReservedProperty", newProperty.Name);

                // Also make sure it's not a read-only property (such as a property
                // that was set at the XMake command-line), but don't actually throw
                // an error in this case.  Only output properties from tasks are allowed 
                // to override read-only properties
                if ((existingProperty.Type == PropertyType.GlobalProperty) &&
                    (newProperty.Type != PropertyType.OutputProperty))
                {
                    return;
                }

                isEquivalentToExistingProperty = newProperty.IsEquivalent(existingProperty);

                if (!isEquivalentToExistingProperty)
                {
                    // Allow properties to be "set" to the same value during a build. This is because Visual Studio unfortunately does this often,
                    // and it is safe to do this, because we won't actually change any state.
                    ErrorUtilities.VerifyThrowInvalidOperation(parentProject == null || !parentProject.IsBuilding, "CannotSetPropertyDuringBuild");
                }
            }

            // Keep track of all output properties, so we can remove them later.
            if (newProperty.Type == PropertyType.OutputProperty)
            {
                if (propertiesOverriddenByOutputProperties == null)
                {
                    propertiesOverriddenByOutputProperties = new CopyOnWriteHashtable(StringComparer.OrdinalIgnoreCase);
                }

                if (propertiesOverriddenByOutputProperties.Contains(newProperty.Name))
                {
                    error.VerifyThrow(existingProperty != null, "If we've overridden this property before, it must exist in the main property table.");
                    error.VerifyThrow(existingProperty.Type == PropertyType.OutputProperty, "If we've overriden this property before, it must be stored as an output property in the main property table.");
                }
                else
                {
                    error.VerifyThrow((existingProperty == null) || (existingProperty.Type != PropertyType.OutputProperty), 
                        "If the property already exists in the main property table, it can't already be there as an output property, because then we would have stored an entry in propertiesOverriddenByOutputProperties.");

                    // NOTE: Use Hashtable.Add() because each output property should only be added to this
                    // table once.  If we ever try to add the same output property to this table twice,
                    // it's a bug in our code.
                    // "existingProperty" may be null, and that's okay.
                    propertiesOverriddenByOutputProperties.Add(newProperty.Name, existingProperty);
                }
            }

            // Okay, now actually set our property, but only if the value has actually changed.
            if (!isEquivalentToExistingProperty)
            {
                this.propertyTableByName[newProperty.Name] = newProperty;
                this.MarkPropertyGroupAsDirty();
            }
        }

        /// <summary>
        /// Sets a property taking the property name and value as strings directly. 
        /// 
        /// Either overrides the value of the property with the given name, or adds it if it
        /// doesn't already exist. Setting to the same value as before does nothing.
        ///
        /// This method will take into account property precedence rules, so that for
        /// example, a reserved MSBuild property cannot be overridden by a normal property.
        ///
        /// PropertyGroup must be virtual.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="propertyValue"></param>
        public void SetProperty(string propertyName, string propertyValue)
        {
            this.SetProperty(new BuildProperty(propertyName, propertyValue));
        }

        /// <summary>
        /// Sets a property in this PropertyGroup, optionally escaping the property value so
        /// that it will be treated as a literal.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="propertyValue"></param>
        /// <param name="treatPropertyValueAsLiteral"></param>
        /// <owner>RGoel</owner>
        public void SetProperty
            (
            string propertyName,
            string propertyValue,
            bool treatPropertyValueAsLiteral
            )
        {
            this.SetProperty(propertyName, 
                treatPropertyValueAsLiteral ? EscapingUtilities.Escape(propertyValue) : propertyValue);
        }

        /// <summary>
        /// The AddNewProperty method adds a new property element to the persisted
        /// &lt;PropertyGroup&gt; at the end.  This method takes the property name and
        /// value as strings directly, so that the BuildProperty object can be created
        /// with the correct owner XML document and parent XML element.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="propertyValue"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        public BuildProperty AddNewProperty
        (
            string propertyName,
            string propertyValue
        )
        {
            MustBePersisted("NeedPersistedPropertyGroup", XMakeElements.propertyGroup);

            error.VerifyThrowInvalidOperation(!this.importedFromAnotherProject,
                "CannotModifyImportedProjects");

            BuildProperty newProperty = new BuildProperty(this.ownerDocument, propertyName, propertyValue, PropertyType.NormalProperty);

            this.AddProperty(newProperty);

            return newProperty;
        }

        /// <summary>
        /// Adds a new property to the PropertyGroup, optionally escaping the property value so
        /// that it will be treated as a literal.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="propertyValue"></param>
        /// <param name="treatPropertyValueAsLiteral"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        public BuildProperty AddNewProperty
            (
            string propertyName,
            string propertyValue,
            bool treatPropertyValueAsLiteral
            )
        {
            return this.AddNewProperty(propertyName,
                treatPropertyValueAsLiteral ? EscapingUtilities.Escape(propertyValue) : propertyValue);
        }

        /// <summary>
        /// The AddNewImportedProperty method adds a new imported propert element.
        /// This method takes the property name and value as strings directly.  The
        /// Project representing the imported Project is passed in so parent document
        /// can be retrieved.
        /// </summary>
        /// <owner>DavidLe</owner>
        /// <param name="propertyName"></param>
        /// <param name="propertyValue"></param>
        /// <param name="importedProject"></param>
        /// <returns>The new BuildPropertyGroup.</returns>
        internal BuildProperty AddNewImportedProperty
        (
            string propertyName,
            string propertyValue,
            Project importedProject
        )
        {
            MustBePersisted("NeedPersistedPropertyGroup", XMakeElements.propertyGroup);
            XmlDocument parentDocument = importedProject.XmlDocument;

            BuildProperty newProperty = new BuildProperty(parentDocument, propertyName, propertyValue, PropertyType.ImportedProperty);

            this.AddExistingProperty(newProperty);

            return newProperty;
        }

        /// <summary>
        /// Adds an existing BuildProperty to the list of properties, does not attempt 
        /// to add backing Xml for the item.
        /// </summary>
        /// <param name="propertyToAdd"></param>
        /// <owner>JomoF</owner>
        internal void AddExistingProperty 
        (
            BuildProperty propertyToAdd
        )
        {
            MustBePersisted("NeedPersistedPropertyGroup", XMakeElements.propertyGroup);

            // Add the item to our list.
            propertyToAdd.ParentPersistedPropertyGroup = this;
            this.propertyList.Add(propertyToAdd);
            this.MarkPropertyGroupAsDirty();
        }

        /// <summary>
        /// The AddProperty method adds an existing property element to the persisted
        /// &lt;PropertyGroup&gt; at the end.  This property element must be associated
        /// with the same Xml document as the &lt;PropertyGroup&gt;.
        /// </summary>
        /// <param name="propertyToAdd"></param>
        /// <owner>RGoel</owner>
        internal void AddProperty
        (
            BuildProperty propertyToAdd
        )
        {
            MustBePersisted("NeedPersistedPropertyGroup", XMakeElements.propertyGroup);

            // We don't allow any modifications to the XML of any of the imported
            // project files ... only the main project file.
            error.VerifyThrowInvalidOperation(!this.importedFromAnotherProject,
                "CannotModifyImportedProjects");

            // Make sure the property to be added has an XML element backing it,
            // and that its XML belongs to the same XML document as our BuildPropertyGroup.
            error.VerifyThrow(propertyToAdd.PropertyElement != null, "BuildProperty does not have an XML element");
            error.VerifyThrow(propertyToAdd.PropertyElement.OwnerDocument == this.ownerDocument, 
                "Cannot add an BuildProperty with a different XML owner document.");

            // For persisted groups, just append the property at the end of the <BuildPropertyGroup> tag.
            this.propertyGroupElement.AppendChild(propertyToAdd.PropertyElement);

            // Add the property to our list.
            this.AddExistingProperty(propertyToAdd);
        }

        /// <summary>
        /// Removes the given BuildProperty object from either a persisted or a virtual
        /// BuildPropertyGroup.
        /// </summary>
        /// <param name="property"></param>
        /// <owner>RGoel</owner>
        public void RemoveProperty
        (
            BuildProperty property
        )
        {
            error.VerifyThrowArgumentNull(property, "property");

            // If this is a persisted <PropertyGroup>, then remove the property element from 
            // the XML and from the array list.
            if (IsPersisted)
            {
                MustBePersisted("NeedPersistedPropertyGroup", XMakeElements.propertyGroup);

                // We don't allow any modifications to the XML of any of the imported
                // project files ... only the main project file.
                error.VerifyThrowInvalidOperation(!this.importedFromAnotherProject,
                    "CannotModifyImportedProjects");

                // Find the property XML element.
                XmlElement propertyElement = property.PropertyElement;

                MustBelongToPropertyGroup(propertyElement);

                // Get the parent node, and then delete this property from it.
                propertyElement.ParentNode.RemoveChild(propertyElement);

                // Remove the property object from the array list.
                this.propertyList.Remove(property);
                property.ParentPersistedPropertyGroup = null;
            }
            else
            {
                RemoveProperty(property.Name);
            }

            this.MarkPropertyGroupAsDirty();
        }

        /// <summary>
        /// Removes all properties with the given name from either a persisted or a virtual BuildPropertyGroup. For persisted
        /// PropertyGroups, there could be multiple. For a virtual BuildPropertyGroup, there can be only one.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <param name="propertyName"></param>
        public void RemoveProperty
        (
            string propertyName
        )
        {
            // If this is a persisted <PropertyGroup>, then remove all of the properties
            // with the specified name.
            if (IsPersisted)
            {
                MustBePersisted("NeedPersistedPropertyGroup", XMakeElements.propertyGroup);

                // For persisted <PropertyGroup>'s, there could be multiple properties 
                // with the given name.  We need to loop through our arraylist of properties, 
                // finding all the ones with the given property name, and delete them.  But we 
                // shouldn't be modifying the arraylist while we're still enumerating through 
                // it.  So, first we create a new list of all the properties we want to remove,
                // and then we later go through and actually remove them.
                ArrayList propertiesToRemove = new ArrayList();

                // Search for all properties in the arraylist that have the given property
                // name.
                foreach (BuildProperty property in this)
                {
                    if (0 == String.Compare(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        // Add the property to our list of things to remove.
                        propertiesToRemove.Add(property);
                    }
                }

                // Go remove all the properties we found with the given name.
                foreach (BuildProperty propertyToRemove in propertiesToRemove)
                {
                    this.RemoveProperty(propertyToRemove);
                }
            }
            else
            {
                MustBeVirtual("NeedVirtualPropertyGroup");

                // We only need to remove the BuildProperty object with the given name from 
                // the Hashtable.  There can be only one.
                this.propertyTableByName.Remove(propertyName);

                // if the property was overridden by an output property, we also want to remove the original
                if (propertiesOverriddenByOutputProperties != null)
                {
                    propertiesOverriddenByOutputProperties.Remove(propertyName);
                }
            }

            this.MarkPropertyGroupAsDirty();
        }

        /// <summary>
        /// Make sure that this property group doesn't contain any reserved properties.
        /// </summary>
        internal void EnsureNoReservedProperties()
        {
            foreach (BuildProperty property in this.propertyList)
            {
                // Make sure this property doesn't override a reserved property
                ProjectErrorUtilities.VerifyThrowInvalidProject(this.ParentProject.ReservedProperties[property.Name] == null,
                    property.PropertyElement, "CannotModifyReservedProperty", property.Name);

            }
        }

        /// <summary>
        /// Removes all output properties, and restores the non-output properties that were overridden.
        /// Requires property group to be virtual.
        /// </summary>
        internal void RevertAllOutputProperties()
        {
            MustBeVirtual("NeedVirtualPropertyGroup");

            if (propertiesOverriddenByOutputProperties != null)
            {
                foreach (DictionaryEntry propertyEntry in propertiesOverriddenByOutputProperties)
                {
                    propertyTableByName.Remove(propertyEntry.Key);

                    if (propertyEntry.Value != null)
                    {
                        propertyTableByName.Add(propertyEntry.Key, propertyEntry.Value);
                    }
                }

                propertiesOverriddenByOutputProperties = null;
            }

            MarkPropertyGroupAsDirty();
        }

        /// <summary>
        /// Imports all the properties from another BuildPropertyGroup into this one.
        /// Any existing properties with the same name are overridden by the new properties.
        /// Requires property group to be virtual.
        /// </summary>
        internal void ImportProperties
        (
            BuildPropertyGroup sourceProperties
        )
        {
            MustBeVirtual("NeedVirtualPropertyGroup");

            // Loop through all the properties in the source BuildPropertyGroup, and add them
            // to this BuildPropertyGroup.
            foreach (DictionaryEntry propertyEntry in sourceProperties.propertyTableByName)
            {
                // We want a SetProperty here, because we want to override any existing
                // properties with the same name.
                SetProperty((BuildProperty)propertyEntry.Value);
            }
        }

        /// <summary>
        /// Helper for the Clear methods
        /// </summary>
        internal void ClearHelper(bool clearImportedPropertyGroup)
        {
            // If this group is backed by XML, clear all attributes and 
            // children out unless it's an imported group, in which case we don't want to modify the XML
            if (IsPersisted && !clearImportedPropertyGroup)
            {
                MustBePersisted("NeedPersistedPropertyGroup", XMakeElements.propertyGroup);

                // We don't allow any modifications to the XML of any of the imported
                // project files ... only the main project file.
                error.VerifyThrowInvalidOperation(!this.importedFromAnotherProject || clearImportedPropertyGroup,
                    "CannotModifyImportedProjects");

                // Remove all of the property elements from wherever they may be.
                foreach (BuildProperty propertyToRemove in this.propertyList)
                {
                    // Find the property XML element.
                    XmlElement propertyElement = propertyToRemove.PropertyElement;

                    MustBelongToPropertyGroup(propertyElement);

                    // Remove the property element.
                    propertyElement.ParentNode.RemoveChild(propertyElement);

                    propertyToRemove.ParentPersistedPropertyGroup = null;
                }

                MustBePersisted("NeedPersistedPropertyGroup", XMakeElements.propertyGroup);
            }

            this.conditionAttribute = null;

            // Clear the contents of the hash table, if one exists.
            if (this.propertyTableByName != null)
            {
                this.propertyTableByName.Clear();
            }

            // clear out saved properties
            propertiesOverriddenByOutputProperties = null;

            // Clear the contents of the arraylist, if one exists.
            if (this.propertyList != null)
            {
                this.propertyList.Clear();
            }

            this.MarkPropertyGroupAsDirty();
        }

        /// <summary>
        /// Removes all properties and conditions from this BuildPropertyGroup.
        /// </summary>
        /// <owner>RGoel</owner>
        internal void ClearImportedPropertyGroup
            (
            )
        {
            ClearHelper(true /* allow imported */);
        }

        /// <summary>
        /// Removes all properties and conditions from this BuildPropertyGroup.
        /// </summary>
        /// <owner>RGoel</owner>
        public void Clear
            (
            )
        {
            ClearHelper(false /* disallow imported */);
        }

        /// <summary>
        /// Marks the parent project as dirty.
        /// </summary>
        /// <owner>RGoel</owner>
        internal void MarkPropertyGroupAsDirty
            (
            )
        {
            if (this.ParentProject != null)
            {
                if (this.IsPersisted && !this.IsImported)
                {
                    // This is a change to the contents of the main project file.
                    this.ParentProject.MarkProjectAsDirty();
                }
                else
                {
                    // This is not a change to the contents of the project file, however
                    // this change does require a re-evaluation of the project.  For 
                    // example, if a global property changes....
                    this.ParentProject.MarkProjectAsDirtyForReevaluation();
                }
            };
        }

        /// <summary>
        /// This method grabs all the environment variables and sets them as
        /// properties. This method can be invoked multiple times if there is
        /// reason to believe the environment has changed. It will update all
        /// the previously gathered variables, and set new ones. This method
        /// will not, however, unset previously set variables.
        /// Requires property group to be virtual.
        /// 
        /// NOTE: this method does not allow environment variables to override
        /// previously set properties of type "GlobalProperty" or "ReservedProperty"
        /// </summary>
        internal void GatherEnvironmentVariables()
        {
            MustBeVirtual("NeedVirtualPropertyGroup");

            SetExtensionsPathProperties();

            IDictionary environmentVariablesBag = Environment.GetEnvironmentVariables();
            if (environmentVariablesBag != null)
            {
                foreach (DictionaryEntry environmentVariable in environmentVariablesBag)
                {
                    // We're going to just skip environment variables that contain names
                    // with characters we can't handle. There's no logger registered yet
                    // when this method is called, so we can't really log anything.
                    string environmentVariableName = environmentVariable.Key.ToString();

                    if (XmlUtilities.IsValidElementName(environmentVariableName))
                    {
                        this.SetProperty(new BuildProperty(environmentVariableName,
                                environmentVariable.Value.ToString(), PropertyType.EnvironmentProperty));
                    }
                    else
                    {
                        // The name was invalid, so we just didn't add the environment variable.
                        // That's fine, continue for the next one.
                    }
                }
            }
        }

        /// <summary>
        /// Set the special "MSBuildExtensionsPath" and "MSBuildExtensionsPath32" properties.
        /// </summary>
        private void SetExtensionsPathProperties()
        {
            // We set the MSBuildExtensionsPath variables here because we don't want to make them official 
            // reserved properties; we need the ability for people to override our default in their 
            // environment or as a global property.  

            // "MSBuildExtensionsPath32". This points to whatever the value of "Program Files (x86)" environment variable is;
            // but on a 32 bit box this isn't set, and we should use "Program Files" instead.
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    
            // Similarly for "MSBuildExtensionsPath32". This points to whatever the value of "Program Files (x86)" environment variable is;
            // but on a 32 bit box this isn't set, and we should use "Program Files" instead.
            string programFiles32 = Environment.GetEnvironmentVariable(Constants.programFilesx86);
            if (String.IsNullOrEmpty(programFiles32))
            {
                // 32 bit box
                programFiles32 = programFiles;
            }

            string extensionsPath32 = Path.Combine(programFiles32, ReservedPropertyNames.extensionsPathSuffix);
            SetProperty(new BuildProperty(ReservedPropertyNames.extensionsPath32, extensionsPath32, PropertyType.EnvironmentProperty));

            // MSBuildExtensionsPath:  The way this used to work is that it would point to "Program Files\MSBuild" on both 
            // 32-bit and 64-bit machines.  We have a switch to continue using that behavior; however the default is now for
            // MSBuildExtensionsPath to always point to the same location as MSBuildExtensionsPath32. 

            bool useLegacyMSBuildExtensionsPathBehavior = !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSBUILDLEGACYEXTENSIONSPATH"));

            string extensionsPath; 
            if (useLegacyMSBuildExtensionsPathBehavior)
            {
                extensionsPath = Path.Combine(programFiles, ReservedPropertyNames.extensionsPathSuffix);
            }
            else
            {
                extensionsPath = extensionsPath32;
            }

            SetProperty(new BuildProperty(ReservedPropertyNames.extensionsPath, extensionsPath, PropertyType.EnvironmentProperty));
        }

        /// <summary>
        /// This method does a comparison of the actual contents of two property bags
        /// and returns True if they are equal, else False.  Equality means that 
        /// the two collections contain the same set of property names (case insensitive)
        /// with the same values (case sensitive).
        /// Requires property group to be virtual.
        /// </summary>
        /// <param name="compareToPropertyGroup"></param>
        /// <owner>RGoel</owner>
        /// <returns>true if the two property bags are equivalent, and false otherwise.</returns>
        internal bool IsEquivalent
        (
            BuildPropertyGroup compareToPropertyGroup
        )
        {
            ErrorUtilities.VerifyThrow(compareToPropertyGroup != null, "Received a null propertyBag!");

            // IsEquivalent is only supported for virtual PropertyGroups.
            this.MustBeVirtual("NeedVirtualPropertyGroup");
            compareToPropertyGroup.MustBeVirtual("NeedVirtualPropertyGroup");

            // Reference equality is easy
            if (this == compareToPropertyGroup)
            {
                return true;
            }

            // First check if the sizes of the two bags match.  If they don't,
            // we don't need to do anything further.
            bool isEqual = true;
            if (this.Count == compareToPropertyGroup.Count)
            {
                // If both bags do have the same number of elements, it should
                // be sufficient to check if one bag contains all of the 
                // elements in the other.
                foreach (DictionaryEntry entry in this.propertyTableByName)
                {
                    BuildProperty leftProperty = (BuildProperty)entry.Value;

                    ErrorUtilities.VerifyThrow(leftProperty != null, "How can we have a null entry in the hash table?");

                    BuildProperty rightProperty = compareToPropertyGroup[(string)entry.Key];

                    if (!leftProperty.IsEquivalent(rightProperty))
                    {
                        isEqual = false;
                        break;
                    }
                }
            }
            else
            {
                // Sizes are unequal
                isEqual = false;
            }

            return isEqual;
        }

        /// <summary>
        /// Returns a boolean that indicates whether this is a virtual property
        /// group.
        /// </summary>
        private bool IsVirtual
        {
            get { return !IsPersisted; }
        }

        /// <summary>
        /// Call this method to verify that this property group is a well-formed
        /// virtual property group.
        /// Requires property group to be virtual.
        /// PERF WARNING: this method is called a lot because virtual PropertyGroups
        /// are used extensively -- keep this method fast and cheap
        /// </summary>
        /// <param name="errorResourceName"></param>
        /// <owner>JomoF</owner>
        private void MustBeVirtual
        (
            string errorResourceName
        )
        {
            error.VerifyThrowInvalidOperation(IsVirtual, errorResourceName, XMakeElements.propertyGroup);

            // If this is a virtual BuildPropertyGroup (not a <PropertyGroup> element), then 
            // we should not have an ArrayList of BuildProperty objects ... we should only have 
            // the hash table.
            error.VerifyThrow(this.propertyList == null,
                "ArrayList of BuildProperty objects not expected for a virtual BuildPropertyGroup.");

            error.VerifyThrow(this.propertyTableByName != null,
                "HashTable of BuildProperty objects expected for a virtual BuildPropertyGroup.");
        }

        /// <summary>
        /// Returns whether this is a persisted group.
        /// </summary>
        /// <returns></returns>
        private bool IsPersisted
        {
            get { return (this.propertyGroupElement != null); }
        }

        /// <summary>
        /// Verifies group is persisted.
        /// </summary>
        /// <param name="errorResourceName"></param>
        /// <param name="args"></param>
        /// <owner>JomoF</owner>
        private void MustBePersisted
        (
            string errorResourceName,
            string arg
        )
        {
            error.VerifyThrowInvalidOperation(IsPersisted, errorResourceName, arg);

            // If this is a persisted element, then we should have an
            // ArrayList of BuildProperty objects, but not a hash table.
            error.VerifyThrow(this.propertyList != null, 
                "ArrayList of BuildProperty objects expected for this BuildPropertyGroup.");
            error.VerifyThrow(this.propertyTableByName == null, 
                "HashTable of BuildProperty objects not expected for this BuildPropertyGroup.");
            error.VerifyThrow(this.ownerDocument != null, 
                "There must be an owner document. It should have been set in the constructor.");
        }

        /// <summary>
        /// Verifies the XmlElement is a child node of the propertyGroupElement backing this BuildPropertyGroup.
        /// </summary>
        /// <param name="propertyElement"></param>
        private void MustBelongToPropertyGroup
        (
            XmlElement propertyElement
        )
        {
            error.VerifyThrowInvalidOperation(propertyElement != null, 
                "PropertyDoesNotBelongToPropertyGroup");
            error.VerifyThrowInvalidOperation(propertyElement.ParentNode == this.propertyGroupElement,
                "PropertyDoesNotBelongToPropertyGroup");
        }

        /// <summary>
        /// Evaluates condition on property group, and if true, evaluates
        /// on each contained property.  If that's true as well, adds property
        /// to evaluatedPropertyBag.
        /// </summary>
        /// <owner>DavidLe</owner>
        /// <param name="evaluatedPropertyBag"></param>
        /// <param name="conditionedPropertiesTable"></param>
        /// <param name="pass"></param>
        internal void Evaluate
            (
            BuildPropertyGroup evaluatedPropertyBag,
            Hashtable conditionedPropertiesTable,
            ProcessingPass pass
            )
        {
            ErrorUtilities.VerifyThrow(pass == ProcessingPass.Pass1, "Pass should be Pass1 for PropertyGroups.");

            Expander expander = new Expander(evaluatedPropertyBag);

            if (!Utilities.EvaluateCondition(this.Condition, this.ConditionAttribute,
                expander, conditionedPropertiesTable, ParserOptions.AllowProperties,
                ParentProject.ParentEngine.LoggingServices, ParentProject.ProjectBuildEventContext))
            {
                return;
            }

            // Add all the properties to our project-level property bag.
            foreach (BuildProperty currentProperty in this.propertyList)
            {
                if (!Utilities.EvaluateCondition(currentProperty.Condition, currentProperty.ConditionAttribute,
                    expander, conditionedPropertiesTable, ParserOptions.AllowProperties,
                    ParentProject.ParentEngine.LoggingServices, parentProject.ProjectBuildEventContext))
                {
                    continue;
                }

                BuildProperty newProperty = currentProperty.Clone(false);
                newProperty.Evaluate(expander);
                evaluatedPropertyBag.SetProperty(newProperty);
            }
        }
        #endregion
    }
}
