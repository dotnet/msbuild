#!/usr/bin/env python

import argparse
import glob
import os
import re
import subprocess
import sys

SCRIPT_ROOT_PATH = os.path.dirname(os.path.realpath(__file__))
PERFTEST_JSON_PATH = os.path.join(SCRIPT_ROOT_PATH, 'project.json')
XUNITPERF_REPO_URL = 'https://github.com/microsoft/xunit-performance.git'

script_args = None

class FatalError(Exception):
    def __init__(self, message):
        self.message = message

def check_requirements():
    try:
        run_command('git', '--version', quiet = True)
    except:
        raise FatalError("git not found, please make sure that it's installed and on path.")

    try:
        run_command('msbuild', '-version', quiet = True)
    except:
        raise FatalError("msbuild not found, please make sure that it's installed and on path.")

    if script_args.xunit_perf_path == None:
        raise FatalError("Don't know where to clone xunit-performance. Please specify --xunit-perf-path <path>. " +
            "You can also set/export XUNIT_PERFORMANCE_PATH to not have to set the value every time.")

def process_arguments():
    parser = argparse.ArgumentParser(
        description = "Runs CLI perf tests. Requires 'git' and 'msbuild' to be on the PATH.",
    )

    parser.add_argument(
        'test_cli',
        help = "full path to the dotnet.exe under test",
    )
    parser.add_argument(
        '--runid', '--name', '-n',
        help = "unique ID for this run",
        required = True,
    )
    parser.add_argument(
        '--base', '--baseline', '-b',
        help = "full path to the baseline dotnet.exe",
        metavar = 'baseline_cli',
        dest = 'base_cli',
    )
    parser.add_argument(
        '--xunit-perf-path', '-x',
        help = """Path to local copy of the xunit-performance repository.
            Required unless the environment variable XUNIT_PERFORMANCE_PATH is defined.""",
        default = os.environ.get('XUNIT_PERFORMANCE_PATH'),
        metavar = 'path',
    )
    parser.add_argument(
        '--rebuild', '--rebuild-tools', '-r',
        help = "Rebuilds the test tools from scratch.",
        action = 'store_true',
    )
    parser.add_argument(
        '--verbose', '-v',
        help = "Shows the output of all commands run by this script",
        action = 'store_true',
    )

    global script_args
    script_args = parser.parse_args()

def run_command(*vargs, **kwargs):
    title = kwargs['title'] if 'title' in kwargs else None
    from_dir = kwargs['from_dir'] if 'from_dir' in kwargs else None
    quiet = kwargs['quiet'] if 'quiet' in kwargs else False

    quoted_args = map(lambda x: '"{x}"'.format(x=x) if ' ' in x else x, vargs)
    cmd_line = ' '.join(quoted_args)
    should_log = not script_args.verbose and title != None
    redirect_args = { 'stderr': subprocess.STDOUT }

    nullfile = None
    logfile = None
    cwd = None

    try:
        if should_log:
            log_name = '-'.join(re.sub(r'\W', ' ', title).lower().split()) + '.log'
            log_path = os.path.join(SCRIPT_ROOT_PATH, 'logs', 'run-perftests', log_name)
            log_dir = os.path.dirname(log_path)
            if not os.path.exists(log_dir):
                os.makedirs(log_dir)
            cmd_line += ' > "{log}"'.format(log = log_path)
            logfile = open(log_path, 'w')
            redirect_args['stdout'] = logfile

        elif quiet or not script_args.verbose:
            nullfile = open(os.devnull, 'w')
            redirect_args['stdout'] = nullfile

        prefix = ''
        if not quiet and title != None:
            print('# {msg}...'.format(msg = title))
            prefix = '  $ '

        if from_dir != None:
            cwd = os.getcwd()
            if not quiet: print('{pref}cd "{dir}"'.format(pref = prefix, dir = from_dir))
            os.chdir(from_dir)

        if not quiet: print(prefix + cmd_line)
        returncode = subprocess.call(vargs, **redirect_args)

        if returncode != 0:
            logmsg = " See '{log}' for details.".format(log = log_path) if should_log else ''
            raise FatalError("Command `{cmd}` returned with error code {e}.{log}".format(cmd = cmd_line, e = returncode, log = logmsg))

    finally:
        if logfile != None: logfile.close()
        if nullfile != None: nullfile.close()
        if cwd != None: os.chdir(cwd)

def clone_repo(repo_url, local_path):
    if os.path.exists(local_path):
        # For now, we just assume that if the path exists, it's already the correct repo
        print("# xunit-performance repo was detected at '{path}', skipping git clone".format(path = local_path))
        return
    run_command(
        'git', 'clone', repo_url, local_path,
        title = "Clone the xunit-performance repo",
    )

def get_xunitperf_dotnet_path(xunitperf_src_path):
    return os.path.join(xunitperf_src_path, 'tools', 'bin', 'dotnet')

def get_xunitperf_runner_src_path(xunitperf_src_path):
    return os.path.join(xunitperf_src_path, 'src', 'cli', 'Microsoft.DotNet.xunit.performance.runner.cli')

def get_xunitperf_analyzer_path(xunitperf_src_path):
    return os.path.join(xunitperf_src_path, 'src', 'xunit.performance.analysis', 'bin', 'Release', 'xunit.performance.analysis')

def make_xunit_perf(xunitperf_src_path):
    dotnet_path = get_xunitperf_dotnet_path(xunitperf_src_path)
    dotnet_base_path = os.path.dirname(dotnet_path)
    analyzer_base_path = os.path.dirname(get_xunitperf_analyzer_path(xunitperf_src_path))
    runner_src_path = get_xunitperf_runner_src_path(xunitperf_src_path)

    if script_args.rebuild or not os.path.exists(dotnet_base_path) or not os.path.exists(analyzer_base_path):
        run_command(
            'CiBuild.cmd', '/release',
            title = "Build xunit-performance",
            from_dir = xunitperf_src_path,
        )
        run_command(
            dotnet_path, 'publish', '-c', 'Release', runner_src_path,
            title = "Build Microsoft.DotNet.xunit.performance.runner.cli",
        )
    else:
        print("# xunit-performance at '{path}' was already built, skipping CiBuild. Use --rebuild to force rebuild.".format(path = xunitperf_src_path))

def run_perf_test(runid, cli_path, xunitperf_src_path):
    cli_path = os.path.realpath(cli_path)
    dotnet_path = get_xunitperf_dotnet_path(xunitperf_src_path)
    runner_src_path = get_xunitperf_runner_src_path(xunitperf_src_path)
    result_xml_path = os.path.join(SCRIPT_ROOT_PATH, '{}.xml'.format(runid))
    project_lock_path = os.path.join(SCRIPT_ROOT_PATH, 'project.lock.json')

    saved_path = os.environ.get('PATH')
    print("# Prepending {dir} to PATH".format(dir = os.path.dirname(cli_path)))
    os.environ['PATH'] = os.path.dirname(cli_path) + ';' + os.environ.get('PATH')
    try:
        if os.path.exists(project_lock_path):
            print("# Deleting {file}".format(file = project_lock_path))
            os.remove(project_lock_path)
        run_command(
            cli_path, 'restore', '-f', 'https://dotnet.myget.org/f/dotnet-core',
            title = "Dotnet restore using \"{cli}\"".format(cli = cli_path),
            from_dir = SCRIPT_ROOT_PATH,
        )
        run_command(
            dotnet_path, 'run', '-p', runner_src_path, '-c', 'Release', '--',
            '-runner', cli_path, '-runid', runid,
            '-runnerargs', 'test {json} -c Release'.format(json = PERFTEST_JSON_PATH),
            title = "Run {id}".format(id = runid),
            from_dir = SCRIPT_ROOT_PATH,
        )
        if not os.path.exists(result_xml_path):
            raise FatalError("Running {id} seems to have failed: {xml} was not generated".format(
                id = runid, xml = result_xml_path
            ))
    finally:
        print("# Reverting PATH")
        os.environ['PATH'] = saved_path

def compare_results(base_id, test_id, out_html, xunitperf_src_path):
    analyzer_path = get_xunitperf_analyzer_path(xunitperf_src_path)

    # Make sure there aren't any stale XMLs in the target dir
    for xml in glob.glob(os.path.join(SCRIPT_ROOT_PATH, '*.xml')):
        if not os.path.basename(xml) in [base_id + '.xml', test_id + '.xml']:
            os.rename(xml, xml + '.bak')

    try:
        run_command(
            analyzer_path, SCRIPT_ROOT_PATH, '-compare', base_id, test_id, '-html', out_html,
            title = "Generate comparison report",
            from_dir = SCRIPT_ROOT_PATH,
        )
        if os.path.exists(out_html):
            print("# Comparison finished, please see \"{report}\" for details.".format(report = out_html))
        else:
            raise FatalError("Failed to genererate comparison report: \"{report}\" not found.".format(report = out_html))

    finally:
        # Revert the renamed XMLs
        for xml in glob.glob(os.path.join(SCRIPT_ROOT_PATH, '*.xml.bak')):
            os.rename(xml, xml[0:-4])

def main():
    try:
        process_arguments()
        check_requirements()

        script_args.xunit_perf_path = os.path.realpath(script_args.xunit_perf_path)

        clone_repo(XUNITPERF_REPO_URL, script_args.xunit_perf_path)
        make_xunit_perf(script_args.xunit_perf_path)

        base_runid = script_args.runid + '.base'
        test_runid = script_args.runid + '.test'
        out_html = os.path.join(SCRIPT_ROOT_PATH, script_args.runid + '.html')

        run_perf_test(test_runid, script_args.test_cli, script_args.xunit_perf_path)
        if script_args.base_cli != None:
            run_perf_test(base_runid, script_args.base_cli, script_args.xunit_perf_path)
            compare_results(base_runid, test_runid, out_html, script_args.xunit_perf_path)

        return 0

    except FatalError as error:
        print("! ERROR: {msg}".format(msg = error.message))
        return 1

if __name__ == "__main__":
    sys.exit(main())
