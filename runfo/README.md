# runfo

This is an abbreviation for "runtime info." It's a tool that provides quick
summary status of builds from the dotnet/runtime repository.

## Authentication
In order to use `runfo` you will need to provide personal access
tokens to the tool. These can be obtained by visitting the following site:

- AzDO: https://dev.azure.com/dnceng/_usersSettings/tokens
- Helix: https://helix.dot.net/Account/Tokens

The tokens can be passed to the tool in two ways:

1. By saving your tokens in `%LOCALAPPDATA%/runfo/settings.txt`. Example of the file contents:

```
azdo-token=YOUR_AZDO_TOKEN
helix-token=YOUR_HELIX_TOKEN
helix-base-uri=https://helix.dot.net/
```

2. By passing the token arguments in the command line. Example:

```
runfo --azdo-token=YOUR_AZDO_TOKEN --helix-token=YOUR_HELIX_TOKEN --helix-base-uri=https://helix.dot.net/ [additional arguments]
```

3. Using the `%RUNFO_AZURE_TOKEN%` and `%RUNFO_HELIX_TOKEN%` environment variables. There is no environment variable for the helix base uri (the default `https://helix.dot.net/` will be used). Example:

```cmd
set RUNFO_AZURE_TOKEN=YOUR_AZDO_TOKEN
set RUNFO_HELIX_TOKEN=YOUR_HELIX_TOKEN

runfo [additional arguments]
```

## Build filtering
All commands that search builds use the same set of arguments to define the
set of builds being searched:

- `definition`: the build definition id or name to get builds from
- `count`: count of builds to search. Default is `5`
- `pr`: include PR builds in the search
- `project`: the project to look for builds and definitions. Default is `public`
- `after`: filter to builds after the given date
- `before`: filter to builds before the given date

The common pattern is to search the most recent hundred builds in a given
definition to find occurances of a failure. For example:

```cmd
> runfo search-timeline -d runtime -c 100 -pr -v "Central Directory Record"
```

This will search the last 100 builds from the runtime build definition for any
failures that have the text "Central Directory Record".

## Commands
These are the most common commands used to search for failures. More are
available and all have help available by using the `-help` argument.

### search-timeline
This command will search the timeline of the builds looking for a particular
piece of text. There are two ways to filter the results:

- `name`: only search records matching this name. Default is search all
records
- `value`: find records whose issues match the following string

For example here is how we commonly filtered the Docker 126 exit code issue.

```cmd
> runfo search-timeline -d runtime -c 100 -pr -v "Exit Code 126"
```

The `-markdown` argument will print the output using markdown tables.

### tests
This dumps the test information for the provided builds. Essentially it will
enumerate all of the builds and dump test failure information based on the
provided grouping. The grouping can be changed by passing the following
arguments to the `-group` switch:

- `builds`: group the failures based on the build they occurred in
- `jobs`: group the failures by the job they occurred in
- `tests`: group the failures by the test name

Example of grouping by jobs:

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

When investigating a particular test failure you can use `-name` to limit the
results to test failures matching the provided name regex.

### search-helix
This command dumps all of the console and core URIs for a given build. Using
`-value` you can also get it to dump the console log content directly to the console
(instead of having to click through the output):

```
P:> runfo search-helix -b 505640
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
Indeed I am ashamed. I’ve used more SelectMany calls and Tuples in this
tool as I did in the rest of my career combined :smile:



