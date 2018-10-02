<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns="urn:schemas-microsoft-com:asm.v1"
    xmlns:asmv1="urn:schemas-microsoft-com:asm.v1"
    xmlns:asmv2="urn:schemas-microsoft-com:asm.v2"
    xmlns:asmv3="urn:schemas-microsoft-com:asm.v3"     
    xmlns:xrml="urn:mpeg:mpeg21:2003:01-REL-R-NS"
    xmlns:dsig="http://www.w3.org/2000/09/xmldsig#"
    xmlns:co.v1="urn:schemas-microsoft-com:clickonce.v1"
    exclude-result-prefixes="asmv1 asmv2"
    version="1.0">


<xsl:output method="xml" encoding="utf-8" indent="yes"/>
<xsl:strip-space elements="*"/>

<xsl:param name="manifest-type"/>

<!-- DeployManifest Attributes -->
<xsl:attribute-set name="deploy-manifest-attributes">
    <xsl:attribute name="Publisher">
        <xsl:value-of select="asmv1:description/@asmv2:publisher"/>
    </xsl:attribute>
    <xsl:attribute name="Product">
        <xsl:value-of select="asmv1:description/@asmv2:product"/>
    </xsl:attribute>
    <xsl:attribute name="SuiteName">
        <xsl:value-of select="asmv1:description/@co.v1:suiteName"/>
    </xsl:attribute>
    <xsl:attribute name="SupportUrl">
        <xsl:value-of select="asmv1:description/@asmv2:supportUrl"/>
    </xsl:attribute>
    <xsl:attribute name="ErrorReportUrl">
        <xsl:value-of select="asmv1:description/@co.v1:errorReportUrl"/>
    </xsl:attribute>
    <xsl:attribute name="Description">
        <xsl:value-of select="asmv1:description/text()"/>
    </xsl:attribute>
    <xsl:attribute name="MinimumRequiredVersion">
        <xsl:value-of select="asmv2:deployment/@minimumRequiredVersion"/>
    </xsl:attribute>
    <xsl:attribute name="Install">
        <xsl:value-of select="asmv2:deployment/@install"/>
    </xsl:attribute>
    <xsl:attribute name="CreateDesktopShortcut">
        <xsl:value-of select="asmv2:deployment/@co.v1:createDesktopShortcut"/>
    </xsl:attribute>
    <xsl:attribute name="DisallowUrlActivation">
        <xsl:value-of select="asmv2:deployment/@disallowUrlActivation"/>
    </xsl:attribute>
    <xsl:attribute name="MapFileExtensions">
        <xsl:value-of select="asmv2:deployment/@mapFileExtensions"/>
    </xsl:attribute>    
    <xsl:attribute name="TrustUrlParameters">
        <xsl:value-of select="asmv2:deployment/@trustURLParameters"/>
    </xsl:attribute>    
    <xsl:attribute name="UpdateEnabled">
        <xsl:value-of select="boolean(asmv2:deployment/asmv2:subscription/asmv2:update)"/>
    </xsl:attribute>
    <xsl:attribute name="UpdateMode">
        <xsl:choose>
            <xsl:when test="asmv2:deployment/asmv2:subscription/asmv2:update/asmv2:beforeApplicationStartup">
                <xsl:text>Foreground</xsl:text>
            </xsl:when>
            <xsl:otherwise>
                <xsl:text>Background</xsl:text>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:attribute>
    <xsl:attribute name="UpdateInterval">
        <xsl:value-of select="asmv2:deployment/asmv2:subscription/asmv2:update/asmv2:expiration/@maximumAge"/>
    </xsl:attribute>
    <xsl:attribute name="UpdateUnit">
        <xsl:value-of select="asmv2:deployment/asmv2:subscription/asmv2:update/asmv2:expiration/@unit"/>
    </xsl:attribute>
    <xsl:attribute name="DeploymentUrl">
        <xsl:value-of select="asmv2:deployment/asmv2:deploymentProvider/@codebase"/>
    </xsl:attribute>    
</xsl:attribute-set>


<!-- ApplicationManifest Attributes -->
<xsl:attribute-set name="application-manifest-attributes">
    <xsl:attribute name="Description">
        <xsl:value-of select="asmv1:description/text()"/>
    </xsl:attribute>
    <xsl:attribute name="EntryPointParameters">
        <xsl:value-of select="asmv2:entryPoint/asmv2:commandLine/@parameters"/>
    </xsl:attribute>    
    <xsl:attribute name="EntryPointPath">
        <xsl:value-of select="asmv2:entryPoint/asmv2:commandLine/@file"/>
    </xsl:attribute>
    <xsl:attribute name="ErrorReportUrl">
        <xsl:value-of select="asmv1:description/@co.v1:errorReportUrl"/>
    </xsl:attribute>
    <xsl:attribute name="HostInBrowser">
      <xsl:choose>
        <xsl:when test="asmv2:entryPoint/asmv3:hostInBrowser">
            <xsl:text>true</xsl:text>
        </xsl:when>
        <xsl:otherwise>
            <xsl:text>false</xsl:text>
        </xsl:otherwise>
      </xsl:choose>
    </xsl:attribute>    
    <xsl:attribute name="IconFile">
        <xsl:value-of select="asmv1:description/@asmv2:iconFile"/>
    </xsl:attribute>    
    <xsl:attribute name="OSMajor">
        <xsl:value-of select="asmv1:dependency/asmv2:dependentOS/asmv2:osVersionInfo/asmv2:os/@majorVersion|asmv2:dependency/asmv2:dependentOS/asmv2:osVersionInfo/asmv2:os/@majorVersion"/>
    </xsl:attribute>    
    <xsl:attribute name="OSMinor">
        <xsl:value-of select="asmv1:dependency/asmv2:dependentOS/asmv2:osVersionInfo/asmv2:os/@minorVersion|asmv2:dependency/asmv2:dependentOS/asmv2:osVersionInfo/asmv2:os/@minorVersion"/>
    </xsl:attribute>    
    <xsl:attribute name="OSBuild">
        <xsl:value-of select="asmv1:dependency/asmv2:dependentOS/asmv2:osVersionInfo/asmv2:os/@buildNumber|asmv2:dependency/asmv2:dependentOS/asmv2:osVersionInfo/asmv2:os/@buildNumber"/>
    </xsl:attribute>    
    <xsl:attribute name="OSRevision">
        <xsl:value-of select="asmv1:dependency/asmv2:dependentOS/asmv2:osVersionInfo/asmv2:os/@servicePackMajor|asmv2:dependency/asmv2:dependentOS/asmv2:osVersionInfo/asmv2:os/@servicePackMajor"/>
    </xsl:attribute>    
    <xsl:attribute name="OSSupportUrl">
        <xsl:value-of select="asmv1:dependency/asmv2:dependentOS/@supportUrl|asmv2:dependency/asmv2:dependentOS/@supportUrl"/>
    </xsl:attribute>
    <xsl:attribute name="OSDescription">
        <xsl:value-of select="asmv1:dependency/asmv2:dependentOS/@description|asmv2:dependency/asmv2:dependentOS/@description"/>
    </xsl:attribute>
    <xsl:attribute name="Product">
      <xsl:value-of select="asmv1:description/@asmv2:product"/>
    </xsl:attribute>
    <xsl:attribute name="Publisher">
      <xsl:value-of select="asmv1:description/@asmv2:publisher"/>
    </xsl:attribute>
    <xsl:attribute name="SuiteName">
        <xsl:value-of select="asmv1:description/@co.v1:suiteName"/>
    </xsl:attribute>
    <xsl:attribute name="SupportUrl">
        <xsl:value-of select="asmv1:description/@asmv2:supportUrl"/>
    </xsl:attribute>
    <xsl:attribute name="UseApplicationTrust">
      <xsl:choose>
        <xsl:when test="co.v1:useManifestForTrust">
          <xsl:text>true</xsl:text>
        </xsl:when>
        <xsl:otherwise>
          <xsl:text>false</xsl:text>
        </xsl:otherwise>
      </xsl:choose>
    </xsl:attribute>
</xsl:attribute-set>


<!-- AssemblyManifest Attributes -->
<xsl:attribute-set name="assembly-manifest-attributes">
    <xsl:attribute name="Description">
        <xsl:value-of select="asmv1:description/text()"/>
    </xsl:attribute>
</xsl:attribute-set>


<!-- Manifest Type -->
<xsl:template match="asmv1:assembly">
    <xsl:choose>

        <!-- DeployManifest if document has a <deployment> element -->
        <xsl:when test="$manifest-type='DeployManifest' or count(asmv2:deployment)>0">
            <xsl:element name="DeployManifest" use-attribute-sets="deploy-manifest-attributes" namespace="">
                <xsl:call-template name="base-assembly"/>
                <xsl:call-template name="trust-licenses"/>
                <xsl:call-template name="public-key"/>                
            </xsl:element>
        </xsl:when>

        <!-- ApplicationManifest if document has a <application> element -->
        <xsl:when test="$manifest-type='ApplicationManifest' or count(asmv2:application)>0">
            <xsl:element name="ApplicationManifest" use-attribute-sets="application-manifest-attributes"  namespace="">
                <xsl:call-template name="base-assembly"/>
                <xsl:if test="asmv2:entryPoint/asmv2:assemblyIdentity|asmv2:entryPoint/asmv1:assemblyIdentity">
                    <xsl:apply-templates select="asmv2:entryPoint/asmv2:assemblyIdentity|asmv2:entryPoint/asmv1:assemblyIdentity"/>
                </xsl:if>
                <xsl:call-template name="external-proxy-stubs"/>
                <xsl:if test="count(co.v1:fileAssociation)>0">
                  <xsl:element name="FileAssociations" namespace="">
                    <xsl:apply-templates select="co.v1:fileAssociation"/>
                  </xsl:element>
                </xsl:if>
            </xsl:element>
        </xsl:when>

        <!-- AssemblyManifest if document does not have any of the above -->
        <xsl:otherwise>
            <xsl:element name="AssemblyManifest"  use-attribute-sets="assembly-manifest-attributes" namespace="">
                <xsl:call-template name="base-assembly"/>
                <xsl:call-template name="external-proxy-stubs"/>
            </xsl:element>
        </xsl:otherwise>

    </xsl:choose>
</xsl:template>

<xsl:template match="asmv2:entryPoint/asmv2:assemblyIdentity|asmv2:entryPoint/asmv1:assemblyIdentity">
    <xsl:call-template name="assembly-identity">
        <xsl:with-param name="class">EntryPointIdentity</xsl:with-param>
    </xsl:call-template>
</xsl:template>

<xsl:template name="base-assembly">
    <xsl:apply-templates select="asmv1:assemblyIdentity|asmv2:assemblyIdentity"/>
    <xsl:if test="count(asmv1:dependency[asmv1:dependentAssembly]|asmv1:dependency[asmv2:dependentAssembly]|asmv2:dependency[asmv1:dependentAssembly]|asmv2:dependency[asmv2:dependentAssembly])>0">
        <xsl:element name="AssemblyReferences" namespace="">
            <xsl:apply-templates select="asmv1:dependency[asmv1:dependentAssembly]|asmv1:dependency[asmv2:dependentAssembly]|asmv2:dependency[asmv1:dependentAssembly]|asmv2:dependency[asmv2:dependentAssembly]"/>
        </xsl:element>
    </xsl:if>
    <xsl:if test="count(asmv1:file|asmv2:file)>0">
        <xsl:element name="FileReferences" namespace="">
            <xsl:apply-templates select="asmv1:file|asmv2:file"/>
        </xsl:element>
    </xsl:if>
</xsl:template>

<xsl:template name="external-proxy-stubs">
    <xsl:if test="count(asmv1:comInterfaceExternalProxyStub)">
        <xsl:element name="ExternalProxyStubs" namespace="">
            <xsl:apply-templates select="asmv1:comInterfaceExternalProxyStub"/>
        </xsl:element>
    </xsl:if>
</xsl:template>

<xsl:template match="asmv1:assemblyIdentity|asmv2:assemblyIdentity">
    <xsl:call-template name="assembly-identity">
        <xsl:with-param name="class">AssemblyIdentity</xsl:with-param>
    </xsl:call-template>
</xsl:template>

<xsl:template match="asmv1:dependency">
    <xsl:element name="AssemblyReference" namespace="">    
        <xsl:attribute name="IsNative">true</xsl:attribute>
        <xsl:apply-templates select="asmv1:dependentAssembly|asmv2:dependentAssembly"/>
        <xsl:apply-templates select="asmv1:dependentAssembly/asmv1:assemblyIdentity|asmv1:dependentAssembly/asmv2:assemblyIdentity|asmv2:dependentAssembly/asmv1:assemblyIdentity|asmv2:dependentAssembly/asmv2:assemblyIdentity"/>
    </xsl:element>
</xsl:template>

<xsl:template match="asmv2:dependency">
    <xsl:element name="AssemblyReference" namespace="">    
        <xsl:attribute name="IsNative">false</xsl:attribute>
        <xsl:apply-templates select="asmv1:dependentAssembly|asmv2:dependentAssembly"/>
        <xsl:apply-templates select="asmv1:dependentAssembly/asmv1:assemblyIdentity|asmv1:dependentAssembly/asmv2:assemblyIdentity|asmv2:dependentAssembly/asmv1:assemblyIdentity|asmv2:dependentAssembly/asmv2:assemblyIdentity"/>
    </xsl:element>
</xsl:template>

<xsl:template match="asmv1:dependentAssembly">
    <xsl:if test="@asmv2:dependencyType='preRequisite'">
        <xsl:attribute name="IsPrerequisite">true</xsl:attribute>
    </xsl:if>
    <xsl:if test="string-length(@asmv2:codebase)>0">
        <xsl:attribute name="Path">
            <xsl:value-of select="@asmv2:codebase"/>
        </xsl:attribute>
    </xsl:if>
    <xsl:if test="string-length(@asmv2:size)>0">
        <xsl:attribute name="Size">
            <xsl:value-of select="@asmv2:size"/>
        </xsl:attribute>
    </xsl:if>
    <xsl:if test="string-length(asmv2:hash/dsig:DigestValue)>0">
        <xsl:attribute name="Hash">
            <xsl:value-of select="asmv2:hash/dsig:DigestValue"/>
        </xsl:attribute>
    </xsl:if>
    <xsl:if test="string-length(@asmv2:group)>0">
        <xsl:attribute name="Group">
            <xsl:value-of select="@asmv2:group"/>
        </xsl:attribute>
    </xsl:if>
    <xsl:if test="string-length(parent::node()/@asmv2:optional)>0">
        <xsl:attribute name="IsOptional">
            <xsl:value-of select="parent::node()/@asmv2:optional"/>
        </xsl:attribute>
    </xsl:if>
</xsl:template>

<xsl:template match="asmv2:dependentAssembly">
    <xsl:if test="@dependencyType='preRequisite'">
        <xsl:attribute name="IsPrerequisite">true</xsl:attribute>
    </xsl:if>
    <xsl:if test="string-length(@codebase)>0">
        <xsl:attribute name="Path">
            <xsl:value-of select="@codebase"/>
        </xsl:attribute>
    </xsl:if>
    <xsl:if test="string-length(@size)>0">
        <xsl:attribute name="Size">
            <xsl:value-of select="@size"/>
        </xsl:attribute>
    </xsl:if>
    <xsl:if test="string-length(asmv2:hash/dsig:DigestValue)>0">
        <xsl:attribute name="Hash">
            <xsl:value-of select="asmv2:hash/dsig:DigestValue"/>
        </xsl:attribute>
    </xsl:if>
    <xsl:if test="string-length(@group)>0">
        <xsl:attribute name="Group">
            <xsl:value-of select="@group"/>
        </xsl:attribute>
    </xsl:if>
    <xsl:if test="string-length(parent::node()/@optional)>0">
        <xsl:attribute name="IsOptional">
            <xsl:value-of select="parent::node()/@optional"/>
        </xsl:attribute>
    </xsl:if>
</xsl:template>

<xsl:template match="asmv1:dependentAssembly/asmv1:assemblyIdentity|asmv1:dependentAssembly/asmv2:assemblyIdentity|asmv2:dependentAssembly/asmv1:assemblyIdentity|asmv2:dependentAssembly/asmv2:assemblyIdentity">
        <xsl:call-template name="assembly-identity">
            <xsl:with-param name="class">AssemblyIdentity</xsl:with-param>
        </xsl:call-template>        
</xsl:template>

<xsl:template match="asmv1:file">
    <xsl:element name="FileReference" namespace="">
        <xsl:attribute name="Path">
            <xsl:value-of select="@name"/>
        </xsl:attribute>
        <xsl:if test="string-length(@size)>0">
            <xsl:attribute name="Size">
                <xsl:value-of select="@size"/>
            </xsl:attribute>
        </xsl:if>
        <xsl:if test="string-length(asmv2:hash/dsig:DigestValue)>0">
            <xsl:attribute name="Hash">
                <xsl:value-of select="asmv2:hash/dsig:DigestValue"/>
            </xsl:attribute>
        </xsl:if>
        <xsl:if test="string-length(@asmv2:group)>0">
            <xsl:attribute name="Group">
                <xsl:value-of select="@asmv2:group"/>
            </xsl:attribute>
        </xsl:if>
        <xsl:if test="string-length(@asmv2:optional)>0">
            <xsl:attribute name="IsOptional">
                <xsl:value-of select="@asmv2:optional"/>
            </xsl:attribute>
        </xsl:if>
        <xsl:if test="string-length(@asmv2:writeableType)>0">
            <xsl:attribute name="WriteableType">
                <xsl:value-of select="@asmv2:writeableType"/>
            </xsl:attribute>
        </xsl:if>
        <xsl:if test="count(asmv1:comClass)>0">
            <xsl:element name="ComClasses" namespace="">
                <xsl:apply-templates select="asmv1:comClass"/>
            </xsl:element>
        </xsl:if>
        <xsl:if test="count(asmv1:typelib)>0">
            <xsl:element name="TypeLibs" namespace="">
                <xsl:apply-templates select="asmv1:typelib"/>
            </xsl:element>
        </xsl:if>
        <xsl:if test="count(asmv1:comInterfaceProxyStub)>0">
            <xsl:element name="ProxyStubs" namespace="">
                <xsl:apply-templates select="asmv1:comInterfaceProxyStub"/>
            </xsl:element>
        </xsl:if>
    </xsl:element>
</xsl:template>

<xsl:template match="asmv2:file">
    <xsl:element name="FileReference" namespace="">
        <xsl:attribute name="Path">
            <xsl:value-of select="@name"/>
        </xsl:attribute>
        <xsl:if test="string-length(@size)>0">
            <xsl:attribute name="Size">
                <xsl:value-of select="@size"/>
            </xsl:attribute>
        </xsl:if>
        <xsl:if test="string-length(asmv2:hash/dsig:DigestValue)>0">
            <xsl:attribute name="Hash">
                <xsl:value-of select="asmv2:hash/dsig:DigestValue"/>
            </xsl:attribute>
        </xsl:if>
        <xsl:if test="string-length(@group)>0">
            <xsl:attribute name="Group">
                <xsl:value-of select="@group"/>
            </xsl:attribute>
        </xsl:if>
        <xsl:if test="string-length(@optional)>0">
            <xsl:attribute name="IsOptional">
                <xsl:value-of select="@optional"/>
            </xsl:attribute>
        </xsl:if>
        <xsl:if test="string-length(@writeableType)>0">
            <xsl:attribute name="WriteableType">
                <xsl:value-of select="@writeableType"/>
            </xsl:attribute>
        </xsl:if>
        <xsl:if test="count(asmv1:comClass)>0">
            <xsl:element name="ComClasses" namespace="">
                <xsl:apply-templates select="asmv1:comClass"/>
            </xsl:element>
        </xsl:if>
        <xsl:if test="count(asmv1:typelib)>0">
            <xsl:element name="TypeLibs" namespace="">
                <xsl:apply-templates select="asmv1:typelib"/>
            </xsl:element>
        </xsl:if>
        <xsl:if test="count(asmv1:comInterfaceProxyStub)>0">
            <xsl:element name="ProxyStubs" namespace="">
                <xsl:apply-templates select="asmv1:comInterfaceProxyStub"/>
            </xsl:element>
        </xsl:if>
    </xsl:element>
</xsl:template>

<xsl:template match="asmv1:comClass">
    <xsl:element name="ComClass" namespace="">
        <xsl:attribute name="Clsid">
            <xsl:value-of select="@clsid"/>
        </xsl:attribute>
        <xsl:attribute name="Progid">
            <xsl:value-of select="@progid"/>
        </xsl:attribute>
        <xsl:attribute name="ThreadingModel">
            <xsl:value-of select="@threadingModel"/>
        </xsl:attribute>
        <xsl:attribute name="Tlbid">
            <xsl:value-of select="@tlbid"/>
        </xsl:attribute>
        <xsl:attribute name="Description">
            <xsl:value-of select="@description"/>
        </xsl:attribute>
    </xsl:element>
</xsl:template>

<xsl:template match="asmv1:typelib">
    <xsl:element name="TypeLib" namespace="">
        <xsl:attribute name="Tlbid">
            <xsl:value-of select="@tlbid"/>
        </xsl:attribute>
        <xsl:attribute name="Version">
            <xsl:value-of select="@version"/>
        </xsl:attribute>
        <xsl:attribute name="HelpDir">
            <xsl:value-of select="@helpdir"/>
        </xsl:attribute>
        <xsl:attribute name="ResourceId">
            <xsl:value-of select="@resourceid"/>
        </xsl:attribute>
        <xsl:attribute name="Flags">
            <xsl:value-of select="@flags"/>
        </xsl:attribute>
    </xsl:element>
</xsl:template>

<xsl:template match="asmv1:comInterfaceProxyStub|asmv1:comInterfaceExternalProxyStub">
    <xsl:element name="ProxyStub" namespace="">
        <xsl:attribute name="Iid">
            <xsl:value-of select="@iid"/>
        </xsl:attribute>
        <xsl:attribute name="ProxyStubClsid32">
            <xsl:value-of select="@proxyStubClsid32"/>
        </xsl:attribute>
        <xsl:attribute name="File">
            <xsl:value-of select="@file"/>
        </xsl:attribute>
        <xsl:attribute name="BaseInterface">
            <xsl:value-of select="@baseInterface"/>
        </xsl:attribute>
        <xsl:attribute name="NumMethods">
            <xsl:value-of select="@numMethods"/>
        </xsl:attribute>
        <xsl:attribute name="name">
            <xsl:value-of select="@name"/>
        </xsl:attribute>
        <xsl:attribute name="tlbid">
            <xsl:value-of select="@tlbid"/>
        </xsl:attribute>
    </xsl:element>
</xsl:template>

<xsl:template name="assembly-identity">
    <xsl:param name="class"/>
    <xsl:element name="{$class}" namespace="">
        <xsl:if test="string-length(@name)>0">
            <xsl:attribute name="Name">
                <xsl:value-of select="@name"/>
            </xsl:attribute>
        </xsl:if>
        <xsl:if test="string-length(@version)>0">
            <xsl:attribute name="Version">
                <xsl:value-of select="@version"/>
            </xsl:attribute>
        </xsl:if>
        <xsl:if test="string-length(@publicKeyToken)>0">    
            <xsl:attribute name="PublicKeyToken">
                <xsl:value-of select="@publicKeyToken"/>
            </xsl:attribute>
        </xsl:if>
        <xsl:if test="string-length(@language)>0">
            <xsl:attribute name="Culture">
                <xsl:value-of select="@language"/>
            </xsl:attribute>
        </xsl:if>
        <xsl:if test="string-length(@processorArchitecture)>0">
            <xsl:attribute name="ProcessorArchitecture">
                <xsl:value-of select="@processorArchitecture"/>
            </xsl:attribute>
        </xsl:if>
        <xsl:if test="string-length(@type)>0">
            <xsl:attribute name="Type">
                <xsl:value-of select="@type"/>
            </xsl:attribute>
        </xsl:if>
    </xsl:element>
</xsl:template>



<xsl:template match="asmv2:trustInfo">
    <xsl:copy>
        <xsl:copy-of select="@*"/>
        <xsl:copy-of select="child::*"/>
    </xsl:copy>
</xsl:template>




<xsl:template name="trust-licenses">
  <xsl:if test="count(/asmv1:assembly/asmv2:licensing/asmv2:xrmlLicenseInfo/xrml:licenseGroup/xrml:license)>0"> 
    <xsl:element name="TrustLicenses" namespace="">
      <xsl:apply-templates select="/asmv1:assembly/asmv2:licensing/asmv2:xrmlLicenseInfo" mode="tlic"/>
  </xsl:element>  
 </xsl:if>
</xsl:template>
 
<xsl:template match="xrml:licenseGroup" mode="tlic">
  <xsl:for-each select="xrml:license">
    <xsl:element name="XmlDocument" namespace="">  
      <xsl:copy-of select="."/>
    </xsl:element>         
  </xsl:for-each>
</xsl:template>

<xsl:template name="public-key">
  <xsl:element name="PublicKey" namespace="">
        <xsl:copy-of select="//asmv1:assembly/dsig:Signature/dsig:KeyInfo"/>
  </xsl:element>
</xsl:template>

<xsl:template match="co.v1:fileAssociation">
  <xsl:element name="FileAssociation" namespace="">
    <xsl:attribute name="Extension">
      <xsl:value-of select="@extension"/>
    </xsl:attribute>
    <xsl:attribute name="Description">
      <xsl:value-of select="@description"/>
    </xsl:attribute>
    <xsl:attribute name="Progid">
      <xsl:value-of select="@progid"/>
    </xsl:attribute>
    <xsl:attribute name="DefaultIcon">
      <xsl:value-of select="@defaultIcon"/>
    </xsl:attribute>
  </xsl:element>
</xsl:template>

</xsl:stylesheet>
