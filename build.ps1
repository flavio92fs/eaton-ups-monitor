param([switch]$Test, [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Requires Windows x64 and .NET Framework 4.8.' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$dist = (Resolve-Path -LiteralPath $OutputDirectory).Path
$sources = @(Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter *.cs | ForEach-Object FullName)
$arguments = @('/nologo','/target:winexe','/platform:x64','/optimize+','/codepage:65001',
    '/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Net.Http.dll','/r:System.Web.Extensions.dll','/r:System.Security.dll',
    ('/win32icon:' + (Join-Path $PSScriptRoot 'assets\eaton-monitor.ico')),
    ('/resource:' + (Join-Path $PSScriptRoot 'README.md') + ',EatonMonitor.Readme'),
    ('/resource:' + (Join-Path $PSScriptRoot 'README.it.md') + ',EatonMonitor.ReadmeIt'),
    ('/resource:' + (Join-Path $PSScriptRoot 'LICENSE') + ',EatonMonitor.License'),
    ('/out:' + (Join-Path $dist 'EatonMonitor.exe')))
& $compiler @arguments @sources
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
Copy-Item -LiteralPath (Join-Path $dist 'EatonMonitor.exe') -Destination (Join-Path $dist 'EatonMonitorSetup.exe') -Force
foreach ($name in @('README.md','README.it.md','LICENSE','CHANGELOG.md')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $dist -Force
}
if ($Test) {
    $testArguments = @('/nologo','/target:exe','/platform:x64','/codepage:65001',
        '/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Net.Http.dll','/r:System.Web.Extensions.dll','/r:System.Security.dll',
        '/main:TelemetryTests',('/out:' + (Join-Path $dist 'Tests.exe')))
    & $compiler @testArguments @sources (Join-Path $PSScriptRoot 'tests\TelemetryTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    & (Join-Path $dist 'Tests.exe') (Join-Path $PSScriptRoot 'tests\fixtures\descriptor.txt') (Join-Path $PSScriptRoot 'tests\fixtures\reports.json')
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
$zip = Join-Path $dist 'EatonMonitor-3.4.0-windows-x64.zip'
$package = @('EatonMonitor.exe','EatonMonitorSetup.exe','README.md','README.it.md','LICENSE','CHANGELOG.md') | ForEach-Object { Join-Path $dist $_ }
Compress-Archive -LiteralPath $package -DestinationPath $zip -Force
Get-FileHash -Algorithm SHA256 -LiteralPath $zip | Select-Object Hash,Path
