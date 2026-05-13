#define AppName "CodexSwitch"
#define AppPublisher "AIDotNet"
#define AppUrl "https://github.com/AIDotNet/CodexSwitch"

#ifndef AppVersion
#define AppVersion "0.0.0"
#endif

#ifndef SourceDir
#define SourceDir "..\..\artifacts\publish\win-x64"
#endif

#ifndef OutputDir
#define OutputDir "..\..\artifacts\package"
#endif

#ifndef OutputBaseFilename
#define OutputBaseFilename "CodexSwitch-setup"
#endif

#ifndef IconPath
#define IconPath "..\..\CodexSwitch\Assets\favicon.ico"
#endif

[Setup]
AppId={{7B17D1CB-B861-4C0D-95C9-9B3B7756120A}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
SetupIconFile={#IconPath}
UninstallDisplayIcon={app}\CodexSwitch.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\CodexSwitch.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\CodexSwitch.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\CodexSwitch.exe"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
