# PSConcurrent

An `Invoke-Concurrent` cmdlet for PowerShell.

Available in the [PowerShell Gallery](https://www.powershellgallery.com/packages/PSConcurrent).

## Status

**PSConcurrent 1.x** has been used in production for several years.  There have
been no bug reports.

**PSConcurrent 2.x** is new but does not make significant cmdlet changes.
Rather, the update adds PowerShell Core support and 100% test coverage.

While the author strives to create bug-free software, PSConcurrent is provided
**as is** and without warranty of any kind.  For details, see the
[license and disclaimers](https://github.com/sharpjs/PSConcurrent/blob/master/LICENSE.txt).

## Requirements

PSConcurrent works with both PowerShell Core and traditional Windows PowerShell,
now known as PowerShell Desktop Edition.

For **PowerShell Core**:
* PowerShell Core 6.0 or later.

For **PowerShell Desktop**:
* PowerShell 5.1 or later.
* .NET Framework 4.6.1 or later.
* [PowerShellGet](https://docs.microsoft.com/en-us/powershell/scripting/gallery/installing-psget?view=powershell-5.1)

## Installation

To install PSConcurrent, run this command:
```powershell
Install-Module -Name PSConcurrent
```
Then restart PowerShell.

## Updating

To update PSConcurrent, run this command:
```powershell
Update-Module -Name PSConcurrent
```
Then restart PowerShell.

## Usage

### Basics

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

### Controlling Concurrency

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

### Variables and Modules

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

To export different variables and modules per script block, you can pipe parameters-as-objects to Invoke-Concurrent.

```powershell
$A = "Hocus"
$B = "pocus"
Import-Module PSMagic

$Tasks = `
    [PSCustomObject] {
        ScriptBlock = { Write-Host "$A $B" }    # arrays allowed
        Variable    = Get-Variable A, B         # arrays allowed
    },
    [PSCustomObject] {
        ScriptBlock = { Use-MagicWand }         # arrays allowed
        Module      = Get-Module PSMagic        # arrays allowed
    }

$Tasks | Invoke-Concurrent
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

