#!/usr/bin/perl

use strict;
use Getopt::Long;
use Data::Dumper;
use File::Path qw(make_path);
use File::Spec::Functions qw(rel2abs catfile canonpath);
use File::Basename;
use FindBin qw($RealBin);
use Cwd qw(abs_path);
use POSIX;

#set the timestamp in case we need to create a directory
use constant DATETIME=>strftime("%Y-%m-%d_%H-%M-%S", localtime);

# The source solution
my $solutionToBuild = catfile($RealBin, 'build.proj');

my $usage = <<"USAGE";
Usage build.pl [-root=<outputRoot>] [-fullbuild] [-verify] [-tests] [-all] [-quiet] [-silent]

The script can build MSBuild.exe using mono, verify by
rebuilding with the generated MSBuild.exe and run tests.
If the output root is not specified, the bin and packages
subdirectories are created in the source tree (as well as
bin-verify and packages-verify if verification build runs).
If output root is provided, a dated subdirectory is created
under the output root and the same subdirectories appear
there. The tests are run either on the xbuild generated
binaries or, if verification is done, on the verification
binaries.

Options:
 root      - specifies the output root. The root is the
             source tree by default.
 fullBuild - do a rebuild instead of the incremental build.
             This only makes a difference without the root
             options, as the rooted build are always clean.
 verify    - rebuild the source with the binaries generated
             by the xbuild.
 tests     - run the tests.
 package   - create NuGet package
 testName  - name of the nunit test to run (used with -tests).
             Use full name, including the namespace.
 fixture   - name of the nunit test fixture to be run (used
             with -tests)
 all       - shorthand for -verify and -tests flags.
 quiet     - don't show any build output, only summaries
 silent    - show nothing, not even summaries

USAGE


# Quick check if we are in the right place and that mono is installed
# (just checking for solution and xbuild presence)
my $xbuild = ($^O eq 'MSWin32') ? `where xbuild` : `which xbuild`;
chomp $xbuild;
die ("Solution $solutionToBuild does not exist\n") unless -e $solutionToBuild;
die ("xbuild was not found") unless $^O eq "MSWin32" || -e $xbuild;

my $buildRoot;
my $runTests;
my $testName;
my $fixture;
my $verification;
my $quiet;
my $silent;
my $createPackage;
my $fullBuild;
my $allSteps;
my $help;

die $usage unless GetOptions(
                             'root=s' => \$buildRoot,
                             'verify' => \$verification,
                             'tests' => \$runTests,
                             'package' => \$createPackage,
                             'testName=s' => \$testName,
                             'fixture=s' => \$fixture,
                             'quiet' => \$quiet,
                             'silent' => \$silent,
                             'fullBuild' => \$fullBuild,
                             'all' => \$allSteps,
                             'help' => \$help
                            );

if ($help) {
        print $usage;
        exit (0);
}

# The all steps flag override some other
if ($allSteps) {
    $verification = 1;
    $runTests = 1;
}

# We need nunit-console to run tests
my $nunitConsole;
if ($runTests) {
    # Find the nunit console program
    my $n = ($^O eq 'MSWin32') ? `where nunit-console` : `which nunit-console`;
    chomp $n;
    
    die ("Tests are requested, but nunit-console was not found") unless -e $n;
    
    # Resolve any links
    $nunitConsole = abs_path($n);
    # Use version 4 if found
    $n = $nunitConsole . '4';
    $nunitConsole = $n if -e $n;
}

my $slash;
my $installedBuild;

if ($^O eq "MSWin32") {
    # Find the nunit console program
    my @n = `where msbuild`;
    print "For MSBuild: got @n[0]\n";
    chomp @n;
    print "Chomped MSBuild: got @n[0]\n";
    
    die ("Installed MSBuild was not found") unless -e @n[0];
   
    # Resolve any links
    $installedBuild = abs_path(@n[0]);
    $slash = '\\';
}
else {
    $installedBuild = $xbuild;
    $slash = '/';
}

# Find the location where we're going to store results
# If no root is specifed, use the script location (we'll create
# bin, packages, bin-verify, packages-verify directories and the
# logs there.
# If root is specified, make a dated subdirectory there. That
# will be our root.
if (!$buildRoot) {
    $buildRoot = canonpath($RealBin);
}
else {
    $buildRoot = catfile(rel2abs($buildRoot), DATETIME);
    # Just make sure it's not a file
    die ("Verification root '$buildRoot' exists and is a file") if -f $buildRoot;
    make_path($buildRoot);
}

# The regex to parse build logs
(my $tmpRoot = $buildRoot) =~ s!\\!\\\\!g;
print "buildRoot: $buildRoot, tmpRoot: $tmpRoot\n";
my $extractRegex = qr!^\s*Build binary target directory: '($tmpRoot.+?)[\\/]?'!;

my $msbuildPath;
my $exitCode;
my $errorCount;

# Run the first build
($exitCode, $errorCount, $msbuildPath) = runbuild("\"$installedBuild\"", '', '/', ($^O ne "MSWin32"));

die ("Build with xbuild failed (code $exitCode)") unless $exitCode == 0;
die ("Build with xbuild failed (error count $errorCount") unless $errorCount == 0;
die ("Build succeeded, but MSBuild.exe binary was not found at $msbuildPath") unless -e $msbuildPath;

# Use the MSBuild.exe we created and rebuild (if requested)
if ($verification) {
    my $MSBuildProgram = catfile($msbuildPath, 'MSBuild.exe');
    $MSBuildProgram = 'mono ' . "\"$MSBuildProgram\"" unless $^O eq "MSWin32";
    my $newMSBuildPath;
    ($exitCode, $errorCount, $newMSBuildPath) = runbuild($MSBuildProgram, '-verify', '-', 0);
    die ("Build with msbuild failed (code $exitCode)") unless $exitCode == 0;
    die ("Build with msbuild failed (error count $errorCount") unless $errorCount == 0;
    print "New MSBuild path: $newMSBuildPath\n";
    $msbuildPath = $newMSBuildPath if -e $newMSBuildPath;
}

if ($runTests) {
    # Get the dlls for testing
    my @file = glob catfile($msbuildPath, '*UnitTest*.dll');
    runtests (@file);
}

# This functions runs a build.
# runbuild(program, suffix, switch)
#   program -- the build (xbuild or msbuild)
#   suffix -- appended to log and output directory names
#   switch -- either - or /
sub runbuild {
    die ('runbuild sub was not called correctly') unless @_ == 4;
    my ($program, $suffix, $switch, $overrideToolset) = @_;

    # Get paths of output directories and the log
    (my $binDir = catfile($buildRoot, "bin$suffix")) =~ s:(?<![\/])$:$slash:;
    #$binDir =~ s:(?<![\/])$:\\:;
    print "BinDir = $binDir\n";
    (my $packagesDir = catfile ($buildRoot, "packages$suffix")) =~ s:(?<![\/])$:$slash:;
    my $logFile = catfile($buildRoot, "MSBuild${suffix}.log");

    # If we need to rebuild, add a switch for the task
    my $rebuildSwitch = $fullBuild ? "${switch}t:Rebuild " : "";

    # If we need to create NuGet package, add a witch for the property
    my $packageProperty = $createPackage ? "${switch}p:BuildNugetPackage=true " : "";

    # Except on Windows, we need to specifiy 4.0 toolse
    my $toolSet = $overrideToolset ? "${switch}tv:4.0 " : "";
    my $configSwitch = $^O eq "MSWin32" ? "${switch}p:Configuration=Debug " : "${switch}p:Configuration=Debug-MONO ";
    
    # Generate and print the command we run
    my $command = "$program ${switch}nologo ${switch}v:q " .
                  "$rebuildSwitch $configSwitch $toolSet $packageProperty" .
                  "${switch}p:BinDir=$binDir ${switch}p:PackagesDir=$packagesDir " .
                  "${switch}fl \"${switch}flp:LogFile=$logFile;V=diag\" ${switch}p:BuildSamples=false " .
                  " $solutionToBuild";
    print $command . "\n" unless $silent;

    # Run build, parsing it's output to count errors and warnings
    # Harakiri if can't run
    open(BUILD_OUTPUT, "$command 2>&1 |") or die "Cannot run $program, error $!";
    my $warningCount = 0;
    my $errorCount = 0;
    for (<BUILD_OUTPUT>) {
        print $_ unless ($quiet || $silent);
        m/:\s+error / && ($errorCount++, next);
        m/:\s+warning / && ($warningCount++, next);
    }

    die "Failed to run $program, exit code $!" if $! != 0;
    close BUILD_OUTPUT;
    my $exitCode = $? >> 8;

    # Search the log for the full output path
    my $msbuildPath;
    if (open LOG, '<', $logFile) {
        m/$extractRegex/ && ($msbuildPath = $1, last) for <LOG>;
        close (LOG);
    }
    else {
        # It's not an error if the path cannot be found. At worst, we cannot verify
        print "Warning: Cannot open log file $logFile: $!" if $! && !$silent;
    }

    print "Errors: $errorCount, Warnings $warningCount\n" unless $silent;
    return ($exitCode, $errorCount, $msbuildPath);
}

# This function runs the test. It gets the list of dlls to test.
sub runtests {
    my @files = @_;

    # Create directory for output
    my $testResultsDir = catfile($buildRoot, 'TestResults');
    make_path($testResultsDir);

    # Output file names in that directory
    my $xmlResultFile = catfile($testResultsDir, 'Results.xml');
    my $outputFile = catfile($testResultsDir, 'TestOutput.txt');

    # Build the command to run the test
    my $command = '';
    my $excludeCategories = '';
    if ($^O ne 'MSWin32') {
        $excludeCategories = "WindowsOnly";
    }
    if ($testName) {
        $command = "\"$nunitConsole\" -run:$testName -xml:$xmlResultFile " . join (' ', @files);
    } elsif ($fixture) {
        $command = "\"$nunitConsole\" -fixture:$fixture -exclude:$excludeCategories -xml:$xmlResultFile " . join (' ', @files);
    } else {
        $command = "\"$nunitConsole\" -exclude:$excludeCategories -xml:$xmlResultFile " . join (' ', @files);
    }
    print $command . "\n" unless $silent;

    # Run it silently
    system("$command 2>&1 >$outputFile");

    # Count the passed/failed tests by readin the output XML file
    my $testsFailed = 0;
    my $testsSucceeded = 0;
    
    my $testRegex = qr!^\s*<test-case.+executed="True".+success="((?:True)|(?:False))"!;

    if (open LOG, '<', $xmlResultFile) {
        m/$testRegex/ && ($1 eq 'True' ? $testsSucceeded++ : $testsFailed++) for <LOG>;
        close (LOG);
        my $testsRan = $testsSucceeded + $testsFailed;
        print "Tests ran: $testsRan, tests succeeded: $testsSucceeded, tests failed: $testsFailed\n" unless $silent;
    }
    else {
        print "Warning: Cannot open log file $xmlResultFile: $!\n" if $! && !$silent;
    }
}
