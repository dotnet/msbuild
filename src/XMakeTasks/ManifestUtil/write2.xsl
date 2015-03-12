<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns="urn:schemas-microsoft-com:asm.v1"
    xmlns:asmv1="urn:schemas-microsoft-com:asm.v1"
    xmlns:asmv2="urn:schemas-microsoft-com:asm.v2"
    xmlns:asmv3="urn:schemas-microsoft-com:asm.v3"
    xmlns:dsig="http://www.w3.org/2000/09/xmldsig#"
    xmlns:co.v1="urn:schemas-microsoft-com:clickonce.v1"
    xmlns:co.v2="urn:schemas-microsoft-com:clickonce.v2"
    version="1.0">
    
<xsl:output method="xml" encoding="utf-8" indent="yes"/>
<xsl:strip-space elements="*"/>

<xsl:param name="trust-file"/>

<xsl:variable name="trust" select="document($trust-file)"/>
    
<xsl:template match="AssemblyManifest">
    <assembly
        xmlns="urn:schemas-microsoft-com:asm.v1"
        xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
        xsi:schemaLocation="urn:schemas-microsoft-com:asm.v1 assembly.adaptive.xsd"
        manifestVersion="1.0"
        >
        <xsl:apply-templates select="AssemblyIdentity" mode="v1"/>
        <xsl:call-template name="assembly-manifest"/>
        <xsl:apply-templates select="AssemblyReferences/AssemblyReference"/>
        <xsl:apply-templates select="FileReferences/FileReference"/>
        <xsl:apply-templates select="ExternalProxyStubs/ProxyStub" mode="external"/>
    </assembly>
</xsl:template>

<xsl:template match="ApplicationManifest[@IsClickOnceManifest='false']">
    <assembly
        xmlns="urn:schemas-microsoft-com:asm.v1"
        xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
        xsi:schemaLocation="urn:schemas-microsoft-com:asm.v1 assembly.adaptive.xsd"
        manifestVersion="1.0"
        >
      <xsl:apply-templates select="AssemblyIdentity" mode="v1"/>
      <xsl:call-template name="assembly-manifest"/>
      <!-- Application Trust -->
      <xsl:if test="$trust">
        <xsl:copy-of select="$trust"/>
      </xsl:if>
      <xsl:apply-templates select="AssemblyReferences/AssemblyReference"/>
      <xsl:apply-templates select="FileReferences/FileReference"/>
      <xsl:apply-templates select="ExternalProxyStubs/ProxyStub" mode="external"/>
    </assembly>
</xsl:template>

<xsl:template match="ApplicationManifest[@IsClickOnceManifest='true']">
    <asmv1:assembly
        xmlns="urn:schemas-microsoft-com:asm.v2"
        xmlns:asmv1="urn:schemas-microsoft-com:asm.v1"
        xmlns:asmv2="urn:schemas-microsoft-com:asm.v2"
        xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
        xmlns:co.v1="urn:schemas-microsoft-com:clickonce.v1"
        xsi:schemaLocation="urn:schemas-microsoft-com:asm.v1 assembly.adaptive.xsd"
        manifestVersion="1.0"
        >
        <xsl:apply-templates select="AssemblyIdentity" mode="winxp-hack"/>
        <xsl:call-template name="application-manifest"/>
        <xsl:apply-templates select="AssemblyReferences/AssemblyReference"/>
      <xsl:apply-templates select="FileReferences/FileReference"/>
      <xsl:apply-templates select="FileAssociations/FileAssociation"/>
      <xsl:apply-templates select="ExternalProxyStubs/ProxyStub" mode="external"/>
    </asmv1:assembly>
</xsl:template>

<xsl:template match="DeployManifest">
    <asmv1:assembly
        xmlns="urn:schemas-microsoft-com:asm.v2"
        xmlns:asmv1="urn:schemas-microsoft-com:asm.v1"
        xmlns:asmv2="urn:schemas-microsoft-com:asm.v2"
        xmlns:xrml="urn:mpeg:mpeg21:2003:01-REL-R-NS"
        xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
        xsi:schemaLocation="urn:schemas-microsoft-com:asm.v1 assembly.adaptive.xsd"
        manifestVersion="1.0"
        >
        <xsl:apply-templates select="AssemblyIdentity" mode="v1"/>
        <xsl:call-template name="deploy-manifest"/>
        <xsl:if test="count(CompatibleFrameworks)>0">
          <compatibleFrameworks xmlns="urn:schemas-microsoft-com:clickonce.v2">
            <xsl:apply-templates select="CompatibleFrameworks/CompatibleFramework"/>
          </compatibleFrameworks>
        </xsl:if>
        <xsl:for-each select="AssemblyReferences/AssemblyReference"> <!-- expecting only one match -->
            <xsl:call-template name="application-reference"/>
        </xsl:for-each>
        <xsl:apply-templates select="FileReferences/FileReference"/>
    </asmv1:assembly>
</xsl:template>


<xsl:attribute-set name="identity-attributes">
    <xsl:attribute name="name"><xsl:value-of select="@Name"/></xsl:attribute>
    <xsl:attribute name="version"><xsl:value-of select="@Version"/></xsl:attribute>
    <xsl:attribute name="publicKeyToken"><xsl:value-of select="@PublicKeyToken"/></xsl:attribute>
    <xsl:attribute name="language"><xsl:value-of select="@Culture"/></xsl:attribute>
    <xsl:attribute name="processorArchitecture"><xsl:value-of select="@ProcessorArchitecture"/></xsl:attribute>
    <xsl:attribute name="type"><xsl:value-of select="@Type"/></xsl:attribute>
</xsl:attribute-set>

<!--
WinXP & Win2K3 requires this element to be declared with the asmv1 qualifier.
Setting the default namespace will not work!
This is a quirk of the native loader on downlevel OS.
-->
<xsl:template match="AssemblyIdentity" mode="winxp-hack">
    <xsl:element name="asmv1:assemblyIdentity" use-attribute-sets="identity-attributes"/>
</xsl:template>

<xsl:template match="AssemblyIdentity" mode="v1">
    <xsl:element name="assemblyIdentity" use-attribute-sets="identity-attributes" namespace="urn:schemas-microsoft-com:asm.v1"/>
</xsl:template>

<xsl:template match="AssemblyIdentity" mode="v2">
    <xsl:element name="assemblyIdentity" use-attribute-sets="identity-attributes" namespace="urn:schemas-microsoft-com:asm.v2"/>
</xsl:template>

<xsl:template match="EntryPointIdentity">
    <xsl:element name="assemblyIdentity" use-attribute-sets="identity-attributes" namespace="urn:schemas-microsoft-com:asm.v2"/>
</xsl:template>


<xsl:template name="assembly-manifest">
    <xsl:if test="string-length(@Description)>0">
        <description>
            <xsl:value-of select="@Description"/>
        </description>
    </xsl:if>
</xsl:template>

<xsl:template name="application-manifest">
    <!-- Description -->
    <xsl:if test="string-length(@Description)>0 or string-length(@IconFile)>0 or string-length(@Publisher)>0 or string-length(@Product)>0 or string-length(@SupportUrl)>0 or string-length(@SuiteName)>0 or string-length(@ErrorReportUrl)>0">
        <description asmv2:iconFile="{@IconFile}"
                     asmv2:publisher="{@Publisher}"
                     co.v1:suiteName="{@SuiteName}"
                     asmv2:product="{@Product}"
                     asmv2:supportUrl="{@SupportUrl}"
                     co.v1:errorReportUrl="{@ErrorReportUrl}">
            <xsl:value-of select="@Description"/>
        </description>
    </xsl:if>
    <application xmlns="urn:schemas-microsoft-com:asm.v2"/>
    <!-- Application Startup -->
    <xsl:choose>
        <xsl:when test="EntryPointIdentity">
            <entryPoint xmlns="urn:schemas-microsoft-com:asm.v2">
            <xsl:apply-templates select="EntryPointIdentity"/>
                <commandLine
                    file="{@EntryPointPath}"
                    parameters="{@EntryPointParameters}"
                    />
                <xsl:if test="@HostInBrowser='true'">
                    <hostInBrowser xmlns="urn:schemas-microsoft-com:asm.v3"/>
                </xsl:if>            
            </entryPoint>
        </xsl:when>
        <xsl:when test="@HostInBrowser='true'">
            <entryPoint xmlns="urn:schemas-microsoft-com:asm.v2">
                <xsl:if test="@HostInBrowser='true'">
                    <hostInBrowser xmlns="urn:schemas-microsoft-com:asm.v3"/>
                </xsl:if>            
            </entryPoint>
        </xsl:when>
        <xsl:otherwise>
            <entryPoint xmlns="urn:schemas-microsoft-com:asm.v2">
                <co.v1:customHostSpecified />
            </entryPoint>
        </xsl:otherwise>            
    </xsl:choose>
    <!-- Manifest Trust -->
    <xsl:if test="@UseApplicationTrust='true'">
        <co.v1:useManifestForTrust />
    </xsl:if>
  <!-- Application Trust -->
    <xsl:if test="$trust">
        <xsl:copy-of select="$trust"/>
    </xsl:if>
    <!-- OS dependency -->
    <xsl:if test="string-length(@OSMajor)>0">
        <dependency xmlns="urn:schemas-microsoft-com:asm.v2">
            <dependentOS supportUrl="{@OSSupportUrl}" description="{@OSDescription}">
                <osVersionInfo>
                    <os majorVersion="{@OSMajor}" minorVersion="{@OSMinor}" buildNumber="{@OSBuild}" servicePackMajor="{@OSRevision}"/>
                </osVersionInfo>
            </dependentOS>
        </dependency>
    </xsl:if>
</xsl:template>
    
<xsl:template name="deploy-manifest">
    <!-- Description -->
    <description xmlns="urn:schemas-microsoft-com:asm.v1"
        asmv2:publisher="{@Publisher}"
        co.v1:suiteName="{@SuiteName}"
        asmv2:product="{@Product}"
        asmv2:supportUrl="{@SupportUrl}"
        co.v1:errorReportUrl="{@ErrorReportUrl}"
        >
        <xsl:value-of select="@Description"/>
    </description>
    <!-- Application Updates -->
    <deployment xmlns="urn:schemas-microsoft-com:asm.v2"
        install="{@Install}"
        disallowUrlActivation="{@DisallowUrlActivation}"
        mapFileExtensions="{@MapFileExtensions}"
        minimumRequiredVersion="{@MinimumRequiredVersion}"
        trustURLParameters="{@TrustUrlParameters}"
        co.v1:createDesktopShortcut="{@CreateDesktopShortcut}"
        >
        <xsl:if test="@Install='true' and @UpdateEnabled='true'">
            <subscription>
                <update>
                    <xsl:choose>
                        <xsl:when test="@UpdateMode='Foreground'">
                            <beforeApplicationStartup/>
                        </xsl:when>
                        <xsl:otherwise>
                            <expiration maximumAge="{@UpdateInterval}" unit="{@UpdateUnit}"/>
                        </xsl:otherwise>
                    </xsl:choose>
                </update>
            </subscription>
        </xsl:if>
        <xsl:if test="string-length(@DeploymentUrl)>0">
            <deploymentProvider codebase="{@DeploymentUrl}"/>
        </xsl:if>
   </deployment>
</xsl:template>

<!-- This is the <dependency> element for referencing the ApplicationManifest (.exe.manifest) from the DeployManifest (.application) -->
<xsl:template name="application-reference">
    <dependency xmlns="urn:schemas-microsoft-com:asm.v2">
        <dependentAssembly
            dependencyType="install"
            codebase="{@Path}"
            size="{@Size}"
            >
            <xsl:apply-templates select="AssemblyIdentity" mode="v2"/>
            <xsl:call-template name="hash">
                <xsl:with-param name="value"><xsl:value-of select="@Hash"/></xsl:with-param>
            </xsl:call-template>
        </dependentAssembly>
    </dependency>
</xsl:template>

<!-- Must put native <dependency> elements in the asmv1 namespace -->    
<xsl:template match="AssemblyReference[@IsNative='true' and @IsPrerequisite='false']">
    <dependency optional="{@IsOptional}" xmlns="urn:schemas-microsoft-com:asm.v1">
        <dependentAssembly
        asmv2:dependencyType="install"
        asmv2:codebase="{@Path}"
        asmv2:size="{@Size}"
        asmv2:group="{@Group}"
        >
        <xsl:apply-templates select="AssemblyIdentity" mode="v1"/>
        <xsl:call-template name="hash">
            <xsl:with-param name="value"><xsl:value-of select="@Hash"/></xsl:with-param>
        </xsl:call-template>
        </dependentAssembly>
    </dependency>
</xsl:template>
<xsl:template match="AssemblyReference[@IsNative='true' and @IsPrerequisite='true']">
    <dependency xmlns="urn:schemas-microsoft-com:asm.v1">
            <dependentAssembly
            asmv2:dependencyType="preRequisite"
                >
                <xsl:apply-templates select="AssemblyIdentity" mode="v1"/>
            </dependentAssembly>
    </dependency>
</xsl:template>

<!-- Other <dependency> elements go in the asmv2 namespace -->    
<xsl:template match="AssemblyReference[@IsNative='false' and @IsPrerequisite='false']">
    <dependency optional="{@IsOptional}" xmlns="urn:schemas-microsoft-com:asm.v2">
        <dependentAssembly
            dependencyType="install"
            allowDelayedBinding="true"
            codebase="{@Path}"
            size="{@Size}"
            group="{@Group}"
            >
            <xsl:apply-templates select="AssemblyIdentity" mode="v2"/>
            <xsl:call-template name="hash">
                <xsl:with-param name="value"><xsl:value-of select="@Hash"/></xsl:with-param>
            </xsl:call-template>
        </dependentAssembly>
    </dependency>
</xsl:template>

<xsl:template match="AssemblyReference[@IsNative='false' and @IsPrerequisite='true']">
    <dependency xmlns="urn:schemas-microsoft-com:asm.v2">
        <dependentAssembly
            dependencyType="preRequisite"
            allowDelayedBinding="true"
            >
            <xsl:apply-templates select="AssemblyIdentity" mode="v2"/>
        </dependentAssembly>
    </dependency>
</xsl:template>

<xsl:template match="FileReference[not(ComClasses or WindowClasses)]">
    <file xmlns="urn:schemas-microsoft-com:asm.v2"
        name="{@Path}"
        size="{@Size}"
        group="{@Group}"
        optional="{@IsOptional}"
        writeableType="{@WriteableType}"
        >
        <xsl:call-template name="hash">
            <xsl:with-param name="value"><xsl:value-of select="@Hash"/></xsl:with-param>
        </xsl:call-template>
    </file>
</xsl:template>

<xsl:template match="FileReference[ComClasses or WindowClasses]">
    <file xmlns="urn:schemas-microsoft-com:asm.v1"
        name="{@Path}"
        asmv2:size="{@Size}"
        asmv2:group="{@Group}"
        asmv2:optional="{@IsOptional}"
        asmv2:writeableType="{@WriteableType}"
        >
        <xsl:call-template name="hash">
            <xsl:with-param name="value"><xsl:value-of select="@Hash"/></xsl:with-param>
        </xsl:call-template>
        <xsl:call-template name="isolation"/>
    </file>
</xsl:template>

<xsl:template name="hash">
    <xsl:param name="value"/>
    <xsl:if test="$value and string-length($value)>0">
        <hash xmlns="urn:schemas-microsoft-com:asm.v2">
            <dsig:Transforms>
                <dsig:Transform Algorithm="urn:schemas-microsoft-com:HashTransforms.Identity"/>
            </dsig:Transforms>
            <dsig:DigestMethod Algorithm="http://www.w3.org/2000/09/xmldsig#sha1"/>
            <dsig:DigestValue><xsl:value-of select="$value"/></dsig:DigestValue>
        </hash>
    </xsl:if>
</xsl:template>

<xsl:template name="isolation">
    <xsl:apply-templates select="TypeLibs/TypeLib"/>
    <xsl:apply-templates select="ComClasses/ComClass"/>
    <xsl:apply-templates select="WindowClasses/WindowClass"/>
    <xsl:apply-templates select="ProxyStubs/ProxyStub" mode="internal"/>
</xsl:template>

<xsl:template match="TypeLib">
    <typelib xmlns="urn:schemas-microsoft-com:asm.v1"
        tlbid="{@Tlbid}"
        version="{@Version}"
        helpdir="{@HelpDir}"
        resourceid="{@ResourceId}"
        flags="{@Flags}"/>
</xsl:template>

<xsl:template match="ComClass">
    <comClass xmlns="urn:schemas-microsoft-com:asm.v1"
        clsid="{@Clsid}"
        threadingModel="{@ThreadingModel}"
        tlbid="{@Tlbid}"
        progid="{@Progid}"
        description="{@Description}"/>
</xsl:template>

<xsl:template match="WindowClass">
    <windowClass xmlns="urn:schemas-microsoft-com:asm.v1"
        versioned="{@Versioned}"
        >
        <xsl:value-of select="@Name"/>
    </windowClass>
</xsl:template>

<xsl:template match="ProxyStub" mode="internal">
    <comInterfaceProxyStub xmlns="urn:schemas-microsoft-com:asm.v1"
        name="{@Name}" 
        iid="{@Iid}" 
        numMethods="{@NumMethods}" 
        baseInterface="{@BaseInterface}"
        tlbid="{@Tlbid}"/>        
</xsl:template>

<xsl:template match="ProxyStub" mode="external">
    <comInterfaceExternalProxyStub xmlns="urn:schemas-microsoft-com:asm.v1"
        name="{@Name}" 
        iid="{@Iid}" 
        numMethods="{@NumMethods}" 
        baseInterface="{@BaseInterface}"
        tlbid="{@Tlbid}"/>        
</xsl:template>

<xsl:template match="FileAssociation">
    <fileAssociation xmlns="urn:schemas-microsoft-com:clickonce.v1"
        extension="{@Extension}"
        description="{@Description}"
        progid="{@Progid}"
        defaultIcon="{@DefaultIcon}"/>
</xsl:template>

<xsl:template match="CompatibleFramework">
  <framework xmlns="urn:schemas-microsoft-com:clickonce.v2"
      targetVersion="{@Version}"
      profile="{@Profile}"
      supportedRuntime="{@SupportedRuntime}"/>
</xsl:template>

</xsl:stylesheet>
