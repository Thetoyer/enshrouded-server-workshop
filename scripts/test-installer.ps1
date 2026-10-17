param([Parameter(Mandatory=$true)][string]$InnoCompiler)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$test=Join-Path $root '.build\installer-test'
New-Item -ItemType Directory -Path $test -Force | Out-Null
# Exercise the same file installation and uninstaller without changing the
# host's registry, Start menu, or desktop in CI/sandbox environments.
& $InnoCompiler '/DInstallerTest' '/DAppVersion=2.4.0-beta.1' ('/DPayloadDir='+$root+'\.build\payload') ('/DReleaseDir='+$test) (Join-Path $root 'installer\Workshop.iss')
if($LASTEXITCODE -ne 0){throw 'Test installer build failed'}
$installer=Join-Path $test 'Enshrouded-Server-Workshop-2.4.0-beta.1-Setup-win-x64.exe'
$dir=Join-Path $test 'installed'
$process=Start-Process -FilePath $installer -ArgumentList ('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR="'+$dir+'" /LOG="'+$test+'\install.log"') -Wait -PassThru
if($process.ExitCode -ne 0){throw "Installer exit $($process.ExitCode). See $test\install.log"}
$oldData=$env:ENSHROUDED_WORKSHOP_DATA_DIR
try{
    $env:ENSHROUDED_WORKSHOP_DATA_DIR=Join-Path $test 'user-data'
    $report=Join-Path $test 'application-tests.txt'
    $process=Start-Process -FilePath (Join-Path $dir 'Enshrouded Workshop.exe') -ArgumentList ('/selftest "'+$report+'"') -Wait -PassThru
    if($process.ExitCode -ne 0){throw 'Installed application tests failed'}
    Get-Content -LiteralPath $report
}finally{$env:ENSHROUDED_WORKSHOP_DATA_DIR=$oldData}
$process=Start-Process -FilePath (Join-Path $dir 'unins000.exe') -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -Wait -PassThru
if($process.ExitCode -ne 0 -or (Test-Path -LiteralPath (Join-Path $dir 'Enshrouded Workshop.exe'))){throw 'Uninstall smoke test failed'}
if(!(Test-Path -LiteralPath (Join-Path $test 'user-data'))){throw 'User data was not preserved'}
'PASS: installer file deployment, installed application, uninstall and user-data preservation. OS shortcut/registry registration is not tested by this sandbox-compatible variant.'
