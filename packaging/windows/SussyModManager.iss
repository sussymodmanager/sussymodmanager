; Inno Setup script for SUSSYMODMANAGER.
;
; Per-user install (no admin prompt) into %LocalAppData%\Programs\SussyModManager so the in-app
; auto-updater can replace files without elevation. Version is injected by CI:
;   ISCC //DMyAppVersion=1.2.3 packaging\windows\SussyModManager.iss
; By default it packages the self-contained publish output at ..\..\publish\win-x64.

#define MyAppName "SUSSYMODMANAGER"
#define MyAppExeName "SussyModManager.exe"
#define MyAppPublisher "SUSSYMODMANAGER"
#define MyAppURL "https://github.com/sussymodmanager/sussymodmanager"

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#ifndef SourceDir
  #define SourceDir "..\..\publish\win-x64"
#endif

[Setup]
; AppId uniquely identifies the app for upgrades/uninstall - do NOT change it between releases.
AppId={{B2E6C2F4-7A3D-4C5E-9F1A-7C9D2E5B8A10}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={localappdata}\Programs\SussyModManager
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
OutputDir={#SourcePath}..\..\installer
OutputBaseFilename=SussyModManager-Setup-x64
SetupIconFile={#SourcePath}..\..\src\SussyModManager\Assets\sus.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
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
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
