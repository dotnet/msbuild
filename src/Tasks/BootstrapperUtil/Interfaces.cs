// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Microsoft.Build.Tasks.Deployment.Bootstrapper
{
    /// <summary>
    /// This interface exposes functionality necessary to build a bootstrapper.
    /// </summary>
    [ComVisible(true)]
    [Guid("1D202366-5EEA-4379-9255-6F8CDB8587C9"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IBootstrapperBuilder
    {
        /// <summary>
        /// Specifies the location of the required bootstrapper files.
        /// </summary>
        /// <value>Path to bootstrapper files.</value>
        [DispId(1)]
        string Path { get; set; }

        /// <summary>
        /// Returns all products available at the current bootstrapper Path
        /// </summary>
        [DispId(4)]
        ProductCollection Products { get; }

        /// <summary>
        /// Generates a bootstrapper based on the specified settings.
        /// </summary>
        /// <param name="settings">The properties used to build this bootstrapper.</param>
        /// <returns>The results of the bootstrapper generation</returns>
        [DispId(5)]
        BuildResults Build(BuildSettings settings);
    }

    /// <summary>
    /// This interface defines the settings for the bootstrapper build operation.
    /// </summary>
    [ComVisible(true)]
    [Guid("87EEBC69-0948-4ce6-A2DE-819162B87CC6"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IBuildSettings
    {
        /// <summary>
        /// The name of the application to be installed after the bootstrapper has installed all required components.  If no application is to be installed, this parameter may be null
        /// </summary>
        [DispId(1)]
        string ApplicationName { get; set; }

        /// <summary>
        /// The file to be installed after the bootstrapper has installed the required components.  It is assumed that this file path is relative to the bootstrapper source path.  If no application is to be installed, this parameter may be null
        /// </summary>
        [DispId(2)]
        string ApplicationFile { get; set; }

        /// <summary>
        /// The expected source location if the bootstrapper is published to a website.  It is expected that the ApplicationFile, if specified, will be published to the location consistent to this value. If ComponentsLocation is Relative, required component files will also be published in a manner consistent with this value.  This value may be null if setup.exe is not to be published to the web
        /// </summary>
        [DispId(3)]
        string ApplicationUrl { get; set; }

        /// <summary>
        /// The location the bootstrapper install time will use for components if ComponentsLocation is "Absolute"
        /// </summary>
        [DispId(4)]
        string ComponentsUrl { get; set; }

        /// <summary>
        /// If true, the bootstrapper components will be copied to the build output directory.  If false, the files will not be copied
        /// </summary>
        [DispId(5)]
        bool CopyComponents { get; set; }

        /// <summary>
        /// The culture identifier for the bootstrapper to be built
        /// </summary>
        [DispId(6)]
        int LCID { get; set; }

        /// <summary>
        /// The culture identifier to use if the LCID identifier is not available
        /// </summary>
        [DispId(7)]
        int FallbackLCID { get; set; }

        /// <summary>
        /// The file location to copy output files to
        /// </summary>
        [DispId(8)]
        string OutputPath { get; set; }

        /// <summary>
        /// The product builders to use for generating the bootstrapper
        /// </summary>
        [DispId(9)]
        ProductBuilderCollection ProductBuilders { get; }

        /// <summary>
        /// True if the bootstrapper will perform XML validation on the component manifests
        /// </summary>
        [DispId(10)]
        bool Validate { get; set; }

        /// <summary>
        /// Specifies the install time location for bootstrapper components
        /// </summary>
        [DispId(11)]
        ComponentsLocation ComponentsLocation { get; set; }

        /// <summary>
        /// Specifies a URL for the Web site containing support information for the bootstrapper
        /// </summary>
        [DispId(12)]
        string SupportUrl { get; set; }

        /// <summary>
        /// A value of true indicates that the application should require elevation to install on Windows Vista.
        /// </summary>
        [DispId(13)]
        bool ApplicationRequiresElevation { get; set; }
    }

    /// <summary>
    /// This interface represents a product in the found by the BootstrapperBuilder in the Path property.
    /// </summary>
    [ComVisible(true)]
    [Guid("9E81BE3D-530F-4a10-8349-5D5947BA59AD"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IProduct
    {
        /// <summary>
        /// The ProductBuilder representation of this Product
        /// </summary>
        [DispId(1)]
        ProductBuilder ProductBuilder { get; }

        /// <summary>
        /// A human-readable name for this product
        /// </summary>
        [DispId(2)]
        string Name { get; }

        /// <summary>
        /// A string specifying the unique identifier of this product
        /// </summary>
        [DispId(3)]
        string ProductCode { get; }

        /// <summary>
        /// All products which this product also installs
        /// </summary>
        [DispId(4)]
        ProductCollection Includes { get; }
    }

    /// <summary>
    /// This interface describes a collection of Product objects. This collection is a closed set that is generated by the BootstrapperBuilder based on the Path property. The client cannot add or remove items from this collection.
    /// </summary>
    [ComVisible(true)]
    [Guid("63F63663-8503-4875-814C-09168E595367"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IProductCollection
    {
        /// <summary>
        /// Gets the number of elements actually contained in the ProductCollection
        /// </summary>
        [DispId(1)]
        int Count { get; }

        /// <summary>
        /// Gets the Product at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the element to get</param>
        /// <returns>The Product at the specified index</returns>
        [DispId(2)]
        Product Item(int index);

        /// <summary>
        /// Gets the product with the specified product code
        /// </summary>
        /// <param name="productCode"></param>
        /// <returns>The product with the given name, null if the spercified product code is not found</returns>
        [DispId(3)]
        Product Product(string productCode);
    }

    /// <summary>
    /// This interface represents a buildable version of a Product.  Used for the BootstrapperBuilder's Build method.
    /// </summary>
    [ComVisible(true)]
    [Guid("0777432F-A60D-48b3-83DB-90326FE8C96E"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IProductBuilder
    {
        /// <summary>
        /// The product corresponding to this builder
        /// </summary>
        [DispId(1)]
        Product Product { get; }
    }

    /// <summary>
    /// This class contains a collection of ProductBuilder objects. Used for the BootstrapperBuilder's Build method.
    /// </summary>
    [ComVisible(true)]
    [Guid("0D593FC0-E3F1-4dad-A674-7EA4D327F79B"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IProductBuilderCollection
    {
        /// <summary>
        /// Adds a builder to the collection
        /// </summary>
        /// <param name="builder">The ProductBuilder to add to the collection</param>
        [DispId(2)]
        void Add(ProductBuilder builder);
    }

    /// <summary>
    /// Represents the results of the build operation of the BootstrapperBuilder.
    /// </summary>
    [ComVisible(true)]
    [Guid("586B842C-D9C7-43b8-84E4-9CFC3AF9F13B"),
    InterfaceTypeAttribute(ComInterfaceType.InterfaceIsDual)]
    public interface IBuildResults
    {
        /// <summary>
        /// Returns true if the bootstrapper build was successful, false otherwise
        /// </summary>
        [DispId(1)]
        bool Succeeded { get; }

        /// <summary>
        /// The file path to the generated primary bootstrapper file
        /// </summary>
        /// <value>Path to setup.exe</value>
        [DispId(2)]
        string KeyFile { get; }

        /// <summary>
        /// File paths to copied component installer files
        /// </summary>
        /// <value>Path to component files</value>
        [DispId(3)]
        string[] ComponentFiles { get; }

        /// <summary>
        /// The build messages generated from a bootstrapper build
        /// </summary>
        [DispId(4)]
        BuildMessage[] Messages { get; }
    }

    /// <summary>
    /// Represents messages that occur during the BootstrapperBuilder's Build operation.
    /// </summary>
    [ComVisible(true)]
    [Guid("E3C981EA-99E6-4f48-8955-1AAFDFB5ACE4"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IBuildMessage
    {
        /// <summary>
        /// This severity of this build message
        /// </summary>
        [DispId(1)]
        BuildMessageSeverity Severity { get; }

        /// <summary>
        /// A text string describing the details of the build message
        /// </summary>
        [DispId(2)]
        string Message { get; }

        /// <summary>
        /// The MSBuild F1-help keyword for the host IDE, or null
        /// </summary>
        [DispId(3)]
        string HelpKeyword { get; }

        /// <summary>
        /// The MSBuild help id for the host IDE
        /// </summary>
        [DispId(4)]
        int HelpId { get; }
    }

    /// <summary>
    /// This enumeration provides three levels of importance for build messages.
    /// </summary>
    [ComVisible(true)]
    [Guid("936D32F9-1A68-4d5e-98EA-044AC9A1AADA")]
    public enum BuildMessageSeverity
    {
        /// <summary>
        /// Indicates that the message corresponds to build information
        /// </summary>
        Info,
        /// <summary>
        /// Indicates that the message corresponds to a build warning
        /// </summary>
        Warning,
        /// <summary>
        /// Indicates that the message corresponds to a build error
        /// </summary>
        Error
    };

    /// <summary>
    /// This enumeration describes the way required components will be published
    /// </summary>
    [ComVisible(true)]
    [Guid("12F49949-7B60-49CD-B6A0-2B5E4A638AAF")]
    public enum ComponentsLocation
    {
        /// <summary>
        /// Products will be found according to the redist vendor's designated URL 
        /// </summary>
        HomeSite,
        /// <summary>
        /// Products will be located relative to generated bootstrapper
        /// </summary>
        Relative,
        /// <summary>
        /// All products will be located at s specific location
        /// </summary>
        Absolute
    };
}
