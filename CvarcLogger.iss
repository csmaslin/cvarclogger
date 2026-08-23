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
DisableDirPage=no
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
; Let Windows Restart Manager detect and close a running CvarcLogger before its files are replaced, so an
; in-place upgrade (and the Fresh reset in [Code] below) never fails on a locked exe or a locked database.
CloseApplications=yes
RestartApplications=no

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

[Code]
{ Install-time pre-flight: if a previous CVARC Logger is already on the PC, ask whether to keep the
  user's data (Update) or start clean (Fresh install). Fresh never deletes the QSO log outright -- it
  first copies it to backuplog.db in the install folder so the user can restore or delete it later. }

var
  FreshRequested: Boolean;
  RemoveDataRequested: Boolean;

{ Where a previous install recorded itself, or '' if none. AppId + '_is1' is Inno's own uninstall key;
  a 32-bit installer's HKLM read is WOW64-redirected to the same place a prior run of this same script
  wrote it, so this stays consistent without special 64-bit handling. }
function PriorInstallLocation(): String;
var
  loc: String;
begin
  Result := '';
  if RegQueryStringValue(HKLM,
      'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{8BB4663A-5103-4469-8959-C7D81E15E4A7}_is1',
      'InstallLocation', loc) then
    Result := loc;
end;

{ The folder the app actually keeps data in: the legacy %LOCALAPPDATA%\CVARC Logger if it still holds
  real data, otherwise the install folder itself (next to the exe). Mirrors the app's own
  App.DataDirectory resolution so Fresh cleans -- and backs up from -- the right place. }
function AppDataDir(InstallDir: String): String;
var
  Legacy: String;
begin
  Legacy := ExpandConstant('{localappdata}\CVARC Logger');
  if FileExists(Legacy + '\settings.json') or FileExists(Legacy + '\cvarclogger.db')
     or FileExists(Legacy + '\credentials.dpapi') then
    Result := Legacy
  else
    Result := InstallDir;
end;

function InitializeSetup(): Boolean;
var
  Choice: Integer;
begin
  Result := True;
  FreshRequested := False;
  RemoveDataRequested := False;

  { Treat either a recorded prior install or a leftover install folder as "already here". }
  if (PriorInstallLocation() = '') and (not DirExists(ExpandConstant('{sd}\CvarcLogger'))) then
    exit;

  Choice := MsgBox(
    'An existing CVARC Logger was found on this PC.' + #13#10 + #13#10 +
    'UPDATE keeps your log, settings, and station/radio profiles.' + #13#10 + #13#10 +
    'FRESH INSTALL starts with clean settings. Your current log is first copied to backuplog.db in the '
    + 'install folder, so you can restore or delete it yourself later -- it is never just erased.' + #13#10 + #13#10 +
    'Update and keep everything?' + #13#10 +
    '     Yes = Update       No = Fresh install       Cancel = quit Setup',
    mbConfirmation, MB_YESNOCANCEL);

  if Choice = IDCANCEL then
  begin
    Result := False;
    exit;
  end;

  FreshRequested := (Choice = IDNO);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  InstallDir, DataDir, Db, Backup: String;
begin
  { Runs after files are installed and (via CloseApplications) the old app is closed, so the database
    isn't locked. Only acts when the user chose Fresh. }
  if (CurStep <> ssPostInstall) or (not FreshRequested) then
    exit;

  InstallDir := ExpandConstant('{app}');
  DataDir := AppDataDir(InstallDir);
  Db := DataDir + '\cvarclogger.db';
  Backup := InstallDir + '\backuplog.db';

  ForceDirectories(InstallDir);

  if FileExists(Db) then
  begin
    if CopyFile(Db, Backup, False) then
    begin
      DeleteFile(Db);
      DeleteFile(DataDir + '\settings.json');
      DeleteFile(DataDir + '\credentials.dpapi');
    end
    else
      MsgBox('Fresh install: your existing log could not be copied to' + #13#10 + Backup + #13#10 + #13#10 +
        'Nothing was removed -- your existing log and settings are untouched.', mbError, MB_OK);
  end
  else
  begin
    { No existing log to preserve; still clear any stray settings for a genuinely clean start. }
    DeleteFile(DataDir + '\settings.json');
    DeleteFile(DataDir + '\credentials.dpapi');
  end;
end;

function InitializeUninstall(): Boolean;
var
  DataDir, Db: String;
  Choice: Integer;
begin
  Result := True;
  DataDir := ExpandConstant('{localappdata}\CVARC Logger');
  Db := DataDir + '\cvarclogger.db';

  if FileExists(Db) or FileExists(DataDir + '\settings.json') then
  begin
    Choice := MsgBox(
      'Remove QSO database and settings?' + #13#10 + #13#10 +
      'KEEP: Saves your log data. You can reinstall later and continue where you left off.' + #13#10 + #13#10 +
      'REMOVE: Deletes the log, settings, and radio profiles (a clean wipe).' + #13#10 + #13#10 +
      'Keep your data?',
      mbConfirmation, MB_YESNO);

    RemoveDataRequested := (Choice = IDNO);
  end;
end;

procedure DeinitializeUninstall();
var
  DataDir: String;
  InstallDir: String;
begin
  if RemoveDataRequested then
  begin
    DataDir := ExpandConstant('{localappdata}\CVARC Logger');
    InstallDir := ExpandConstant('{app}');

    if FileExists(DataDir + '\cvarclogger.db') then
      DeleteFile(DataDir + '\cvarclogger.db');
    if FileExists(DataDir + '\settings.json') then
      DeleteFile(DataDir + '\settings.json');
    if FileExists(DataDir + '\credentials.dpapi') then
      DeleteFile(DataDir + '\credentials.dpapi');
    if FileExists(InstallDir + '\backuplog.db') then
      DeleteFile(InstallDir + '\backuplog.db');

    if DirExists(DataDir) then
      DelTree(DataDir, True, True, True);
    if DirExists(InstallDir) then
      DelTree(InstallDir, True, True, True);
  end;
end;
