<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet
	xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
	xmlns="http://schemas.microsoft.com/developer/2004/01/bootstrapper" 
	xmlns:b="http://schemas.microsoft.com/developer/2004/01/bootstrapper" 
	version="1.0">

<xsl:variable name="newline">
<xsl:text>%NEWLINE%</xsl:text>
</xsl:variable>

    <xsl:output method="text"/>

    <xsl:template match="Configuration">
        <xsl:apply-templates/>
    </xsl:template>

    <xsl:template match="b:Package">
        <xsl:call-template name="object">
            <xsl:with-param name="name" select="local-name()"/>
            <xsl:with-param name="attributes" select="@*[local-name()='Name' or local-name()='LicenseAgreement' or local-name()='PackageCode']"/>
        </xsl:call-template>
    </xsl:template>

    <xsl:template match="*">
        <xsl:param name="indent"/>
        <xsl:call-template name="object">
            <xsl:with-param name="name" select="local-name()"/>
            <xsl:with-param name="attributes" select="@*"/>
            <xsl:with-param name="indent" select="$indent"/>
        </xsl:call-template>
    </xsl:template>

    <xsl:template match="b:PackageFile">
        <xsl:param name="indent"/>
        <xsl:call-template name="object">
            <xsl:with-param name="name" select="local-name()"/>
            <xsl:with-param name="attributes" select="@*[local-name()='Name' or local-name()='Size' or local-name()='Hash' or local-name()='HomeSite' or local-name()='PublicKey' or local-name()='UrlName']"/>
            <xsl:with-param name="indent" select="$indent"/>
        </xsl:call-template>
    </xsl:template>

    <xsl:template match="b:Dependencies"/>
    <xsl:template match="b:Overrides"/>

    <xsl:template name="object">
        <xsl:param name="name"/>
        <xsl:param name="attributes"/>
        <xsl:param name="indent"/>
        <xsl:value-of select="$indent"/>
        <xsl:text>Begin </xsl:text>
        <xsl:value-of select="$name"/>
        <xsl:value-of select="$newline"/>
        <xsl:apply-templates select="$attributes"><xsl:with-param name="indent" select="concat($indent, '&#9;')"/></xsl:apply-templates>
        <xsl:apply-templates select="child::*"><xsl:with-param name="indent" select="concat($indent, '&#9;')"/></xsl:apply-templates>
        <xsl:value-of select="$indent"/>
        <xsl:text>End </xsl:text>
        <xsl:value-of select="$name"/>
        <xsl:value-of select="$newline"/>
    </xsl:template>

    <xsl:template match="@*">
        <xsl:param name="indent"/>    
        <xsl:value-of select="$indent"/>
        <xsl:value-of select="local-name()"/>
        <xsl:text>="</xsl:text>
        <xsl:value-of select="."/>
        <xsl:text>"</xsl:text>
        <xsl:value-of select="$newline"/>
    </xsl:template>

</xsl:stylesheet>