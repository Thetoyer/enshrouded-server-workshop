param([string]$Destination=(Join-Path $env:TEMP 'workshop-inno'))
$ErrorActionPreference='Stop'
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
$installer=Join-Path $Destination 'inno-setup.exe'
Invoke-WebRequest -UseBasicParsing 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $installer
if((Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash -ne '9C73C3BAE7ED48D44112A0F48E66742C00090BDB5BEF71D9D3C056C66E97B732'){throw 'Inno Setup SHA256 mismatch'}
if((Get-AuthenticodeSignature -LiteralPath $installer).Status -ne 'Valid'){throw 'Inno Setup signature validation failed'}
$process=Start-Process -FilePath $installer -ArgumentList ('/PORTABLE=1 /CURRENTUSER /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /NOICONS /DIR="'+$Destination+'\compiler"') -Wait -PassThru
if($process.ExitCode -ne 0){throw 'Inno Setup portable extraction failed'}
Join-Path $Destination 'compiler\ISCC.exe'
