Filing issues for .NET Core CLI
===============================

As you may notice based on our issues page, the CLI repo is what is known as a 
"high-profile" and "high-volume" repo; we 
get a lot of issues. This, in turn, may mean that some issues get 
lost in the noise and/or are not reacted on with the needed speed. 

In order to help with the above situation, we need to have a certain way to file 
issues in order for the core team of maintainers can react as fast as (humanly)
possible.

The below steps are something that we believe is not a huge increase in process, 
but would help us react much faster to any issues that are filed. 

1. Check if the [known issues](known-issues.md) cover the issue you are running 
into. We are collecting issues that are known and that have workarounds, so it 
could be that you can get unblocked pretty easy. 

2. Add a label to determine which type of issue it is. If it is a defect, use 
the "bug" label, if it is a suggestion for a feature, use the "enhancement" 
label. This helps the team get to defects more effectively. 

3. Unless you are sure in which milestone the issue falls into, leave it blank.

4. If you don't know who is on point to fix it or should be on point, assign it 
first to @blackdwarf and he will triage it from there. 

5. /cc the person that the issue is assigned to (or @blackdwarf) so that person 
would get notified. In this way

6. For bugs, please be as concrete as possible on what is working, what 
is not working. Things like operating system, the version of the tools, the 
version of the installer and when you installed all help us determine the 
potential problem and allows us to easily reproduce the problem at hand.

7. For enhancements please be as concrete as possible on what is the addition 
you would like to see, what scenario it covers and especially why the current 
tools cannot satisfy that scenario. 

Thanks and happy filing! :)
