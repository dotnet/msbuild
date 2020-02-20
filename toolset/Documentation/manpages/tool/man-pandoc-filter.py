#!/usr/bin/env python
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

import copy
from pandocfilters import toJSONFilters, Para, Str, Header, Space, Link

def remove_includes(key, value, format, meta):
    if key == 'Para' and value[0]['c'] == '[!INCLUDE':
        return Para([Str("")])
    return None

def promote_and_capitalize_sections(key, value, format, meta):
    if key == 'Header':
        header_contents = value[2]
        header_text = ' '.join([ x['c'] for x in header_contents if x['t'] == 'Str']).lower()
        if header_text in ['name', 'synopsis', 'description', 'options', 'examples', 'environment variables']:
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

def remove_references(key, value, format, meta):
    if key == 'Link':
        pass
        if value[1][0]['t'] == 'Code':
            return value[1][0]
        return Str(' '.join([e['c'] for e in value[1] if 'c' in e.keys()]))
    return None

def main():
    toJSONFilters([
        remove_includes,
        promote_and_capitalize_sections,
        demote_net_core_1_2,
        remove_references,
    ])

if __name__ == '__main__':
    main()
