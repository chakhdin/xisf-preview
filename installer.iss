[Setup]
AppId={{D37B4D58-7F16-4BC9-9A07-4C0E51610E94}
AppName=XISF FastViewer & Shell Extensions
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
ChangesAssociations=yes

[Files]
Source: "bin\Release\net48\XisfFastViewer.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net48\SharpShell.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net48\*.dll"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
Root: HKLM; Subkey: "Software\Classes\.xisf"; ValueType: string; ValueName: ""; ValueData: "XisfFile"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\Classes\.xisf\OpenWithProgids"; ValueType: string; ValueName: "XisfFile"; ValueData: ""; Flags: uninsdeletevalue

Root: HKLM; Subkey: "Software\Classes\XisfFile"; ValueType: string; ValueName: ""; ValueData: "Extensible Image Serialization Format"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\XisfFile\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\XisfFastViewer.exe,0"
Root: HKLM; Subkey: "Software\Classes\XisfFile\shell"; ValueType: string; ValueName: ""; ValueData: "open"
Root: HKLM; Subkey: "Software\Classes\XisfFile\shell\open"; ValueType: string; ValueName: ""; ValueData: "&Open with XISF FastViewer"
Root: HKLM; Subkey: "Software\Classes\XisfFile\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\XisfFastViewer.exe"" ""%1"""

Root: HKLM; Subkey: "Software\Classes\Applications\XisfFastViewer.exe\SupportedTypes"; ValueType: string; ValueName: ".xisf"; ValueData: ""; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\Applications\XisfFastViewer.exe\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\XisfFastViewer.exe"" ""%1"""

[Run]
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\XisfFastViewer.exe"""; Flags: runhidden
Filename: "reg"; Parameters: "delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.xisf\UserChoice"" /f"; Flags: runhidden

[UninstallRun]
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/unregister ""{app}\XisfFastViewer.exe"""; Flags: runhidden; RunOnceId: "UnregXisfCOMServer"

[Code]
// Import Win32 SHChangeNotify from shell32.dll
procedure SHChangeNotify(wEventId: Cardinal; uFlags: UINT; dwItem1, dwItem2: DWORD);
  external 'SHChangeNotify@shell32.dll stdcall';

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // SHCNE_ASSOCCHANGED = $08000000, SHCNF_IDLIST = $0000
    SHChangeNotify($08000000, 0, 0, 0);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    SHChangeNotify($08000000, 0, 0, 0);
  end;
end;