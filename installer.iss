[Setup]
AppId={{D37B4D58-7F16-4BC9-9A07-4C0E51610E94}
AppName=XISF & FITS Shell Preview & Viewer
AppVersion=1.0
AppPublisher=Gruppo Astrofili Rozzano
DefaultDirName={autopf}\XisfFastViewer
DefaultGroupName=XISF FastViewer
OutputDir=bin\Installer
OutputBaseFilename=XisfFastViewerSetup
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
ChangesAssociations=yes
SetupIconFile=app.ico
UsedUserAreasWarning=no
CloseApplications=yes
CloseApplicationsFilter=dllhost.exe,prevhost.exe
RestartApplications=no

[Files]
Source: "bin\Release\net48\XisfFastViewer.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net48\SharpShell.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net48\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "app.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\XISF & FITS FastViewer"; Filename: "{app}\XisfFastViewer.exe"; IconFilename: "{app}\XisfFastViewer.exe"

[Registry]
; -----------------------------------------------------------------------------
; 1. IMAGE PERCEIVED TYPE & THUMBNAIL TREATMENT
; -----------------------------------------------------------------------------
Root: HKLM; Subkey: "Software\Classes\.fits"; ValueType: string; ValueName: "PerceivedType"; ValueData: "image"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\Classes\.fit"; ValueType: string; ValueName: "PerceivedType"; ValueData: "image"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\Classes\.fts"; ValueType: string; ValueName: "PerceivedType"; ValueData: "image"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\Classes\.xisf"; ValueType: string; ValueName: "PerceivedType"; ValueData: "image"; Flags: uninsdeletevalue

Root: HKLM; Subkey: "Software\Classes\FitsFile"; ValueType: dword; ValueName: "Treatment"; ValueData: 0; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\Classes\ASIFitsView"; ValueType: dword; ValueName: "Treatment"; ValueData: 0; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\Classes\XisfFile"; ValueType: dword; ValueName: "Treatment"; ValueData: 0; Flags: uninsdeletevalue

; -----------------------------------------------------------------------------
; 2. THUMBNAIL HANDLER SHELLEX ({e357fccd-a995-4576-b01f-234630154e96})
; CLSID: {D37B4D58-7F16-4BC9-9A07-4C0E51610E91}
; -----------------------------------------------------------------------------
; Extensions
Root: HKLM; Subkey: "Software\Classes\.fits\ShellEx\{{e357fccd-a995-4576-b01f-234630154e96}"; ValueType: string; ValueName: ""; ValueData: "{{D37B4D58-7F16-4BC9-9A07-4C0E51610E91}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\.fit\ShellEx\{{e357fccd-a995-4576-b01f-234630154e96}"; ValueType: string; ValueName: ""; ValueData: "{{D37B4D58-7F16-4BC9-9A07-4C0E51610E91}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\.fts\ShellEx\{{e357fccd-a995-4576-b01f-234630154e96}"; ValueType: string; ValueName: ""; ValueData: "{{D37B4D58-7F16-4BC9-9A07-4C0E51610E91}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\.xisf\ShellEx\{{e357fccd-a995-4576-b01f-234630154e96}"; ValueType: string; ValueName: ""; ValueData: "{{D37B4D58-7F16-4BC9-9A07-4C0E51610E91}"; Flags: uninsdeletekey

; SystemFileAssociations
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.fits\ShellEx\{{e357fccd-a995-4576-b01f-234630154e96}"; ValueType: string; ValueName: ""; ValueData: "{{D37B4D58-7F16-4BC9-9A07-4C0E51610E91}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.fit\ShellEx\{{e357fccd-a995-4576-b01f-234630154e96}"; ValueType: string; ValueName: ""; ValueData: "{{D37B4D58-7F16-4BC9-9A07-4C0E51610E91}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.fts\ShellEx\{{e357fccd-a995-4576-b01f-234630154e96}"; ValueType: string; ValueName: ""; ValueData: "{{D37B4D58-7F16-4BC9-9A07-4C0E51610E91}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.xisf\ShellEx\{{e357fccd-a995-4576-b01f-234630154e96}"; ValueType: string; ValueName: ""; ValueData: "{{D37B4D58-7F16-4BC9-9A07-4C0E51610E91}"; Flags: uninsdeletekey

; ProgIDs (FitsFile, ASIFitsView, XisfFile)
Root: HKLM; Subkey: "Software\Classes\FitsFile\ShellEx\{{e357fccd-a995-4576-b01f-234630154e96}"; ValueType: string; ValueName: ""; ValueData: "{{D37B4D58-7F16-4BC9-9A07-4C0E51610E91}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\ASIFitsView\ShellEx\{{e357fccd-a995-4576-b01f-234630154e96}"; ValueType: string; ValueName: ""; ValueData: "{{D37B4D58-7F16-4BC9-9A07-4C0E51610E91}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\XisfFile\ShellEx\{{e357fccd-a995-4576-b01f-234630154e96}"; ValueType: string; ValueName: ""; ValueData: "{{D37B4D58-7F16-4BC9-9A07-4C0E51610E91}"; Flags: uninsdeletekey

; -----------------------------------------------------------------------------
; 3. PREVIEW HANDLER SHELLEX ({8895b1c6-b41f-4c1c-a562-0d564250836f})
; CLSID: {D37B4D58-7F16-4BC9-9A07-4C0E51610E92}
; -----------------------------------------------------------------------------
; Extensions
Root: HKLM; Subkey: "Software\Classes\.fits\ShellEx\{{8895b1c6-b41f-4c1c-a562-0d564250836f}"; ValueType: string; ValueName: ""; ValueData: "{{D37B4D58-7F16-4BC9-9A07-4C0E51610E92}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\.fit\ShellEx\{{8895b1c6-b41f-4c1c-a562-0d564250836f}"; ValueType: string; ValueName: ""; ValueData: "{{D37B4D58-7F16-4BC9-9A07-4C0E51610E92}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\.fts\ShellEx\{{8895b1c6-b41f-4c1c-a562-0d564250836f}"; ValueType: string; ValueName: ""; ValueData: "{{D37B4D58-7F16-4BC9-9A07-4C0E51610E92}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\.xisf\ShellEx\{{8895b1c6-b41f-4c1c-a562-0d564250836f}"; ValueType: string; ValueName: ""; ValueData: "{{D37B4D58-7F16-4BC9-9A07-4C0E51610E92}"; Flags: uninsdeletekey

; SystemFileAssociations
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.fits\ShellEx\{{8895b1c6-b41f-4c1c-a562-0d564250836f}"; ValueType: string; ValueName: ""; ValueData: "{{D37B4D58-7F16-4BC9-9A07-4C0E51610E92}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.fit\ShellEx\{{8895b1c6-b41f-4c1c-a562-0d564250836f}"; ValueType: string; ValueName: ""; ValueData: "{{D37B4D58-7F16-4BC9-9A07-4C0E51610E92}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.fts\ShellEx\{{8895b1c6-b41f-4c1c-a562-0d564250836f}"; ValueType: string; ValueName: ""; ValueData: "{{D37B4D58-7F16-4BC9-9A07-4C0E51610E92}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.xisf\ShellEx\{{8895b1c6-b41f-4c1c-a562-0d564250836f}"; ValueType: string; ValueName: ""; ValueData: "{{D37B4D58-7F16-4BC9-9A07-4C0E51610E92}"; Flags: uninsdeletekey

; ProgIDs
Root: HKLM; Subkey: "Software\Classes\FitsFile\ShellEx\{{8895b1c6-b41f-4c1c-a562-0d564250836f}"; ValueType: string; ValueName: ""; ValueData: "{{D37B4D58-7F16-4BC9-9A07-4C0E51610E92}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\ASIFitsView\ShellEx\{{8895b1c6-b41f-4c1c-a562-0d564250836f}"; ValueType: string; ValueName: ""; ValueData: "{{D37B4D58-7F16-4BC9-9A07-4C0E51610E92}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\XisfFile\ShellEx\{{8895b1c6-b41f-4c1c-a562-0d564250836f}"; ValueType: string; ValueName: ""; ValueData: "{{D37B4D58-7F16-4BC9-9A07-4C0E51610E92}"; Flags: uninsdeletekey

Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\PreviewHandlers"; ValueType: string; ValueName: "{{D37B4D58-7F16-4BC9-9A07-4C0E51610E92}"; ValueData: "XISF & FITS Preview Handler"; Flags: uninsdeletevalue

[Run]
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\XisfFastViewer.exe"""; Flags: runhidden

[UninstallRun]
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/unregister ""{app}\XisfFastViewer.exe"""; Flags: runhidden; RunOnceId: "UnregXisfCOMServer"

[Code]
procedure SHChangeNotify(wEventId: Cardinal; uFlags: UINT; dwItem1, dwItem2: DWORD);
  external 'SHChangeNotify@shell32.dll stdcall';

// CloseApplications/Restart Manager doesn't reliably catch the short-lived dllhost.exe/
// prevhost.exe COM surrogates Explorer spawns per thumbnail/preview request, which is why
// "DeleteFile failed; code 5 (Access is denied)" on XisfFastViewer.exe can still happen even
// with it enabled. Kill them outright instead, same as the project's own build already does.
procedure KillLockingProcesses();
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/F /IM dllhost.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill.exe', '/F /IM prevhost.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill.exe', '/F /IM XisfFastViewer.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function InitializeSetup(): Boolean;
begin
  KillLockingProcesses();
  Result := True;
end;

function InitializeUninstall(): Boolean;
begin
  KillLockingProcesses();
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    SHChangeNotify($08000000, 0, 0, 0); // SHCNE_ASSOCCHANGED
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    SHChangeNotify($08000000, 0, 0, 0);
  end;
end;