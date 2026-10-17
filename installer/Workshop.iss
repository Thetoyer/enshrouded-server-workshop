#ifndef AppVersion
  #define AppVersion "2.4.0-beta.1"
#endif
#ifndef PayloadDir
  #error PayloadDir must be supplied
#endif
#ifndef ReleaseDir
  #error ReleaseDir must be supplied
#endif
[Setup]
AppId={{66D8863B-6474-42FB-88A7-CE24E7C05AC9}
AppName=Enshrouded Server Workshop
AppVersion={#AppVersion}
AppVerName=Enshrouded Server Workshop {#AppVersion}
DefaultDirName={code:GetInstallFolder}
DefaultGroupName=Enshrouded Server Workshop
PrivilegesRequired=lowest
#ifdef InstallerTest
CreateUninstallRegKey=no
#endif
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#ReleaseDir}
OutputBaseFilename=Enshrouded-Server-Workshop-{#AppVersion}-Setup-win-x64
SetupIconFile=..\assets\Workshop.ico
UninstallDisplayIcon={app}\Enshrouded Workshop.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\LICENSE
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: checkedonce
[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
#ifndef InstallerTest
Name: "{userprograms}\Enshrouded Server Workshop"; Filename: "{app}\Enshrouded Workshop.exe"
Name: "{userdesktop}\Enshrouded Server Workshop"; Filename: "{app}\Enshrouded Workshop.exe"; Tasks: desktopicon
#endif
[Run]
Filename: "{app}\Enshrouded Workshop.exe"; Description: "Open Enshrouded Server Workshop"; Flags: nowait postinstall skipifsilent
[Code]
function GetInstallFolder(Param: String): String;
begin
  Result := ExpandConstant('{param:DIR}');
  if Result = '' then
    Result := ExpandConstant('{localappdata}\Programs\Enshrouded Server Workshop');
end;
