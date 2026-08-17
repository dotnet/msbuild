<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:sila="http://www.sila-standard.org"
                xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                xsi:schemaLocation="http://www.sila-standard.org https://gitlab.com/SiLA2/sila_base/raw/master/schema/FeatureDefinition.xsd">
    <xsl:output method="text" encoding="UTF-8" indent="no"/>

    <xsl:template match="/sila:Feature">
        <xsl:call-template name="detect-errors"/>
    </xsl:template>

    <xsl:template name="detect-errors">
        <xsl:call-template name="detect-list-of-list"/>
        <xsl:call-template name="detect-unknown-identifiers"/>
        <xsl:call-template name="detect-invalid-constraint-base-type"/>
        <xsl:call-template name="detect-invalid-constraint"/>
        <xsl:call-template name="detect-duplicate-definitions"/>
        <xsl:call-template name="detect-cyclic-references"/>
        <xsl:call-template name="detect-invalid-sila2-version"/>
        <xsl:call-template name="detect-invalid-constraint-values"/>
        <xsl:call-template name="detect-intermediate-response-in-unobservable-command"/>
    </xsl:template>

    <xsl:template name="detect-list-of-list">
        <xsl:for-each select="//sila:List">
            <xsl:if test="./sila:DataType/sila:List">
                <xsl:message terminate="yes">Nested lists are not allowed</xsl:message>
            </xsl:if>
        </xsl:for-each>
    </xsl:template>

    <xsl:template name="detect-unknown-identifiers">
        <xsl:for-each select="//sila:DefinedExecutionErrors/sila:Identifier">
            <xsl:if test="not(//sila:DefinedExecutionError/sila:Identifier/text() = ./text())">
                <xsl:message terminate="yes">DefinedExecutionError '<xsl:value-of select="text()"/>' is not defined</xsl:message>
            </xsl:if>
        </xsl:for-each>
        <xsl:for-each select="//sila:DataTypeIdentifier">
            <xsl:if test="not(//sila:DataTypeDefinition/sila:Identifier/text() = ./text())">
                <xsl:message terminate="yes">Data type identifier '<xsl:value-of select="text()"/>' is not defined</xsl:message>
            </xsl:if>
        </xsl:for-each>
    </xsl:template>

    <xsl:template name="detect-invalid-constraint-base-type">
        <xsl:for-each select="//sila:Constrained">
            <xsl:choose>
                <xsl:when test="sila:DataType/sila:Structure">
                    <xsl:message terminate="yes">Constrained structures are not allowed</xsl:message>
                </xsl:when>
                <xsl:when test="sila:DataType/sila:Constrained">
                    <xsl:message terminate="yes">Constrained constrained types are not allowed</xsl:message>
                </xsl:when>
                <xsl:when test="sila:DataType/sila:DataTypeIdentifier">
                    <xsl:message terminate="yes">Constrained data type identifiers are not allowed</xsl:message>
                </xsl:when>
                <xsl:when test="sila:DataType/sila:Basic/text() = 'Boolean'">
                    <xsl:message terminate="yes">Constrained booleans are not allowed</xsl:message>
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:template>

    <xsl:template name="detect-invalid-constraint">
        <xsl:for-each select="//sila:Constrained">
            <xsl:choose>
                <xsl:when test="sila:DataType/sila:Basic/text() = 'String'">
                    <xsl:for-each select="sila:Constraints/*">
                        <xsl:if test="not(
                                local-name() = 'Length'
                                or local-name() = 'MinimalLength'
                                or local-name() = 'MaximalLength'
                                or local-name() = 'Pattern'
                                or local-name() = 'ContentType'
                                or local-name() = 'FullyQualifiedIdentifier'
                                or local-name() = 'Schema'
                                or local-name() = 'Set'
                            )">
                            <xsl:message terminate="yes">Invalid constraint on type String: '<xsl:value-of select="local-name()"/>'</xsl:message>
                        </xsl:if>
                    </xsl:for-each>
                </xsl:when>
                <xsl:when test="sila:DataType/sila:Basic/text() = 'Integer'">
                    <xsl:for-each select="sila:Constraints/*">
                        <xsl:if test="not(
                                local-name() = 'Set'
                                or local-name() = 'MaximalInclusive'
                                or local-name() = 'MaximalExclusive'
                                or local-name() = 'MinimalInclusive'
                                or local-name() = 'MinimalExclusive'
                                or local-name() = 'Unit'
                            )">
                            <xsl:message terminate="yes">Invalid constraint on type Integer: '<xsl:value-of select="local-name()"/>'</xsl:message>
                        </xsl:if>
                    </xsl:for-each>
                </xsl:when>
                <xsl:when test="sila:DataType/sila:Basic/text() = 'Real'">
                    <xsl:for-each select="sila:Constraints/*">
                        <xsl:if test="not(
                                local-name() = 'Set'
                                or local-name() = 'MaximalInclusive'
                                or local-name() = 'MaximalExclusive'
                                or local-name() = 'MinimalInclusive'
                                or local-name() = 'MinimalExclusive'
                                or local-name() = 'Unit'
                            )">
                            <xsl:message terminate="yes">Invalid constraint on type Real: '<xsl:value-of select="local-name()"/>'</xsl:message>
                        </xsl:if>
                    </xsl:for-each>
                </xsl:when>
                <xsl:when test="sila:DataType/sila:Basic/text() = 'Binary'">
                    <xsl:for-each select="sila:Constraints/*">
                        <xsl:if test="not(
                                local-name() = 'Length'
                                or local-name() = 'MinimalLength'
                                or local-name() = 'MaximalLength'
                                or local-name() = 'ContentType'
                                or local-name() = 'Schema'
                            )">
                            <xsl:message terminate="yes">Invalid constraint on type Binary: '<xsl:value-of select="local-name()"/>'</xsl:message>
                        </xsl:if>
                    </xsl:for-each>
                </xsl:when>
                <xsl:when test="sila:DataType/sila:Basic/text() = 'Date'">
                    <xsl:for-each select="sila:Constraints/*">
                        <xsl:if test="not(
                                local-name() = 'Set'
                                or local-name() = 'MaximalInclusive'
                                or local-name() = 'MaximalExclusive'
                                or local-name() = 'MinimalInclusive'
                                or local-name() = 'MinimalExclusive'
                            )">
                            <xsl:message terminate="yes">Invalid constraint on type Date: '<xsl:value-of select="local-name()"/>'</xsl:message>
                        </xsl:if>
                    </xsl:for-each>
                </xsl:when>
                <xsl:when test="sila:DataType/sila:Basic/text() = 'Time'">
                    <xsl:for-each select="sila:Constraints/*">
                        <xsl:if test="not(
                                local-name() = 'Set'
                                or local-name() = 'MaximalInclusive'
                                or local-name() = 'MaximalExclusive'
                                or local-name() = 'MinimalInclusive'
                                or local-name() = 'MinimalExclusive'
                            )">
                            <xsl:message terminate="yes">Invalid constraint on type Time: '<xsl:value-of select="local-name()"/>'</xsl:message>
                        </xsl:if>
                    </xsl:for-each>
                </xsl:when>
                <xsl:when test="sila:DataType/sila:Basic/text() = 'Timestamp'">
                    <xsl:for-each select="sila:Constraints/*">
                        <xsl:if test="not(
                                local-name() = 'Set'
                                or local-name() = 'MaximalInclusive'
                                or local-name() = 'MaximalExclusive'
                                or local-name() = 'MinimalInclusive'
                                or local-name() = 'MinimalExclusive'
                            )">
                            <xsl:message terminate="yes">Invalid constraint on type Timestamp: '<xsl:value-of select="local-name()"/>'</xsl:message>
                        </xsl:if>
                    </xsl:for-each>
                </xsl:when>
                <xsl:when test="sila:DataType/sila:Basic/text() = 'Any'">
                    <xsl:for-each select="sila:Constraints/*">
                        <xsl:if test="not(
                                local-name() = 'AllowedTypes'
                            )">
                            <xsl:message terminate="yes">Invalid constraint on type Any: '<xsl:value-of select="local-name()"/>'</xsl:message>
                        </xsl:if>
                    </xsl:for-each>
                </xsl:when>
                <xsl:when test="sila:DataType/sila:List">
                    <xsl:for-each select="sila:Constraints/*">
                        <xsl:if test="not(
                                local-name() = 'ElementCount'
                                or local-name() = 'MinimalElementCount'
                                or local-name() = 'MaximalElementCount'
                            )">
                            <xsl:message terminate="yes">Invalid constraint on type List: '<xsl:value-of select="local-name()"/>'</xsl:message>
                        </xsl:if>
                    </xsl:for-each>
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:template>

    <xsl:template name="detect-duplicate-definitions">
        <xsl:for-each select="//sila:DefinedExecutionError/sila:Identifier">
            <xsl:variable name="current-id" select="."/>
            <xsl:if test="count(//sila:DefinedExecutionError/sila:Identifier[text() = $current-id]) > 1">
                <xsl:message terminate="yes">Execution error '<xsl:value-of select="."/>' is defined multiple times</xsl:message>
            </xsl:if>
        </xsl:for-each>
        <xsl:for-each select="//sila:DataTypeDefinition/sila:Identifier/text()">
            <xsl:variable name="current-id" select="."/>
            <xsl:if test="count(//sila:DataTypeDefinition/sila:Identifier[text() = $current-id]) > 1">
                <xsl:message terminate="yes">Data type '<xsl:value-of select="."/>' is defined multiple times</xsl:message>
            </xsl:if>
        </xsl:for-each>
        <xsl:for-each select="//sila:Command/sila:Identifier/text()">
            <xsl:variable name="current-id" select="."/>
            <xsl:if test="count(//sila:Command/sila:Identifier[text() = $current-id]) > 1">
                <xsl:message terminate="yes">Command '<xsl:value-of select="."/>' is defined multiple times</xsl:message>
            </xsl:if>
        </xsl:for-each>
        <xsl:for-each select="//sila:Property/sila:Identifier/text()">
            <xsl:variable name="current-id" select="."/>
            <xsl:if test="count(//sila:Property/sila:Identifier[text() = $current-id]) > 1">
                <xsl:message terminate="yes">Property '<xsl:value-of select="."/>' is defined multiple times</xsl:message>
            </xsl:if>
        </xsl:for-each>
        <xsl:for-each select="//sila:Metadata/sila:Identifier/text()">
            <xsl:variable name="current-id" select="."/>
            <xsl:if test="count(//sila:Metadata/sila:Identifier[text() = $current-id]) > 1">
                <xsl:message terminate="yes">Metadata '<xsl:value-of select="."/>' is defined multiple times</xsl:message>
            </xsl:if>
        </xsl:for-each>
        <xsl:for-each select="//sila:Command">
            <xsl:variable name="current-command" select="."/>
            <xsl:for-each select="sila:Parameter/sila:Identifier/text()">
                <xsl:variable name="current-id" select="."/>
                <xsl:if test="count($current-command/sila:Parameter/sila:Identifier[text() = $current-id]) > 1">
                    <xsl:message terminate="yes">Command '<xsl:value-of select="$current-command/sila:Identifier/text()"/>': parameter '<xsl:value-of select="."/>' is defined multiple times</xsl:message>
                </xsl:if>
            </xsl:for-each>
            <xsl:for-each select="sila:IntermediateResponse/sila:Identifier/text()">
                <xsl:variable name="current-id" select="."/>
                <xsl:if test="count($current-command/sila:IntermediateResponse/sila:Identifier[text() = $current-id]) > 1">
                    <xsl:message terminate="yes">Command '<xsl:value-of select="$current-command/sila:Identifier/text()"/>': intermediate response '<xsl:value-of select="."/>' is defined multiple times</xsl:message>
                </xsl:if>
            </xsl:for-each>
            <xsl:for-each select="sila:Response/sila:Identifier/text()">
                <xsl:variable name="current-id" select="."/>
                <xsl:if test="count($current-command/sila:Response/sila:Identifier[text() = $current-id]) > 1">
                    <xsl:message terminate="yes">Command '<xsl:value-of select="$current-command/sila:Identifier/text()"/>': response '<xsl:value-of select="."/>' is defined multiple times</xsl:message>
                </xsl:if>
            </xsl:for-each>
            <xsl:for-each select="sila:DefinedExecutionErrors/sila:Identifier/text()">
                <xsl:variable name="current-id" select="."/>
                <xsl:if test="count($current-command/sila:DefinedExecutionErrors/sila:Identifier[text() = $current-id]) > 1">
                    <xsl:message terminate="yes">Command '<xsl:value-of select="$current-command/sila:Identifier/text()"/>': execution error '<xsl:value-of select="."/>' is defined multiple times</xsl:message>
                </xsl:if>
            </xsl:for-each>
        </xsl:for-each>
    </xsl:template>

    <xsl:template name="detect-cyclic-references">
        <xsl:for-each select="//sila:DataTypeDefinition">
            <xsl:variable name="outer-def" select="."/>
            <xsl:variable name="outer-id" select="sila:Identifier/text()"/>
            <xsl:for-each select="//sila:DataTypeDefinition">
                <xsl:variable name="inner-def" select="."/>
                <xsl:variable name="inner-id" select="sila:Identifier/text()"/>
                <xsl:if test="($inner-id != $outer-id) and $inner-def/descendant::sila:DataTypeIdentifier[text() = $outer-id] and $outer-def/descendant::sila:DataTypeIdentifier[text() = $inner-id]">
                    <xsl:message terminate="yes">Cyclic reference in data type definitions '<xsl:value-of select="$outer-id"/>' and '<xsl:value-of select="$inner-id"/>'</xsl:message>
                </xsl:if>
            </xsl:for-each>
        </xsl:for-each>
    </xsl:template>

    <xsl:template name="detect-invalid-sila2-version">
        <xsl:variable name="sila2-version" select="@SiLA2Version"/>
        <xsl:if test="not ($sila2-version = '1.0' or $sila2-version = '1.1')">
            <xsl:message terminate="yes">Invalid SiLA2 version: '<xsl:value-of select="$sila2-version"/>'</xsl:message>
        </xsl:if>
    </xsl:template>

    <xsl:template name="detect-invalid-constraint-values">
        <xsl:for-each select="//sila:Set/sila:Value | //sila:MaximalExclusive | //sila:MaximalInclusive | //sila:MinimalExclusive | //sila:MinimalInclusive">
            <xsl:variable name="type" select="ancestor::sila:Constrained/sila:DataType/sila:Basic/text()"/>
            <!-- Integer -->
            <xsl:if test="$type = 'Integer' and local-name() = 'Value'">
                <xsl:call-template name="validate-integer">
                    <xsl:with-param name="value" select="text()"/>
                </xsl:call-template>
            </xsl:if>
            <!-- Real -->
            <xsl:if test="$type = 'Real' or ($type = 'Integer' and substring(local-name(), 1, 1) = 'M')">
                <xsl:call-template name="validate-real">
                    <xsl:with-param name="value" select="text()"/>
                </xsl:call-template>
            </xsl:if>
            <!-- Date -->
            <xsl:if test="$type = 'Date'">
                <xsl:call-template name="validate-date">
                    <xsl:with-param name="date" select="substring(text(), 1, 10)"/>
                </xsl:call-template>
                <xsl:call-template name="validate-timezone">
                    <xsl:with-param name="timezone" select="substring(text(), 11)"/>
                </xsl:call-template>
            </xsl:if>
            <!-- Time -->
            <xsl:if test="$type = 'Time'">
                <xsl:call-template name="validate-time">
                    <xsl:with-param name="time" select="substring(text(), 1, 8)"/>
                </xsl:call-template>
                <xsl:call-template name="validate-timezone">
                    <xsl:with-param name="timezone" select="substring(text(), 9)"/>
                </xsl:call-template>
            </xsl:if>
            <!-- Timestamp -->
            <xsl:if test="$type = 'Timestamp'">
                <xsl:call-template name="validate-date">
                    <xsl:with-param name="date" select="substring(text(), 1, 10)"/>
                </xsl:call-template>
                <xsl:if test="substring(text(), 11, 1) != 'T'">
                    <xsl:message terminate="yes">Invalid Timestamp: 11th character must be 'T'</xsl:message>
                </xsl:if>
                <xsl:call-template name="validate-time">
                    <xsl:with-param name="time" select="substring(text(), 12, 8)"/>
                </xsl:call-template>
                <xsl:call-template name="validate-timezone">
                    <xsl:with-param name="timezone" select="substring(text(), 20)"/>
                </xsl:call-template>
            </xsl:if>
        </xsl:for-each>
        <xsl:for-each select="//sila:AllowedTypes//sila:DataTypeIdentifier">
            <xsl:message terminate="yes">DataTypeIdentifier is not allowed in AllowedTypes</xsl:message>
        </xsl:for-each>
    </xsl:template>

    <xsl:template name="validate-integer">
        <xsl:param name="value"/>

        <xsl:if test="not(string-length($value) > 0 and concat(translate(substring($value, 1, 1), '+-0123456789', ''), translate(substring($value, 2), '0123456789', '')) = '')">
            <xsl:message terminate="yes">Not an integer value: '<xsl:value-of select="$value"/>'</xsl:message>
        </xsl:if>
    </xsl:template>

    <xsl:template name="validate-real">
        <xsl:param name="value"/>

        <xsl:choose>
            <xsl:when test="$value = 'INF' or $value = '-INF' or $value = '+INF' or $value = 'NaN'"/>
            <xsl:when test="contains($value, 'e') or contains($value, 'E')">
                <xsl:variable name="lowercase-value" select="translate($value, 'E', 'e')"/>
                <xsl:call-template name="validate-float">
                    <xsl:with-param name="value" select="substring-before($lowercase-value, 'e')"/>
                </xsl:call-template>
                <xsl:call-template name="validate-integer">
                    <xsl:with-param name="value" select="substring-after($lowercase-value, 'e')"/>
                </xsl:call-template>
            </xsl:when>
            <xsl:otherwise>
                <xsl:call-template name="validate-float">
                    <xsl:with-param name="value" select="$value"/>
                </xsl:call-template>
            </xsl:otherwise>
        </xsl:choose>

    </xsl:template>

    <xsl:template name="validate-float">
        <xsl:param name="value"/>

        <!-- first character is in '+-.0123456789' and rest is in '.0123456789' -->
        <xsl:if test="translate(substring($value, 1, 1), '+-.0123456789', '') != '' or translate(substring($value, 2), '.0123456789', '') != ''">
            <xsl:message terminate="yes">Not a decimal value: '<xsl:value-of select="$value"/>'</xsl:message>
        </xsl:if>
        <!-- only '0123456789' is allowed after the first '.' -->
        <xsl:if test="contains($value, '.') and translate(substring-after($value, '.'), '0123456789', '') != ''">
            <xsl:message terminate="yes">Not a decimal value: '<xsl:value-of select="$value"/>'</xsl:message>
        </xsl:if>
    </xsl:template>

    <xsl:template name="validate-timezone">
        <xsl:param name="timezone"/>
        <!-- Z -->
        <xsl:if test="$timezone != 'Z'">
            <!-- 6 chars -->
            <xsl:if test="string-length($timezone) != 6">
                <xsl:message terminate="yes">Invalid Timezone format: Must be 'Z' or '+HH:MM' or '-HH:MM', was '<xsl:value-of select="$timezone"/>'</xsl:message>
            </xsl:if>
            <!-- char 1 is + or - -->
            <xsl:if test="not(contains('+-', substring($timezone, 1, 1)))">
                <xsl:message terminate="yes">Invalid Timezone format: Must be 'Z' or '+HH:MM' or '-HH:MM', was '<xsl:value-of select="$timezone"/>'</xsl:message>
            </xsl:if>
            <!-- chars 2, 3, 5 and 6 are numbers - -->
            <xsl:if test="not(translate(concat(substring($timezone, 2, 2), substring($timezone, 5, 2)), '0123456789', '') = '')">
                <xsl:message terminate="yes">Invalid Timezone format: Must be 'Z' or '+HH:MM' or '-HH:MM', was '<xsl:value-of select="$timezone"/>'</xsl:message>
            </xsl:if>
            <!-- char 4 is : -->
            <xsl:if test="not(substring($timezone, 4, 1) = ':')">
                <xsl:message terminate="yes">Invalid Timezone format: Must be 'Z' or '+HH:MM' or '-HH:MM', was '<xsl:value-of select="$timezone"/>'</xsl:message>
            </xsl:if>
            <xsl:variable name="hour" select="number(substring($timezone, 2, 2))"/>
            <xsl:variable name="minute" select="number(substring($timezone, 5, 2))"/>
            <xsl:if test="0 > $minute or $minute > 59 or 0 > $hour">
                <xsl:message terminate="yes">Invalid Timezone format: Minute must be in range 00-59, was '<xsl:value-of select="$timezone"/>'</xsl:message>
            </xsl:if>
            <xsl:if test="$minute + $hour * 60 > 840">
                <xsl:message terminate="yes">Invalid Timezone format: Must be in range -14:00 to +14:00, was '<xsl:value-of select="$timezone"/>'</xsl:message>
            </xsl:if>
        </xsl:if>
    </xsl:template>

    <xsl:template name="validate-date">
        <xsl:param name="date"/>
        <xsl:if test="string-length($date) != 10">
            <xsl:message terminate="yes">Invalid Date format: Must be 'YYYY-MM-DD', was '<xsl:value-of select="$date"/>'</xsl:message>
        </xsl:if>
        <xsl:if test="translate(concat(substring($date, 1, 4), substring($date, 6, 2), substring($date, 9, 2)), '0123456789', '') != ''">
            <xsl:message terminate="yes">Invalid Date format: Must be 'YYYY-MM-DD', was '<xsl:value-of select="$date"/>'</xsl:message>
        </xsl:if>
        <xsl:if test="concat(substring($date, 5, 1), substring($date, 8, 1)) != '--'">
            <xsl:message terminate="yes">Invalid Date format: Must be 'YYYY-MM-DD', was '<xsl:value-of select="$date"/>'</xsl:message>
        </xsl:if>
        <xsl:variable name="year" select="number(substring($date, 1, 4))"/>
        <xsl:variable name="month" select="number(substring($date, 6, 2))"/>
        <xsl:variable name="day" select="number(substring($date, 9, 2))"/>
        <xsl:if test="$month > 12 or $day > 31">
            <xsl:message terminate="yes">Invalid Date format: Must be 'YYYY-MM-DD' (month or day out of range), was '<xsl:value-of select="$date"/>'</xsl:message>
        </xsl:if>
        <xsl:if test="($month = 4 or $month = 6 or $month = 9 or $month = 11) and $day > 30">
            <xsl:message terminate="yes">Invalid Date format: Must be 'YYYY-MM-DD' (day out of range), was '<xsl:value-of select="$date"/>'</xsl:message>
        </xsl:if>
        <xsl:if test="$month = 2">
            <xsl:if test="$day > 29">
                <xsl:message terminate="yes">Invalid Date format: Must be 'YYYY-MM-DD' (day out of range), was '<xsl:value-of select="$date"/>'</xsl:message>
            </xsl:if>
            <xsl:if test="$year mod 4 != 0 and $day > 28">
                <xsl:message terminate="yes">Invalid Date format: Must be 'YYYY-MM-DD' (day out of range), was '<xsl:value-of select="$date"/>'</xsl:message>
            </xsl:if>
            <xsl:if test="$year mod 100 = 0 and $year mod 400 != 0 and $day > 28">
                <xsl:message terminate="yes">Invalid Date format: Must be 'YYYY-MM-DD' (day out of range), was '<xsl:value-of select="$date"/>'</xsl:message>
            </xsl:if>
        </xsl:if>
    </xsl:template>

    <xsl:template name="validate-time">
        <xsl:param name="time"/>
        <xsl:if test="string-length($time) != 8">
            <xsl:message terminate="yes">Invalid Date format: Must be 'HH:MM:SS', was '<xsl:value-of select="$time"/>'</xsl:message>
        </xsl:if>
        <xsl:if test="translate(concat(substring($time, 1, 2), substring($time, 4, 2), substring($time, 7, 2)), '0123456789', '') != ''">
            <xsl:message terminate="yes">Invalid Date format: Must be 'HH:MM:SS', was '<xsl:value-of select="$time"/>'</xsl:message>
        </xsl:if>
        <xsl:if test="concat(substring($time, 3, 1), substring($time, 6, 1)) != '::'">
            <xsl:message terminate="yes">Invalid Date format: Must be 'HH:MM:SS', was '<xsl:value-of select="$time"/>'</xsl:message>
        </xsl:if>
        <xsl:variable name="hour" select="number(substring($time, 1, 2))"/>
        <xsl:variable name="minute" select="number(substring($time, 4, 2))"/>
        <xsl:variable name="second" select="number(substring($time, 7, 2))"/>
        <xsl:if test="$hour > 23 or $minute > 59 or $second > 59">
            <xsl:message terminate="yes">Invalid Date format: Must be 'HH:MM:SS' (hour, minute or seconds out of range), was '<xsl:value-of select="$time"/>'</xsl:message>
        </xsl:if>
    </xsl:template>

    <xsl:template name="detect-intermediate-response-in-unobservable-command">
        <xsl:for-each select="//sila:Command[sila:Observable/text() = 'No']/sila:IntermediateResponse">
            <xsl:message terminate="yes">Unobservable commands must not have intermediate responses</xsl:message>
        </xsl:for-each>
    </xsl:template>
</xsl:stylesheet>
