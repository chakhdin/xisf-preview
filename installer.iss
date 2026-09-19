[Setup]
AppId={{D37B4D58-7F16-4BC9-9A07-4C0E51610E94}
AppName=XISF & FITS FastViewer & Shell Extensions
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

[Files]
Source: "bin\Release\net48\XisfFastViewer.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net48\SharpShell.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net48\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "app.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\XISF & FITS FastViewer"; Filename: "{app}\XisfFastViewer.exe"; IconFilename: "{app}\XisfFastViewer.exe"

[Registry]
; --- XISF SYSTEM REGISTRATION (HKLM) ---
Root: HKLM; Subkey: "Software\Classes\.xisf"; ValueType: string; ValueName: ""; ValueData: "XisfFile"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\Classes\.xisf\OpenWithProgids"; ValueType: string; ValueName: "XisfFile"; ValueData: ""; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\Classes\XisfFile"; ValueType: string; ValueName: ""; ValueData: "Extensible Image Serialization Format"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\XisfFile\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\XisfFastViewer.exe,0"
Root: HKLM; Subkey: "Software\Classes\XisfFile\shell"; ValueType: string; ValueName: ""; ValueData: "open"
Root: HKLM; Subkey: "Software\Classes\XisfFile\shell\open"; ValueType: string; ValueName: "&Open with FastViewer"
Root: HKLM; Subkey: "Software\Classes\XisfFile\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\XisfFastViewer.exe"" ""%1"""

; --- FITS SYSTEM REGISTRATION (HKLM) ---
Root: HKLM; Subkey: "Software\Classes\.fits"; ValueType: string; ValueName: ""; ValueData: "FitsFile"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\Classes\.fits\OpenWithProgids"; ValueType: string; ValueName: "FitsFile"; ValueData: ""; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\Classes\.fit"; ValueType: string; ValueName: ""; ValueData: "FitsFile"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\Classes\.fit\OpenWithProgids"; ValueType: string; ValueName: "FitsFile"; ValueData: ""; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\Classes\.fts"; ValueType: string; ValueName: ""; ValueData: "FitsFile"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\Classes\.fts\OpenWithProgids"; ValueType: string; ValueName: "FitsFile"; ValueData: ""; Flags: uninsdeletevalue

Root: HKLM; Subkey: "Software\Classes\FitsFile"; ValueType: string; ValueName: ""; ValueData: "Flexible Image Transport System"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\FitsFile\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\XisfFastViewer.exe,0"
Root: HKLM; Subkey: "Software\Classes\FitsFile\shell"; ValueType: string; ValueName: ""; ValueData: "open"
Root: HKLM; Subkey: "Software\Classes\FitsFile\shell\open"; ValueType: string; ValueName: "&Open with FastViewer"
Root: HKLM; Subkey: "Software\Classes\FitsFile\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\XisfFastViewer.exe"" ""%1"""

; --- APPLICATION CAPABILITIES (HKLM) ---
Root: HKLM; Subkey: "Software\Classes\Applications\XisfFastViewer.exe\SupportedTypes"; ValueType: string; ValueName: ".xisf"; ValueData: ""; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\Applications\XisfFastViewer.exe\SupportedTypes"; ValueType: string; ValueName: ".fits"; ValueData: ""; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\Applications\XisfFastViewer.exe\SupportedTypes"; ValueType: string; ValueName: ".fit"; ValueData: ""; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\Applications\XisfFastViewer.exe\SupportedTypes"; ValueType: string; ValueName: ".fts"; ValueData: ""; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\Applications\XisfFastViewer.exe\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\XisfFastViewer.exe"" ""%1"""

; --- PER-USER OVERRIDES (HKCU) ---
Root: HKCU; Subkey: "Software\Classes\.fits"; ValueType: string; ValueName: ""; ValueData: "FitsFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.fit"; ValueType: string; ValueName: ""; ValueData: "FitsFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.fts"; ValueType: string; ValueName: ""; ValueData: "FitsFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.xisf"; ValueType: string; ValueName: ""; ValueData: "XisfFile"; Flags: uninsdeletevalue

[Run]
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
    ClearUserChoices();
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