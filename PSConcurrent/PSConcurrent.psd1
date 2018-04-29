<#
    Copyright (C) 2018 Jeffrey Sharp

    Permission to use, copy, modify, and distribute this software for any
    purpose with or without fee is hereby granted, provided that the above
    copyright notice and this permission notice appear in all copies.

    THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
    WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
    MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
    ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
    WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
    ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
    OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
#>
@{
    # Identity
    GUID          = 'e228c9f6-e66e-4168-a1ad-3556746d4f06'
    RootModule    = 'PSConcurrent.dll'
    ModuleVersion = '1.0.0'

    # General
    Author      = 'Jeffrey Sharp'
    CompanyName = 'Jeffrey Sharp'
    Copyright   = 'Copyright (C) 2018 Jeffrey Sharp'
    Description = 'Provides the Invoke-Concurrent cmdlet, which can run a set of ScriptBlocks concurrently, up to a configurable maximum concurrency level.'

    # Requirements
    PowerShellVersion      = '5.0'
    #CompatiblePSEditions  = @("Desktop")   # Added in PowerShell 5.1
    DotNetFrameworkVersion = '4.5.2'        # Valid for Desktop edition only
    CLRVersion             = '4.0'          # Valid for Desktop edition only

    # Exports
    # NOTE: Use empty arrays to indicate no exports.
    FunctionsToExport    = @()
    CmdletsToExport      = @("Invoke-Concurrent")
    VariablesToExport    = @()
    AliasesToExport      = @()
    DscResourcesToExport = @()

    # Discoverability and URLs
    PrivateData = @{
        PSData = @{
            Tags = @("Concurrent", "Parallel", "Thread", "Invoke", "Foreach")
            LicenseUri = 'https://github.com/sharpjs/PSConcurrent/blob/master/LICENSE.txt'
            ProjectUri = 'https://github.com/sharpjs/PSConcurrent'
            # IconUri = ''
            ReleaseNotes = @"
Release notes are available at:
https://github.com/sharpjs/PSConcurrent/releases
"@
        }
    }
}
