# Manual Testing

This project contains a simple console application that uses the MSI APIs to install an MSI. You can run the application using the command line below

```Microsoft.Win32.Mist.ManualTest.exe install <PATH_TO_YOUR_MSI>```

If the MSI you provide installs per-machine, run the application from an elevated prompt. The test application will not handle elevation. It will report
an error if you don't have sufficient privileges.

Additionally, the application uses the external callback features provided by Windows Installer to display a simple progress bar in the console window.

It will also generate an install log file, e.g. ```%temp%\MSInnnn.LOG```.
