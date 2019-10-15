Filing issues for .NET Core CLI
===============================

As you may notice based on our issues page, the CLI repo is what is known as a 
"high-profile" and "high-volume" repo; we 
get a lot of issues. This, in turn, may mean that some issues get 
lost in the noise and/or are not reacted on with the needed speed. 

In order to help with the above situation, we need to have a certain way to file 
issues so that the core team of maintainers can react as fast as 
possible and can triage effectively. 

The below steps are something that we believe is not a huge increase in process, 
but would help us react much faster to any issues that are filed. 

1. Check if the [known issues](https://github.com/dotnet/core/blob/master/release-notes/2.0/2.0.0-known-issues.md) cover the issue you are running 
into. We are collecting issues that are known and that have workarounds, so it 
could be that you can get unblocked pretty easily. 

4. /cc the person that the issue should be assigned to (or @blackdwarf) so that person 
would get notified. In this way the correct person can immediately jump on the 
issue and triage it.

5. For bugs, be as concrete as possible on what is working, what 
is not working. Things like operating system, the version of the tools, the 
version of the installer and when you installed all help us determine the 
potential problem and allows us to easily reproduce the problem at hand.

6. For enhancements be as concrete as possible on what is the addition 
you would like to see, what scenario it covers and especially why the current 
tools cannot satisfy that scenario. 

Thanks and happy filing! :)

## Providing the repro for bugs
For bugs, what we need more than anything is a good repro of the defective 
behavior. We would like to go towards the "clone, run, repro" model. In short:

1. If you find a bug, package up a repro in a git repo somewhere (GitHub is 
usually a good place :)). 
2. Inside the issue, specify what needs to be done (steps) to get an accurate 
repro of the bug. Ideally, this should be "here is how to build, these are the 
commands you run from the dotnet tools".
3. We use the above to get the repro, investigate and fix!

