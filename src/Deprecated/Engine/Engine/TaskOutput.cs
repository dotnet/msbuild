// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml;

using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class encapsulates the data needed to specify outputs from a task.
    /// </summary>
    /// <owner>SumedhK</owner>
    internal sealed class TaskOutput
    {
        /// <summary>
        /// Default constructor not supported.
        /// </summary>
        /// <owner>SumedhK</owner>
        private TaskOutput()
        {
            // do nothing
        }

        /// <summary>
        /// This constructor allows all output data to be initialized.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="node">The XML element for the Output tag.</param>
        internal TaskOutput(XmlElement node)
        {
            ErrorUtilities.VerifyThrow(node != null, "Need the XML for the <Output> tag.");

            ProjectXmlUtilities.VerifyThrowProjectNoChildElements(node);

            int requiredData = 0;
            string taskName = node.ParentNode.Name;

            foreach (XmlAttribute outputAttribute in node.Attributes)
            {
                switch (outputAttribute.Name)
                {
                    case XMakeAttributes.taskParameter:
                        ProjectErrorUtilities.VerifyThrowInvalidProject(outputAttribute.Value.Length > 0, outputAttribute,
                            "InvalidAttributeValue", outputAttribute.Value, outputAttribute.Name, XMakeElements.output);
                        ProjectErrorUtilities.VerifyThrowInvalidProject(!XMakeAttributes.IsSpecialTaskAttribute(outputAttribute.Value) && !XMakeAttributes.IsBadlyCasedSpecialTaskAttribute(outputAttribute.Value), outputAttribute,
                            "BadlyCasedSpecialTaskAttribute", outputAttribute.Value, taskName, taskName);
                        this.taskParameterAttribute = outputAttribute;
                        break;

                    case XMakeAttributes.itemName:
                        ProjectErrorUtilities.VerifyThrowInvalidProject(outputAttribute.Value.Length > 0, outputAttribute,
                            "InvalidAttributeValue", outputAttribute.Value, outputAttribute.Name, XMakeElements.output);
                        this.itemNameAttribute = outputAttribute;
                        requiredData++;
                        break;

                    case XMakeAttributes.propertyName:
                        ProjectErrorUtilities.VerifyThrowInvalidProject(outputAttribute.Value.Length > 0, outputAttribute,
                            "InvalidAttributeValue", outputAttribute.Value, outputAttribute.Name, XMakeElements.output);
                        ProjectErrorUtilities.VerifyThrowInvalidProject(!ReservedPropertyNames.IsReservedProperty(outputAttribute.Value), node,
                            "CannotModifyReservedProperty", outputAttribute.Value);
                        this.propertyNameAttribute = outputAttribute;
                        requiredData++;
                        break;

                    case XMakeAttributes.condition:
                        this.conditionAttribute = outputAttribute;
                        break;

                    default:
                        ProjectXmlUtilities.ThrowProjectInvalidAttribute(outputAttribute);
                        break;
                }
            }

            /* NOTE:
                *  TaskParameter must be specified
                *  either ItemName or PropertyName must be specified
                *  if ItemName is specified, then PropertyName cannot be specified
                *  if PropertyName is specified, then ItemName cannot be specified
                *  only Condition is truly optional
                */
            ProjectErrorUtilities.VerifyThrowInvalidProject((this.taskParameterAttribute != null) && (requiredData == 1), 
                node, "InvalidTaskOutputSpecification", taskName);
        }

        /// <summary>
        /// Indicates if the output is an item vector.
        /// </summary>
        /// <owner>SumedhK</owner>
        internal bool IsItemVector
        {
            get
            {
                return this.itemNameAttribute != null;
            }
        }

        /// <summary>
        /// Indicates if the output is a property.
        /// </summary>
        /// <owner>SumedhK</owner>
        internal bool IsProperty
        {
            get
            {
                return this.propertyNameAttribute != null;
            }
        }

        /// <summary>
        /// The task parameter bound to this output.
        /// </summary>
        /// <owner>SumedhK</owner>
        internal XmlAttribute TaskParameterAttribute
        {
            get
            {
                // This will never return null.  The constructor ensures this.
                return this.taskParameterAttribute;
            }
        }

        /// <summary>
        /// The item type, if the output is an item vector.
        /// </summary>
        /// <remarks>If PropertyName is already set, this property cannot be set.</remarks>
        /// <owner>SumedhK</owner>
        internal XmlAttribute ItemNameAttribute
        {
            get
            {
                return this.itemNameAttribute;
            }
        }

        /// <summary>
        /// The property name, if the output is a property.
        /// </summary>
        /// <remarks>If ItemName is already set, this property cannot be set.</remarks>
        /// <owner>SumedhK</owner>
        internal XmlAttribute PropertyNameAttribute
        {
            get
            {
                return this.propertyNameAttribute;
            }
        }

        /// <summary>
        /// The condition on the output.
        /// </summary>
        /// <owner>SumedhK</owner>
        internal XmlAttribute ConditionAttribute
        {
            get
            {
                return this.conditionAttribute;
            }
        }

        // the task parameter bound to this output
        private XmlAttribute taskParameterAttribute;
        // the item type, if the output is an item vector
        private XmlAttribute itemNameAttribute;
        // the property name, if the output is a property
        private XmlAttribute propertyNameAttribute;
        // the condition that makes this output usable (can be null)
        private XmlAttribute conditionAttribute;
    }
}
