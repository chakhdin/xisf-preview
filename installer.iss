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

[Tasks]
Name: "setdefault"; Description: "Set FastViewer as default program to open .xisf and .fits files"; Flags: unchecked

[Files]
Source: "bin\Release\net48\XisfFastViewer.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net48\SharpShell.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net48\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "app.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\XISF & FITS FastViewer"; Filename: "{app}\XisfFastViewer.exe"; IconFilename: "{app}\XisfFastViewer.exe"

[Registry]
; -----------------------------------------------------------------------------
; 1. APPLICATION REGISTRATION (Enables "Open with..." without overriding defaults)
; -----------------------------------------------------------------------------
Root: HKLM; Subkey: "Software\Classes\Applications\XisfFastViewer.exe"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "XISF & FITS FastViewer"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\Applications\XisfFastViewer.exe\SupportedTypes"; ValueType: string; ValueName: ".xisf"; ValueData: ""; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\Applications\XisfFastViewer.exe\SupportedTypes"; ValueType: string; ValueName: ".fits"; ValueData: ""; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\Applications\XisfFastViewer.exe\SupportedTypes"; ValueType: string; ValueName: ".fit"; ValueData: ""; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\Applications\XisfFastViewer.exe\SupportedTypes"; ValueType: string; ValueName: ".fts"; ValueData: ""; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\Applications\XisfFastViewer.exe\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\XisfFastViewer.exe"" ""%1"""; Flags: uninsdeletekey

; Make FastViewer visible in Explorer's "Open with..." menu
Root: HKLM; Subkey: "Software\Classes\.xisf\OpenWithProgids"; ValueType: string; ValueName: "XisfFastViewer.Assoc"; ValueData: ""; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\Classes\.fits\OpenWithProgids"; ValueType: string; ValueName: "XisfFastViewer.Assoc"; ValueData: ""; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\Classes\.fit\OpenWithProgids"; ValueType: string; ValueName: "XisfFastViewer.Assoc"; ValueData: ""; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\Classes\.fts\OpenWithProgids"; ValueType: string; ValueName: "XisfFastViewer.Assoc"; ValueData: ""; Flags: uninsdeletevalue

Root: HKLM; Subkey: "Software\Classes\XisfFastViewer.Assoc"; ValueType: string; ValueName: ""; ValueData: "Astronomical Image"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\XisfFastViewer.Assoc\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\XisfFastViewer.exe,0"
Root: HKLM; Subkey: "Software\Classes\XisfFastViewer.Assoc\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\XisfFastViewer.exe"" ""%1"""

; -----------------------------------------------------------------------------
; 2. OPTIONAL: DEFAULT ASSOCIATION (ONLY applied if "setdefault" task is checked)
; -----------------------------------------------------------------------------
Root: HKLM; Subkey: "Software\Classes\.xisf"; ValueType: string; ValueName: ""; ValueData: "XisfFastViewer.Assoc"; Flags: uninsdeletevalue; Tasks: setdefault
Root: HKLM; Subkey: "Software\Classes\.fits"; ValueType: string; ValueName: ""; ValueData: "XisfFastViewer.Assoc"; Flags: uninsdeletevalue; Tasks: setdefault
Root: HKLM; Subkey: "Software\Classes\.fit"; ValueType: string; ValueName: ""; ValueData: "XisfFastViewer.Assoc"; Flags: uninsdeletevalue; Tasks: setdefault
Root: HKLM; Subkey: "Software\Classes\.fts"; ValueType: string; ValueName: ""; ValueData: "XisfFastViewer.Assoc"; Flags: uninsdeletevalue; Tasks: setdefault

[Run]
; COM Server Registration via 64-bit RegAsm for SharpShell Thumbnail & Preview Handlers
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\XisfFastViewer.exe"""; Flags: runhidden

[UninstallRun]
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/unregister ""{app}\XisfFastViewer.exe"""; Flags: runhidden; RunOnceId: "UnregXisfCOMServer"

[Code]
procedure SHChangeNotify(wEventId: Cardinal; uFlags: UINT; dwItem1, dwItem2: DWORD);
  external 'SHChangeNotify@shell32.dll stdcall';

procedure ClearUserChoices();
var
  Extensions: array[0..3] of string;
  I: Integer;
  ResultCode: Integer;
begin
  Extensions[0] := '.fits';
  Extensions[1] := '.fit';
  Extensions[2] := '.fts';
  Extensions[3] := '.xisf';

  for I := 0 to 3 do
  begin
    Exec('reg.exe', 'delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\' + Extensions[I] + '\UserChoice" /f', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if WizardIsTaskSelected('setdefault') then
    begin
      ClearUserChoices();
    end;
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