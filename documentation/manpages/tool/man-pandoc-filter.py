#!/usr/bin/env python3
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

import copy
from pandocfilters import toJSONFilters, Para, Str, Header, Space
import sys

def fail_on_includes(key, value, format, meta):
    if key == 'Para' and value[0]['c'] == '[!INCLUDE':
        assert False, 'Found an unexpected [!INCLUDE'

def promote_and_capitalize_sections(key, value, format, meta):
    if key == 'Header':
        header_contents = value[2]
        header_text = ' '.join([ x['c'] for x in header_contents if x['t'] == 'Str']).lower()
        if header_text in ['name', 'synopsis', 'description', 'arguments', 'options', 'examples', 'environment variables', 'see also']:
            # capitalize
            for element in header_contents:
                if element['t'] == 'Str':
                    element['c'] = element['c'].upper()
            # promote
            value = Header(1, value[1], header_contents)
            return value
    return None

def demote_net_core_1_2(key, value, format, meta):
    if key == 'Header':
        header_id = value[1][0]
        if header_id.startswith('net-core-'):
            value = Header(2, value[1], value[2][0]['c'][1])
            return value
    return None

fix_command_name = False

def fix_space_in_command_names(key, value, format, meta):
    global fix_command_name
    if key == 'Header':
        header_contents = value[2]
        header_text = ' '.join([ x['c'] for x in header_contents if x['t'] == 'Str']).lower()
        if header_text == 'name':
            fix_command_name = True
        else:
            fix_command_name = False
    if fix_command_name and key == 'Para':
        for i in range(len(value)):
            if value[i]['t'] == 'Code' and value[i]['c'][1].startswith('dotnet '):
                value[i] = {'t': 'Str', 'c': value[i]['c'][1].replace(' ', '-')}

def remove_markers(key, value, format, meta):
    if key == 'Str' and value in ['[!NOTE]', '[!IMPORTANT]']:
        return Str('')

def main():
    toJSONFilters([
        fail_on_includes,
        promote_and_capitalize_sections,
        demote_net_core_1_2,
        fix_space_in_command_names,
        remove_markers,
    ])

if __name__ == '__main__':
    main()
