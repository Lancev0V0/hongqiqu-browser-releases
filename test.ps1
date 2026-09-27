param([string]$OutputDirectory = (Join-Path $PSScriptRoot '.build\tests'))
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$exe=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'UpdateTests.exe'
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /target:exe /platform:x64 ('/out:'+$exe) /r:System.Net.Http.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'src\UpdateEngine.cs') (Join-Path $PSScriptRoot 'tests\UpdateTests.cs')
if($LASTEXITCODE -ne 0){throw 'Test compilation failed'}
& $exe
if($LASTEXITCODE -ne 0){throw 'Update tests failed'}
