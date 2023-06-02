<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:sila="http://www.sila-standard.org"
                xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                xsi:schemaLocation="http://www.sila-standard.org https://gitlab.com/SiLA2/sila_base/raw/master/schema/FeatureDefinition.xsd">
    <xsl:output method="text" encoding="UTF-8" indent="no"/>
    
    <xsl:template name="generate-message">
        <xsl:param name="feature-package"/>
        <xsl:param name="parent-package"/>
        <xsl:param name="message-name"/>
        <xsl:param name="data-nodes"/>
        <xsl:param name="indent-level" select="0"/>

        <xsl:call-template name="indent"><xsl:with-param name="level" select="$indent-level"/></xsl:call-template>
        <xsl:text>message </xsl:text><xsl:value-of select="$message-name"/><xsl:text> {</xsl:text>
        <xsl:call-template name="newline"/>
            <!-- generate messages for structure types -->
            <xsl:for-each select="$data-nodes">
                <xsl:if test="sila:DataType/sila:Structure">
                    <xsl:call-template name="generate-message">
                        <xsl:with-param name="message-name" select="concat(sila:Identifier, '_Struct')"/>
                        <xsl:with-param name="data-nodes" select="sila:DataType/sila:Structure/sila:Element"/>
                        <xsl:with-param name="feature-package" select="$feature-package"/>
                        <xsl:with-param name="parent-package" select="concat($parent-package, '.', sila:Identifier, '_Struct')"/>
                        <xsl:with-param name="indent-level" select="$indent-level + 1"/>
                    </xsl:call-template>
                </xsl:if>
                <xsl:if test="sila:DataType/sila:List/sila:DataType/sila:Structure">
                    <xsl:call-template name="generate-message">
                        <xsl:with-param name="message-name" select="concat(sila:Identifier, '_Struct')"/>
                        <xsl:with-param name="data-nodes" select="sila:DataType/sila:List/sila:DataType/sila:Structure/sila:Element"/>
                        <xsl:with-param name="feature-package" select="$feature-package"/>
                        <xsl:with-param name="parent-package" select="concat($parent-package, '.', sila:Identifier, '_Struct')"/>
                        <xsl:with-param name="indent-level" select="$indent-level + 1"/>
                    </xsl:call-template>
                </xsl:if>
            </xsl:for-each>
        
            <!-- generate entry for each message member -->
            <xsl:for-each select="$data-nodes">
                <xsl:call-template name="indent"><xsl:with-param name="level" select="$indent-level + 1"/></xsl:call-template>
                <xsl:call-template name="generate-message-entry">
                    <xsl:with-param name="parent-package" select="concat($parent-package, '.', sila:Identifier, '_Struct')"/>
                    <xsl:with-param name="feature-package" select="$feature-package"/>
                    <xsl:with-param name="datatype" select="sila:DataType"/>
                    <xsl:with-param name="identifier" select="sila:Identifier"/>
                </xsl:call-template> = <xsl:value-of select="position()"/>;  /* <xsl:value-of select="normalize-space(sila:Description)"/><xsl:text> */</xsl:text>
                <xsl:call-template name="newline"/>
            </xsl:for-each>
        <xsl:call-template name="indent"><xsl:with-param name="level" select="$indent-level"/></xsl:call-template>
        <xsl:text>}</xsl:text>
        <xsl:call-template name="newline"/>
    </xsl:template>

    <!-- Message for data type definitions -->
    <xsl:template match="sila:DataTypeDefinition">
        <xsl:param name="package"/>

        <xsl:call-template name="newline"/>
        <xsl:text>/* </xsl:text><xsl:value-of select="sila:Description"/><xsl:text> */</xsl:text>
        <xsl:call-template name="newline"/>
        <xsl:call-template name="generate-message">
            <xsl:with-param name="message-name" select="concat('DataType_', sila:Identifier)"/>
            <xsl:with-param name="data-nodes" select="."/>
            <xsl:with-param name="feature-package" select="$package"/>
            <xsl:with-param name="parent-package" select="concat($package, '.', 'DataType_', sila:Identifier)"/>
        </xsl:call-template>
    </xsl:template>

    <!-- Message for commands -->
    <xsl:template match="sila:Command" mode="message">
        <xsl:param name="package"/>

        <xsl:call-template name="newline"/>
        <xsl:text>/* Parameters for </xsl:text><xsl:value-of select="sila:Identifier"/><xsl:text> */</xsl:text>
        <xsl:call-template name="newline"/>
        <xsl:call-template name="generate-message">
            <xsl:with-param name="message-name" select="concat(sila:Identifier, '_Parameters')"/>
            <xsl:with-param name="data-nodes" select="sila:Parameter"/>
            <xsl:with-param name="feature-package" select="$package"/>
            <xsl:with-param name="parent-package" select="concat($package, '.', sila:Identifier, '_Parameters')"/>
        </xsl:call-template>

        <xsl:call-template name="newline"/>
        <xsl:text>/* Responses of </xsl:text><xsl:value-of select="sila:Identifier"/><xsl:text> */</xsl:text>
        <xsl:call-template name="newline"/>
        <xsl:call-template name="generate-message">
            <xsl:with-param name="message-name" select="concat(sila:Identifier, '_Responses')"/>
            <xsl:with-param name="data-nodes" select="sila:Response"/>
            <xsl:with-param name="feature-package" select="$package"/>
            <xsl:with-param name="parent-package" select="concat($package, '.', sila:Identifier, '_Responses')"/>
        </xsl:call-template>

        <xsl:if test="sila:IntermediateResponse">
            <xsl:call-template name="newline"/>
            <xsl:text>/* Intermediate responses of </xsl:text><xsl:value-of select="sila:Identifier"/><xsl:text> */</xsl:text>
            <xsl:call-template name="newline"/>
            <xsl:call-template name="generate-message">
                <xsl:with-param name="message-name" select="concat(sila:Identifier, '_IntermediateResponses')"/>
                <xsl:with-param name="data-nodes" select="sila:IntermediateResponse"/>
                <xsl:with-param name="feature-package" select="$package"/>
                <xsl:with-param name="parent-package" select="concat($package, '.', sila:Identifier, '_IntermediateResponses')"/>
            </xsl:call-template>
        </xsl:if>
    </xsl:template>

    <!-- Message for properties -->
    <xsl:template match="sila:Property" mode="message">
        <xsl:param name="package"/>
        <xsl:variable name="property-name">
            <xsl:call-template name="PropertyName"/>
        </xsl:variable>

        <xsl:call-template name="newline"/>
        <xsl:text>/* Parameters for </xsl:text><xsl:value-of select="sila:Identifier"/><xsl:text> */</xsl:text>
        <xsl:call-template name="newline"/>
        <xsl:text>message </xsl:text><xsl:value-of select="concat($property-name, '_Parameters')"/><xsl:text> {</xsl:text>
        <xsl:call-template name="newline"/>
        <xsl:text>}</xsl:text>
        <xsl:call-template name="newline"/>

        <xsl:call-template name="newline"/>
        <xsl:text>/* Responses of </xsl:text><xsl:value-of select="sila:Identifier"/><xsl:text> */</xsl:text>
        <xsl:call-template name="newline"/>
        <xsl:call-template name="generate-message">
            <xsl:with-param name="message-name" select="concat($property-name, '_Responses')"/>
            <xsl:with-param name="data-nodes" select="."/>
            <xsl:with-param name="feature-package" select="$package"/>
            <xsl:with-param name="parent-package" select="concat($package, '.', $property-name, '_Responses')"/>
        </xsl:call-template>
    </xsl:template>

    <!-- Message for metadata -->
    <xsl:template match="sila:Metadata" mode="message">
        <xsl:param name="package"/>

        <xsl:call-template name="newline"/>
        <xsl:text>/* Parameters for Get_FCPAffectedByMetadata_</xsl:text><xsl:value-of select="sila:Identifier"/><xsl:text> */</xsl:text>
        <xsl:call-template name="newline"/>
        <xsl:text>message Get_FCPAffectedByMetadata_</xsl:text><xsl:value-of select="concat(sila:Identifier, '_Parameters')"/><xsl:text> {</xsl:text>
        <xsl:call-template name="newline"/>
        <xsl:text>}</xsl:text>
        <xsl:call-template name="newline"/>

        <xsl:call-template name="newline"/>
        <xsl:text>/* Responses of Get_FCPAffectedByMetadata_</xsl:text><xsl:value-of select="sila:Identifier"/><xsl:text> */</xsl:text>
        <xsl:call-template name="newline"/>
        <xsl:text>message Get_FCPAffectedByMetadata_</xsl:text><xsl:value-of select="concat(sila:Identifier, '_Responses')"/><xsl:text> {</xsl:text>
        <xsl:call-template name="newline"/>
        <xsl:text>  repeated sila2.org.silastandard.String AffectedCalls = 1;  /* Fully qualified identifiers of all features, commands and properties affected by </xsl:text><xsl:value-of select="sila:Identifier"/><xsl:text> */</xsl:text>
        <xsl:call-template name="newline"/>
        <xsl:text>}</xsl:text>
        <xsl:call-template name="newline"/>

        <xsl:call-template name="newline"/>
        <xsl:text>/* </xsl:text><xsl:value-of select="sila:Description"/><xsl:text> */</xsl:text>
        <xsl:call-template name="newline"/>
        <xsl:call-template name="generate-message">
            <xsl:with-param name="message-name" select="concat('Metadata_', sila:Identifier)"/>
            <xsl:with-param name="data-nodes" select="."/>
            <xsl:with-param name="feature-package" select="$package"/>
            <xsl:with-param name="parent-package" select="concat($package, '.', 'Metadata_', sila:Identifier)"/>
        </xsl:call-template>
    </xsl:template>

    <!-- Message entry -->
    <xsl:template name="generate-message-entry">
        <xsl:param name="feature-package"/>
        <xsl:param name="parent-package"/>
        <xsl:param name="identifier"/>
        <xsl:param name="datatype"/>
        <xsl:choose>
            <xsl:when test="$datatype/sila:Basic">
                <xsl:text>sila2.org.silastandard.</xsl:text><xsl:value-of select="$datatype/sila:Basic"/><xsl:text> </xsl:text><xsl:value-of select="$identifier"/>
            </xsl:when>
            <xsl:when test="$datatype/sila:Constrained">
                <xsl:call-template name="generate-message-entry">
                    <xsl:with-param name="feature-package" select="$feature-package"/>
                    <xsl:with-param name="parent-package" select="$parent-package"/>
                    <xsl:with-param name="identifier" select="sila:Identifier"/>
                    <xsl:with-param name="datatype" select="$datatype/sila:Constrained/sila:DataType"/>
                </xsl:call-template>
            </xsl:when>
            <xsl:when test="$datatype/sila:List">
                <xsl:text>repeated </xsl:text><xsl:call-template name="generate-message-entry">
                    <xsl:with-param name="feature-package" select="$feature-package"/>
                    <xsl:with-param name="parent-package" select="$parent-package"/>
                    <xsl:with-param name="identifier" select="sila:Identifier"/>
                    <xsl:with-param name="datatype" select="$datatype/sila:List/sila:DataType"/>
                </xsl:call-template>
            </xsl:when>
            <xsl:when test="$datatype/sila:DataTypeIdentifier">
                <xsl:value-of select="$feature-package"/>.DataType_<xsl:value-of select="$datatype/sila:DataTypeIdentifier"/><xsl:text> </xsl:text><xsl:value-of select="$identifier"/>
            </xsl:when>
            <xsl:when test="$datatype/sila:Structure">
                <xsl:value-of select="$parent-package"/><xsl:text> </xsl:text><xsl:value-of select="$identifier"/>
            </xsl:when>
        </xsl:choose>
    </xsl:template>
</xsl:stylesheet>
