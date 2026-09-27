param(
  [string]$Version,
  [string]$FirefoxInstaller,
  [string]$NsisPath,
  [string]$SevenZipPath = 'C:\Program Files\7-Zip\7z.exe',
  [string]$BuildRoot = (Join-Path $PSScriptRoot '.build'),
  [switch]$CompileOnly
)
$ErrorActionPreference = 'Stop'
$config = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'build-config.json') -Raw | ConvertFrom-Json
if (-not $Version) { $Version = $config.version }
if ($Version -notmatch '^\d{1,5}\.\d{1,5}\.\d{1,5}$') { throw 'Version must be X.Y.Z' }
$BuildRoot = [IO.Path]::GetFullPath($BuildRoot)
New-Item -ItemType Directory -Path $BuildRoot -Force | Out-Null
$run = Join-Path $BuildRoot ($Version + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
$stage = Join-Path $run 'stage'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
$utf8 = New-Object Text.UTF8Encoding($false)
$assembly = @"
using System.Reflection;
[assembly: AssemblyTitle("红旗渠爱国浏览器")]
[assembly: AssemblyProduct("红旗渠爱国浏览器")]
[assembly: AssemblyDescription("独立主题、应用更新与 Windows 启动器")]
[assembly: AssemblyVersion("$Version.0")]
[assembly: AssemblyFileVersion("$Version.0")]
"@
$assemblyPath = Join-Path $run 'AssemblyInfo.cs'
[IO.File]::WriteAllText($assemblyPath,$assembly,$utf8)
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$exe = Join-Path $stage 'HongqiBrowser.exe'
$argsList = @('/nologo','/target:winexe','/platform:x64','/optimize+',('/out:'+$exe),('/win32icon:'+(Join-Path $PSScriptRoot 'app\app.ico')),('/win32manifest:'+(Join-Path $PSScriptRoot 'src\app.manifest')))
$argsList += @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Net.Http.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Web.Extensions.dll','Microsoft.CSharp.dll') | ForEach-Object { '/r:'+$_ }
$argsList += (Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs').FullName
$argsList += $assemblyPath
& $csc @argsList
if ($LASTEXITCODE -ne 0) { throw 'C# compilation failed' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'app\app.ico') -Destination $stage
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'app\theme') -Destination $stage -Recurse
[IO.File]::WriteAllText((Join-Path $stage 'version.txt'),$Version,$utf8)
if ($CompileOnly) { Write-Output $stage; exit 0 }
if (-not (Test-Path -LiteralPath $SevenZipPath)) { throw 'Install 7-Zip or supply -SevenZipPath.' }
if (-not $FirefoxInstaller) {
  $FirefoxInstaller = Join-Path $BuildRoot ('firefox-'+$config.firefoxVersion+'.exe')
  if (-not (Test-Path -LiteralPath $FirefoxInstaller)) {
    $url = 'https://download-installer.cdn.mozilla.net/pub/firefox/releases/'+$config.firefoxVersion+'/win64/zh-CN/Firefox%20Setup%20'+$config.firefoxVersion+'.exe'
    Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $FirefoxInstaller
  }
}
$FirefoxInstaller = (Resolve-Path -LiteralPath $FirefoxInstaller).Path
if ((Get-FileHash -LiteralPath $FirefoxInstaller -Algorithm SHA512).Hash -ne $config.firefoxSha512) { throw 'Mozilla installer SHA512 mismatch' }
$signature = Get-AuthenticodeSignature -LiteralPath $FirefoxInstaller
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Mozilla Corporation') { throw 'Mozilla signature verification failed' }
$extracted = Join-Path $run 'firefox-extracted'
& $SevenZipPath x $FirefoxInstaller ('-o'+$extracted) -y | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Firefox extraction failed' }
Move-Item -LiteralPath (Join-Path $extracted 'core') -Destination (Join-Path $stage 'runtime')
$runtime = Join-Path $stage 'runtime'
$runtimeSignature = Get-AuthenticodeSignature -LiteralPath (Join-Path $runtime 'firefox.exe')
if ($runtimeSignature.Status -ne 'Valid') { throw 'Firefox executable signature verification failed' }
New-Item -ItemType Directory -Path (Join-Path $runtime 'distribution') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $runtime 'distribution\policies.json'),'{"policies":{"DisableAppUpdate":true,"DisableTelemetry":true,"DisableDefaultBrowserAgent":true}}',$utf8)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.md') -Destination $stage
if (-not $NsisPath) {
  $nsisDir = Join-Path $BuildRoot ('nsis-'+$config.nsisVersion)
  $NsisPath = Join-Path $nsisDir 'makensis.exe'
  if (-not (Test-Path -LiteralPath $NsisPath)) {
    $nsisZip = Join-Path $PSScriptRoot ('build-tools\nsis-'+$config.nsisVersion+'.zip')
    if ((Get-FileHash -LiteralPath $nsisZip -Algorithm SHA256).Hash -ne $config.nsisSha256) { throw 'NSIS tool archive SHA256 mismatch; build stopped.' }
    Expand-Archive -LiteralPath $nsisZip -DestinationPath $BuildRoot -Force
  }
}
$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$archive = Join-Path $dist ('HongqiquBrowser-'+$Version+'-win-x64.zip')
$installer = Join-Path $dist ('HongqiquBrowser-Setup-'+$Version+'-win-x64.exe')
if ((Test-Path -LiteralPath $archive) -or (Test-Path -LiteralPath $installer)) { throw 'Release files already exist; choose a new version or move the previous build aside.' }
Push-Location $stage
try { & $SevenZipPath a -tzip -mx=7 $archive '.\*' | Out-Null; if ($LASTEXITCODE -ne 0) { throw 'ZIP packaging failed' } } finally { Pop-Location }
Push-Location $PSScriptRoot
try { & $NsisPath /V2 /INPUTCHARSET UTF8 ('/DAPPVERSION='+$Version) ('/DSTAGE='+$stage) ('/DOUTFILE='+$installer) 'installer.nsi'; if ($LASTEXITCODE -ne 0) { throw 'NSIS packaging failed' } } finally { Pop-Location }
$hashes = @($installer,$archive) | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($_) }
[IO.File]::WriteAllLines((Join-Path $dist 'SHA256SUMS.txt'),$hashes,$utf8)
$report = @{version=$Version;stage=$stage;installer=$installer;archive=$archive;firefoxVersion=$config.firefoxVersion;firefoxSignature=[string]$runtimeSignature.Status;sourceInstallerSha512=$config.firefoxSha512}
$report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'build-report.json') -Encoding UTF8
$report | ConvertTo-Json
