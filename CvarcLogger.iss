; Inno Setup script for CVARC Logger. Compiled by publish.ps1 (unattended, via ISCC.exe) right after
; it produces publish\CvarcLogger -- this script just packages that folder as-is into an installer.
;
; AppId is a fixed GUID that must never change -- it's what lets Windows treat a new version as an
; upgrade of the same Apps & Features entry instead of a second, duplicate one.
;
; Installs to C:\CvarcLogger (not Program Files, per user request) and registers a normal Apps &
; Features entry with an auto-generated uninstaller -- PrivilegesRequired=admin because writing to the
; system drive root and to HKLM's Uninstall key both require elevation.
;
; MyAppVersion is passed in from publish.ps1 via `ISCC.exe /DMyAppVersion=<version> CvarcLogger.iss`;
; the #ifndef fallback below only matters if this script is ever compiled by hand without that define.
#ifndef MyAppVersion
  #define MyAppVersion "0.0"
#endif

#define MyAppName "CVARC Logger"
#define MyAppPublisher "Conejo Valley Amateur Radio Club"
#define MyAppExeName "CvarcLogger.exe"
#define MyPublishDir "publish\CvarcLogger"

[Setup]
AppId={{8BB4663A-5103-4469-8959-C7D81E15E4A7}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={sd}\CvarcLogger
DisableDirPage=yes
DefaultGroupName=CVARC Logger
DisableProgramGroupPage=yes
PrivilegesRequired=admin
OutputDir=publish
OutputBaseFilename=CvarcLogger-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; The app writes logs/backups under %LocalAppData%\CVARC Logger at runtime -- not part of the install,
; so Inno won't remove it on its own. Left in place deliberately: it holds the QSO database, and an
; uninstall should never silently delete a user's log data.
