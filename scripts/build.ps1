param([string]$WebViewPackage,[string]$InnoCompiler,[switch]$SkipInstaller)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=Split-Path $PSScriptRoot -Parent
$version='2.4.0-beta.1'
$build=Join-Path $root '.build'
$dist=Join-Path $root 'dist'
$payload=Join-Path $build 'payload'
New-Item -ItemType Directory -Path $build,$dist -Force | Out-Null
if(Test-Path -LiteralPath $payload){throw 'Build payload already exists. Use a clean checkout or move .build/payload aside before building.'}
New-Item -ItemType Directory -Path $payload | Out-Null
if(!$WebViewPackage){
    $WebViewPackage=Join-Path $build 'webview2.zip'
    [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing -Uri 'https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/1.0.4258.31/microsoft.web.webview2.1.0.4258.31.nupkg' -OutFile $WebViewPackage
}
$expected='56F7F4B8BF9AEE4B8EFEFBBDD4F67D5F74EBD1B100ED0806DA71BF76AF481AA9'
if((Get-FileHash -LiteralPath $WebViewPackage -Algorithm SHA256).Hash -ne $expected){throw 'WebView2 package SHA256 mismatch'}
$sdk=Join-Path $build 'webview2'
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path -LiteralPath $WebViewPackage).Path,$sdk)
Copy-Item -LiteralPath (Join-Path $sdk 'lib\net462\Microsoft.Web.WebView2.Core.dll'),(Join-Path $sdk 'lib\net462\Microsoft.Web.WebView2.WinForms.dll'),(Join-Path $sdk 'runtimes\win-x64\native\WebView2Loader.dll') -Destination $payload
Copy-Item -LiteralPath (Join-Path $sdk 'LICENSE.txt') -Destination (Join-Path $payload 'WebView2-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $sdk 'NOTICE.txt') -Destination (Join-Path $payload 'WebView2-NOTICE.txt')
Copy-Item -LiteralPath (Join-Path $root 'assets\Workshop.ico'),(Join-Path $root 'README.md'),(Join-Path $root 'LICENSE') -Destination $payload
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$exe=Join-Path $payload 'Enshrouded Workshop.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$exe" "/win32icon:$root\assets\Workshop.ico" "/win32manifest:$root\src\app.manifest" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll "/r:$payload\Microsoft.Web.WebView2.Core.dll" "/r:$payload\Microsoft.Web.WebView2.WinForms.dll" "$root\src\Workshop.cs" "$root\src\NexusBrowser.cs" "$root\src\ServerCatalog.cs" "$root\src\AssemblyInfo.cs"
if($LASTEXITCODE -ne 0){throw 'C# compilation failed'}
[IO.File]::WriteAllText(($exe+'.config'),'<?xml version="1.0"?><configuration><startup><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" /></startup></configuration>')
$previousData=$env:ENSHROUDED_WORKSHOP_DATA_DIR
try{
    $env:ENSHROUDED_WORKSHOP_DATA_DIR=Join-Path $build 'test-data'
    $report=Join-Path $build 'test-results.txt'
    $process=Start-Process -FilePath $exe -ArgumentList ('/selftest "'+$report+'"') -Wait -PassThru
    Get-Content -LiteralPath $report
    if($process.ExitCode -ne 0 -or !(Select-String -LiteralPath $report -SimpleMatch 'ALL TESTS PASSED' -Quiet)){throw 'Tests failed'}
}finally{$env:ENSHROUDED_WORKSHOP_DATA_DIR=$previousData}
$zip=Join-Path $dist "Enshrouded-Server-Workshop-$version-portable-win-x64.zip"
[IO.Compression.ZipFile]::CreateFromDirectory($payload,$zip)
if(!$SkipInstaller){
    if(!$InnoCompiler){$InnoCompiler=Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'}
    if(!(Test-Path -LiteralPath $InnoCompiler)){throw 'Inno Setup compiler not found. Supply -InnoCompiler or use -SkipInstaller.'}
    & $InnoCompiler "/DAppVersion=$version" "/DPayloadDir=$payload" "/DReleaseDir=$dist" (Join-Path $root 'installer\Workshop.iss')
    if($LASTEXITCODE -ne 0){throw 'Installer compilation failed'}
}
Get-ChildItem -LiteralPath $dist -File | Where-Object {$_.Name -ne 'SHA256SUMS.txt'} | ForEach-Object { '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(),$_.Name } | Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Encoding ASCII
