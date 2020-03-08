# runfo

This is an abbreviation for "runtime info." It's a tool that provides quick 
summary status of builds from the dotnet/runtime repository.

## Authentication
In order to use the `tests` command you will need to provide a personal access
token to the tool. These can be obtained by visitting the following site:

- https://dnceng.visualstudio.com/_usersSettings/tokens

The token can be passed to the tool in two ways:

1. By using the `-token` command line argument
2. Using the `%RUNFO_AZURE_TOKEN%` environment variable

## Commands

### status
Dumps out the info from all of our CI jobs and their current  pass / fail state. 

```
P:\random\dotnet\Azure\runfo> runfo status
runtime                0%  NNNNN
coreclr                0%  NNNNN
libraries              0%  NNNNN
libraries windows      0%  NNNNN
libraries linux        0%  NNNNN
libraries osx          0%  NNNNN
crossgen2              0%  NNNNN
```

It is handy for showing the friendly name of all the build definitions that are
being tracked.

### tests
This dumps the test information for the provided build definition (default to 
last five CI builds). Essentially it will enumerate the last five builds for a
given definition and dump all of the data for it with a custom grouping.

Example command for getting a quick run down on test failures in dotnet/runtime

``` cmd
> runfo tests -d runtime -v 
```

Supported options:

| Option | Description |
| --- | --- | 
| grouping | how to group the test output: builds, tests or jobs |
| definition | Build definition to print for. Can be number or friendly name |
| count | Count of builds to see data for | 
| verbose | Verbose output |

The most interesting flag though is -g for grouping. This lets you group the
test failures by different categories. Example of where this can be useful: 

```cmd
P:\> runfo tests -d runtime -c 5 -g jobs
netcoreapp5.0-Linux-Release-arm64-CoreCLR_release-(Alpine.38.Arm64.Open)Ubuntu.1804.ArmArch.Open@mcr.microsoft.com/dotnet-buildtools/prereqs:alpine-3.8-helix-arm64v8-a45aeeb-20190620184035
  Builds 5
  Test Cases 18
netcoreapp5.0-Linux-Release-arm64-CoreCLR_release-(Ubuntu.1804.ArmArch.Open)Ubuntu.1804.ArmArch.Open@mcr.microsoft.com/dotnet-buildtools/prereqs:ubuntu-16.04-helix-arm64v8-bfcd90a-20200127194925
  Builds 4
  Test Cases 16
<lots of clipped data> 
```

Notice that the Alpine.38.Arm64 job failed on 5 builds which is also the 
amount we’re limiting the results too. So pretty good bet this configuration is 
busted in some way that requires investigation. 

### helix
This command dumps all of the console and core URIs for a given build. Using
-v you can also get it to dump the console log content directly to the console
(instead of having to click through the output):

```
P:> runfo helix -b 505640
Console Logs
https://helix.dot.net/api/2019-06-17/jobs/6afd25d6-b672-4525-bcb3-92be7581046a/workitems/System.Security.Cryptography.OpenSsl.Tests/files/console.929d7000.log
https://helix.dot.net/api/2019-06-17/jobs/3cd49f06-a2f6-4a87-bda4-d33be9b16f83/workitems/System.Runtime.Tests/files/console.7fdd181f.log
```

Going to change tests to have this info soon as well. 

## FAQ
### Why the name runfo?
I’m terrible at naming things. The tool was meant for “Runtime Information” so 
I shortened it to runfo cause I’m bad at naming.

### Why this over the CI Council Dashboard
CI council dashboard is meant to represent overall repository CI health. It 
takes into account bigger issues like publishing, core-eng infra issues, etc … 
It also only follows a subset of the build definitions that we maintain. I
wanted a quick and dirty tool for looking at test failures only as that’s the
biggest source of flakiness that we directly control as a team.

### I looked at the code and you should be ashamed of the hackery!
A: Indeed I am ashamed. I’ve used more SelectMany calls and Tuples in this 
tool as I did in the rest of my career combined :smile:



