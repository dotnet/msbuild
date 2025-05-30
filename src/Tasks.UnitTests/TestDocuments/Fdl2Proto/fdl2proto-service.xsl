<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:sila="http://www.sila-standard.org"
                xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                xsi:schemaLocation="http://www.sila-standard.org https://gitlab.com/SiLA2/sila_base/raw/master/schema/FeatureDefinition.xsd">
    <xsl:output method="text" encoding="UTF-8" indent="no"/>

    <!-- Service body for commands -->
    <xsl:template match="sila:Command" mode="service">
        <xsl:param name="package"/>
        <xsl:choose>
            <xsl:when test="sila:Observable = 'No'">
                <xsl:call-template name="ServiceCommandUnobservable">
                    <xsl:with-param name="package" select="$package"/>
                </xsl:call-template>
            </xsl:when>
            <xsl:when test="sila:Observable = 'Yes'">
                <xsl:call-template name="ServiceCommandObservable">
                    <xsl:with-param name="package" select="$package"/>
                </xsl:call-template>
            </xsl:when>
        </xsl:choose>
    </xsl:template>

    <!-- Service body for unobservable commands -->
    <xsl:template name="ServiceCommandUnobservable">
        <xsl:param name="package"/>
        <xsl:text>  /* </xsl:text> <xsl:value-of select="normalize-space(sila:Description)"/> <xsl:text> */</xsl:text>
        <xsl:call-template name="newline"/>
        <xsl:text>  rpc </xsl:text> <xsl:value-of select="sila:Identifier"/> <xsl:text> (</xsl:text>
        <xsl:value-of select="$package"/>.<xsl:value-of select="sila:Identifier"/><xsl:text>_Parameters</xsl:text>
        <xsl:text>) returns (</xsl:text>
        <xsl:value-of select="$package"/>.<xsl:value-of select="sila:Identifier"/><xsl:text>_Responses</xsl:text>
        <xsl:text>) {}</xsl:text>
        <xsl:call-template name="newline"/>
    </xsl:template>

    <!-- Service body for observable commands -->
    <xsl:template name="ServiceCommandObservable">
        <xsl:param name="package"/>
        <xsl:text>  /* </xsl:text><xsl:value-of select="sila:Description"/><xsl:text> */</xsl:text>
        <xsl:call-template name="newline"/>
        <xsl:text>  rpc </xsl:text><xsl:value-of select="sila:Identifier"/><xsl:text> (</xsl:text><xsl:value-of select="$package"/>.<xsl:value-of select="sila:Identifier"/><xsl:text>_Parameters) returns (sila2.org.silastandard.CommandConfirmation) {}</xsl:text>
        <xsl:call-template name="newline"/>
        <xsl:text>  /* Monitor the state of </xsl:text><xsl:value-of select="sila:Identifier"/><xsl:text> */</xsl:text>
        <xsl:call-template name="newline"/>
        <xsl:text>  rpc </xsl:text><xsl:value-of select="sila:Identifier"/><xsl:text>_Info (sila2.org.silastandard.CommandExecutionUUID) returns (stream sila2.org.silastandard.ExecutionInfo) {}</xsl:text>
        <xsl:call-template name="newline"/>
        <xsl:if test="sila:IntermediateResponse">
            <xsl:text>  /* Retrieve intermediate responses of </xsl:text><xsl:value-of select="sila:Identifier"/><xsl:text> */</xsl:text>
            <xsl:call-template name="newline"/>
            <xsl:text>  rpc </xsl:text><xsl:value-of select="sila:Identifier"/><xsl:text>_Intermediate (sila2.org.silastandard.CommandExecutionUUID) returns (stream </xsl:text><xsl:value-of select="$package"/>.<xsl:value-of select="sila:Identifier"/><xsl:text>_IntermediateResponses) {}</xsl:text>
            <xsl:call-template name="newline"/>
        </xsl:if>
        <xsl:text>  /* Retrieve result of </xsl:text><xsl:value-of select="sila:Identifier"/><xsl:text> */</xsl:text>
        <xsl:call-template name="newline"/>
        <xsl:text>  rpc </xsl:text><xsl:value-of select="sila:Identifier"/><xsl:text>_Result(sila2.org.silastandard.CommandExecutionUUID) returns (</xsl:text><xsl:value-of select="$package"/>.<xsl:value-of select="sila:Identifier"/><xsl:text>_Responses) {}</xsl:text>
        <xsl:call-template name="newline"/>
    </xsl:template>

    <!-- Service body for properties -->
    <xsl:template match="sila:Property" mode="service">
        <xsl:param name="package"/>
        <xsl:text>  /* </xsl:text><xsl:value-of select="sila:Description"/><xsl:text> */</xsl:text>
        <xsl:call-template name="newline"/>
        <xsl:text>  rpc </xsl:text> <xsl:call-template name="PropertyName"/>
        <xsl:text> (</xsl:text> <xsl:value-of select="$package"/>.<xsl:call-template name="PropertyName"/><xsl:text>_Parameters</xsl:text>
        <xsl:text>) returns (</xsl:text><xsl:if test="sila:Observable = 'Yes'">stream </xsl:if><xsl:value-of select="$package"/>.<xsl:call-template name="PropertyName"/><xsl:text>_Responses) {}</xsl:text>
        <xsl:call-template name="newline"/>
    </xsl:template>

    <!-- Service body for metadata -->
    <xsl:template match="sila:Metadata" mode="service">
        <xsl:param name="package"/>
        <xsl:text>  /* Get fully qualified identifiers of all features, commands and properties affected by </xsl:text><xsl:value-of select="sila:Identifier"/><xsl:text> */</xsl:text>
        <xsl:call-template name="newline"/>
        <xsl:text>  rpc Get_FCPAffectedByMetadata_</xsl:text><xsl:value-of select="sila:Identifier"/>
        <xsl:text> (</xsl:text><xsl:value-of select="$package"/>.Get_FCPAffectedByMetadata_<xsl:value-of select="sila:Identifier"/><xsl:text>_Parameters</xsl:text>
        <xsl:text>) returns (</xsl:text><xsl:value-of select="$package"/>.Get_FCPAffectedByMetadata_<xsl:value-of select="sila:Identifier"/><xsl:text>_Responses) {}</xsl:text>
        <xsl:call-template name="newline"/>
    </xsl:template>
</xsl:stylesheet>
