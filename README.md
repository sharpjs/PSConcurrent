# PSConcurrent

An `Invoke-Concurrent` cmdlet for PowerShell.

Available in the [PowerShell Gallery](https://www.powershellgallery.com/packages/PSConcurrent).

## Status

**Release Candidate.**

## Installation

* Ensure that you have the latest version of the
  [PowerShellGet](https://docs.microsoft.com/en-us/powershell/gallery/psget/get_psget_module)
  module.
* Run this command:
  ```powershell
  Install-Module -Name PSConcurrent -AllowPrerelease
  ```
* Restart PowerShell.

## Usage

At its most basic, Invoke-Concurrent runs a set of script blocks ... well,
concurrently.

```powershell
Invoke-Concurrent {echo a}, {echo b}, {echo c}
```
```
[Task 1]: Starting
[Task 2]: Starting
[Task 3]: Starting
[Task 2]: Ended
[Task 1]: Ended
[Task 3]: Ended

TaskId Object
------ ------
     2 b
     1 a
     3 c
```

You can also pipe the script blocks to Invoke-Concurrent:

```powershell
{echo a}, {echo b}, {echo c} | Invoke-Concurrent
```

When a script block writes content to the error, warning, information, verbose,
or debug streams, or directly to the host, Invoke-Concurrent writes the content,
but adds text to identify which "task" (script block) sent it.  When a script
block writes an object to the output stream, Invoke-Concurrent wraps the object
in a container whose `TaskId` property identifies the task that sent the object.
Task ids start at 1.

The script blocks run simultaneously at a level of concurrency determined by the
[.NET managed thread pool](https://docs.microsoft.com/en-us/dotnet/standard/threading/the-managed-thread-pool)
— usually, the number of virtual processors in the system.  You can provide an
explicit limit on the number of concurrently-running script blocks with the
`-MaxConcurrency` parameter.

```powershell
Invoke-Concurrent {echo a}, {echo b}, {echo c} -MaxConcurrency 2
```
```
[Task 1]: Starting
[Task 2]: Starting
[Task 1]: Ended
[Task 3]: Starting
[Task 2]: Ended
[Task 3]: Ended

TaskId Object
------ ------
     1 a
     2 b
     3 c
```

By default, script blocks do not see variables and modules from the PowerShell
session that runs Invoke-Concurrent.  To change that, you can export specific
variables and modules with the `-Variable` and `-Module` parameters.

```powershell
$A = "Hocus"
$B = "pocus"
Import-Module PSMagic

Invoke-Concurrent { Write-Host "$A $B" }, { Use-MagicWand } `
    -Variable (Get-Variable A, B) `
    -Module   (Get-Module PSMagic)
```
```
[Task 1]: Starting
[Task 2]: Starting
[Task 1]: Hocus pocus
[Task 1]: Ended
[Task 2]: Ended

TaskId Object
------ ------
     2 PSMagic.Models.WandResult
```
