#!/usr/bin/env python3
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

import re
import os
import os.path
import sys

def handle_missing_name(this_file, original_lines):

    result = []
    lines = original_lines

    for line in lines:
        if line.strip() == '# Name':
            print(f'Warning: file {this_file} already has a "# Name" section', file=sys.stderr)
            return lines

    for i in range(len(lines)):
        if len(lines[i].strip()) != 0:
            lines = lines[i:]
            break

    result.append('# Name' + os.linesep)
    result.append(os.linesep)
    command_name = os.path.basename(this_file)
    command_name = os.path.splitext(command_name)[0]
    result.append(command_name + ' - ' + lines[0].strip().strip('#') + os.linesep)
    result.append(os.linesep)
    result.append('# Description' + os.linesep)
    result.append(os.linesep)
    result.extend(lines[1:])

    return result

def main(args):
    filename = args[1]
    with open(filename) as original:
        lines = handle_missing_name(filename, original.readlines())
        with open(filename + '.tmp', 'w') as output:
            for line in lines:
                output.write(line)

    os.replace(filename + '.tmp',  filename)

if __name__ == '__main__':
    sys.exit(main(sys.argv))
