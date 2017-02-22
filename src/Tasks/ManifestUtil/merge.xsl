<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet
	xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
	xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
	xmlns="urn:schemas-microsoft-com:asm.v1"
	xmlns:asmv1="urn:schemas-microsoft-com:asm.v1"
	xmlns:asmv2="urn:schemas-microsoft-com:asm.v2"
	xmlns:asmv3="urn:schemas-microsoft-com:asm.v3"
	xmlns:xrml="urn:mpeg:mpeg21:2003:01-REL-R-NS"
	xmlns:dsig="http://www.w3.org/2000/09/xmldsig#"
	xmlns:co.v1="urn:schemas-microsoft-com:clickonce.v1"
	exclude-result-prefixes="asmv1 asmv2 asmv3 co.v1"
	version="1.0">

<xsl:output method="xml" encoding="utf-8" indent="yes"/>
<xsl:strip-space elements="*"/>

<xsl:param name="base-file"/>

<xsl:variable name="app" select="/asmv1:assembly/asmv1:application|asmv2:application"/>
<xsl:variable name="base" select="document($base-file)"/>


<!-- Defined set of standard elements that can be merged between input document and base document -->
<xsl:template match="/asmv1:assembly"><xsl:call-template name="merge-element"><xsl:with-param name="base-element" select="$base/asmv1:assembly"/></xsl:call-template></xsl:template>
<xsl:template match="/asmv1:assembly/asmv1:assemblyIdentity"><xsl:call-template name="merge-element"><xsl:with-param name="base-element" select="$base/asmv1:assembly/asmv1:assemblyIdentity"/></xsl:call-template></xsl:template>
<xsl:template match="/asmv1:assembly/asmv1:description"><xsl:call-template name="merge-element"><xsl:with-param name="base-element" select="$base/asmv1:assembly/asmv1:description"/></xsl:call-template></xsl:template>
<xsl:template match="/asmv1:assembly/asmv2:assemblyIdentity"><xsl:call-template name="merge-element"><xsl:with-param name="base-element" select="$base/asmv1:assembly/asmv2:assemblyIdentity"/></xsl:call-template></xsl:template>
<xsl:template match="/asmv1:assembly/asmv2:configuration"><xsl:call-template name="merge-element"><xsl:with-param name="base-element" select="$base/asmv1:assembly/asmv2:configuration"/></xsl:call-template></xsl:template>
<xsl:template match="/asmv1:assembly/asmv2:deployment"><xsl:call-template name="merge-element"><xsl:with-param name="base-element" select="$base/asmv1:assembly/asmv2:deployment"/></xsl:call-template></xsl:template>
<xsl:template match="/asmv1:assembly/asmv2:deployment/asmv2:subscription"><xsl:call-template name="merge-element"><xsl:with-param name="base-element" select="$base/asmv1:assembly/asmv2:deployment/asmv2:subscription"/></xsl:call-template></xsl:template>
<xsl:template match="/asmv1:assembly/asmv2:deployment/asmv2:subscription/asmv2:update"><xsl:call-template name="merge-element"><xsl:with-param name="base-element" select="$base/asmv1:assembly/asmv2:deployment/asmv2:subscription/asmv2:update"/></xsl:call-template></xsl:template>
<xsl:template match="/asmv1:assembly/asmv2:deployment/asmv2:subscription/asmv2:update/asmv2:expiration"><xsl:call-template name="merge-element"><xsl:with-param name="base-element" select="$base/asmv1:assembly/asmv2:deployment/asmv2:subscription/asmv2:update/asmv2:expiration"/></xsl:call-template></xsl:template>
<xsl:template match="/asmv1:assembly/asmv2:deployment/asmv2:deploymentProvider"><xsl:call-template name="merge-element"><xsl:with-param name="base-element" select="$base/asmv1:assembly/asmv2:deployment/asmv2:deploymentProvider"/></xsl:call-template></xsl:template>
<xsl:template match="/asmv1:assembly/asmv2:entryPoint"><xsl:call-template name="merge-element"><xsl:with-param name="base-element" select="$base/asmv1:assembly/asmv2:entryPoint"/></xsl:call-template></xsl:template>
<xsl:template match="/asmv1:assembly/asmv2:entryPoint/asmv2:assemblyIdentity"><xsl:call-template name="merge-element"><xsl:with-param name="base-element" select="$base/asmv1:assembly/asmv2:entryPoint/asmv2:assemblyIdentity"/></xsl:call-template></xsl:template>
<xsl:template match="/asmv1:assembly/asmv2:entryPoint/asmv2:commandLine"><xsl:call-template name="merge-element"><xsl:with-param name="base-element" select="$base/asmv1:assembly/asmv2:entryPoint/asmv2:commandLine"/></xsl:call-template></xsl:template>
<xsl:template match="/asmv1:assembly/asmv2:trustInfo"><xsl:call-template name="merge-element"><xsl:with-param name="base-element" select="$base/asmv1:assembly/asmv2:trustInfo"/></xsl:call-template></xsl:template>
<xsl:template match="/asmv1:assembly/asmv2:trustInfo/asmv2:security"><xsl:call-template name="merge-element"><xsl:with-param name="base-element" select="$base/asmv1:assembly/asmv2:trustInfo/asmv2:security"/></xsl:call-template></xsl:template>
<xsl:template match="/asmv1:assembly/asmv2:trustInfo/asmv2:security/asmv3:requestedPrivileges"><xsl:call-template name="merge-element"><xsl:with-param name="base-element" select="$base/asmv1:assembly/asmv2:trustInfo/asmv2:security/asmv3:requestedPrivileges"/></xsl:call-template></xsl:template>
<xsl:template match="/asmv1:assembly/asmv2:trustInfo/asmv2:security/asmv3:requestedPrivileges/asmv3:requestedExecutionLevel"><xsl:call-template name="merge-element"><xsl:with-param name="base-element" select="$base/asmv1:assembly/asmv2:trustInfo/asmv2:security/asmv3:requestedPrivileges/asmv3:requestedExecutionLevel"/></xsl:call-template></xsl:template>
<!-- All other matches (i.e. file, dependency) will be copied directly from input document, base document will have no impact -->
<xsl:template match="*"><xsl:copy-of select="."/></xsl:template>

<!-- Defined set of standard elements from base document -->
<xsl:template match="asmv2:application" mode="base"/>
<xsl:template match="asmv1:assemblyIdentity" mode="base"/>
<xsl:template match="asmv1:dependency" mode="base"/>
<xsl:template match="asmv1:description" mode="base"/>
<xsl:template match="asmv1:file" mode="base"/>
<xsl:template match="asmv2:applicationRequestMinimum" mode="base"/>
<xsl:template match="asmv2:assemblyIdentity" mode="base"/>
<xsl:template match="asmv2:beforeApplicationStartup" mode="base"/>
<xsl:template match="asmv2:commandLine" mode="base"/>
<xsl:template match="asmv2:configuration" mode="base"/>
<xsl:template match="asmv2:dependency" mode="base"/>
<xsl:template match="asmv2:deployment" mode="base"/>
<xsl:template match="asmv2:deploymentProvider" mode="base"/>
<xsl:template match="asmv2:entryPoint" mode="base"/>
<xsl:template match="asmv2:expiration" mode="base"/>
<xsl:template match="asmv2:file" mode="base"/>
<xsl:template match="asmv2:licensing" mode="base"/>
<xsl:template match="asmv2:subscription" mode="base"/>
<xsl:template match="asmv2:update" mode="base"/>
<xsl:template match="asmv3:hostInBrowser" mode="base"/>
<xsl:template match="asmv2:trustInfo" mode="base"/>
<xsl:template match="asmv2:security" mode="base"/>
<xsl:template match="asmv3:requestedPrivileges" mode="base"/>
<xsl:template match="asmv3:requestedExecutionLevel" mode="base"/>
<xsl:template match="dsig:Signature" mode="base"/>
<xsl:template match="co.v1:customHostSpecified" mode="base"/>
<xsl:template match="co.v1:useManifestForTrust" mode="base"/>
<xsl:template match="co.v1:fileAssociation" mode="base"/>
<!-- All other matches are non-standard elements from base document -->
<xsl:template match="*" mode="base">
	<xsl:copy-of select="."/>
</xsl:template>

<xsl:template name="merge-element">
    <xsl:param name="base-element"/>

    <!-- import comment from base if present -->
    <xsl:if test="preceding-sibling::comment()">
         <xsl:copy-of select="preceding-sibling::comment()"/>
    </xsl:if>
    <xsl:if test="$base-element/preceding-sibling::comment() and $base-element/preceding-sibling::comment()!=preceding-sibling::comment()">
         <xsl:copy-of select="$base-element/preceding-sibling::comment()"/>
    </xsl:if>

    <!-- copy current node and import attributes and sub-elements from base -->
    <xsl:copy>

        <xsl:call-template name="merge-attributes">
            <xsl:with-param name="base-attributes" select="$base-element/@*"/>
            <xsl:with-param name="app-attributes" select="@*"/>
        </xsl:call-template>

        <!-- Process all child elements from primary input document -->
        <xsl:apply-templates select="child::*"/>
        <!-- Import non-standard elements from base document -->		
        <xsl:apply-templates select="$base-element/child::*" mode="base"/>
        <xsl:choose>
            <xsl:when test="./text()">
                <xsl:copy-of select="./text()"/>
            </xsl:when>
            <xsl:when test="$base-element/text()">
                <xsl:copy-of select="$base-element/text()"/>
            </xsl:when>
        </xsl:choose>
    </xsl:copy>
</xsl:template>

<xsl:template name="merge-attributes">
    <xsl:param name="base-attributes"/>
    <xsl:param name="app-attributes"/>
	
    <!-- Import attributes from base document -->
    <xsl:for-each select="$base-attributes">
        <xsl:attribute name="{name()}"><xsl:value-of select="current()"/></xsl:attribute>
    </xsl:for-each>
    <!-- Import attributes from app document -->
    <xsl:for-each select="$app-attributes"> 
        <xsl:attribute name="{name()}"><xsl:value-of select="current()"/></xsl:attribute>
    </xsl:for-each>
</xsl:template>


</xsl:stylesheet>
