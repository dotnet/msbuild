<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet
	xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
	xmlns="urn:schemas-microsoft-com:asm.v2"
	version="1.0">

<xsl:output method="xml" encoding="utf-8" indent="yes"/>
<xsl:strip-space elements="*"/>

<xsl:param name="defaultRequestedPrivileges"/>
<xsl:variable name="requestedPrivileges" select="document($defaultRequestedPrivileges)" />
              
<xsl:template match="asmv2:PermissionSet" xmlns:asmv2="urn:schemas-microsoft-com:asm.v2">
<trustInfo>
    <security>
        <applicationRequestMinimum>
            <xsl:copy-of select="."/>
            <defaultAssemblyRequest permissionSetReference="{@ID}" />
        </applicationRequestMinimum>
        <xsl:if test="$requestedPrivileges">
            <xsl:copy-of select="$requestedPrivileges"/>
        </xsl:if>
    </security>
</trustInfo>

</xsl:template>
</xsl:stylesheet>
