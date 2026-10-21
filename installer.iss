[Setup]
AppId={{D37B4D58-7F16-4BC9-9A07-4C0E51610E94}
AppName=XISF & FITS Shell Preview & Viewer
AppVersion=1.03
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
SetupIconFile=app.ico
UsedUserAreasWarning=no
CloseApplications=yes
CloseApplicationsFilter=explorer.exe,dllhost.exe,prevhost.exe
RestartApplications=yes

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
var
  ExplorerWasKilled: Boolean;

procedure SHChangeNotify(wEventId: Cardinal; uFlags: UINT; dwItem1, dwItem2: DWORD);
  external 'SHChangeNotify@shell32.dll stdcall';

// Explorer is not the only possible holder: any program that loads the shell handler
// (e.g. PixInsight's file dialogs) can too - see WaitForFileUnlockedOrAsk.
// Root cause confirmed by hand: Explorer holds a plain (non-FILE_SHARE_DELETE) read
// handle on XisfFastViewer.exe merely from having its folder open in a view - long
// enough to make DeleteFile fail with "Access is denied" even for a fully elevated
// process, and short-lived enough that no diagnostic tool (Get-Process modules,
// Restart Manager, Sysinternals handle64.exe run a beat later) ever caught it holding
// the file. CloseApplications/RestartApplications is the supported way to handle this,
// but proved unreliable on its own for the short-lived dllhost.exe/prevhost.exe COM
// surrogates too, so kill everything outright as a fallback and restart Explorer
// ourselves afterwards instead of trusting RestartApplications alone.
// Always run system tools by full path: this process is elevated, so never let a
// bare name be resolved through the search path.
function SysTool(Name: String): String;
begin
  Result := ExpandConstant('{sys}\' + Name);
end;

procedure KillLockingProcesses();
var
  ResultCode: Integer;
begin
  Exec(SysTool('taskkill.exe'), '/F /IM dllhost.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(SysTool('taskkill.exe'), '/F /IM prevhost.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(SysTool('taskkill.exe'), '/F /IM XisfFastViewer.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

// Only called right before the actual file copy/removal, not during the wizard -
// killing Explorer for the whole time the user is clicking through wizard pages
// would blank their desktop/taskbar for no reason.
procedure KillExplorerAndLockingProcesses();
var
  ResultCode: Integer;
begin
  Exec(SysTool('taskkill.exe'), '/F /IM explorer.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  ExplorerWasKilled := True;
  KillLockingProcesses();
end;

procedure RelaunchExplorerIfNeeded();
var
  ResultCode: Integer;
begin
  if ExplorerWasKilled then
  begin
    // Setup runs elevated, so a plain Exec('explorer.exe') spawns Explorer as an
    // elevated child - which then doesn't register itself as the interactive desktop
    // shell (this is why the desktop/taskbar stayed gone after the file-copy step).
    // /trustlevel:0x20000 explicitly de-elevates the new process to Medium integrity,
    // the same mechanism Task Manager uses for its "Run new task" (unprivileged) option.
    Exec(SysTool('runas.exe'), '/trustlevel:0x20000 "' + ExpandConstant('{win}') + '\explorer.exe"', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
    ExplorerWasKilled := False;
  end;
end;

// Deliberately no InitializeSetup/InitializeUninstall hook that kills processes:
// dllhost.exe hosts COM surrogates of unrelated programs, so nothing is killed until
// the user has confirmed (ssInstall / usUninstall below).

// Even after a process hosting the exe as an executable image is killed, Windows
// can hold the underlying image section open for a short, unpredictable moment
// before the file is actually deletable/overwritable (taskkill returning is not
// proof the OS has released it). So don't just kill once and hope - keep killing
// and retrying the delete until it actually succeeds, or give up after ~5s.
function WaitForFileUnlocked(FileName: String): Boolean;
var
  i: Integer;
begin
  Result := True;
  if not FileExists(FileName) then
    exit;
  for i := 1 to 10 do
  begin
    if DeleteFile(FileName) then
      exit;
    KillLockingProcesses();
    Sleep(500);
  end;
  Result := False;
end;

// Any process that browses XISF/FITS files (PixInsight's file dialogs, an image
// editor, a file manager, ...) can load our in-proc shell handler and keep
// XisfFastViewer.exe/SharpShell.dll open. Those are ordinary applications that may
// hold unsaved work, so don't kill them: ask the user to close them and retry.
procedure WaitForFileUnlockedOrAsk(FileName: String);
begin
  while not WaitForFileUnlocked(FileName) do
  begin
    if SuppressibleMsgBox(
         'Setup cannot replace ' + FileName + ' because another program is using it.' + #13#10#13#10 +
         'Close programs that open or browse XISF/FITS files (for example PixInsight, ' +
         'other image editors or file managers), then click Retry.',
         mbError, MB_RETRYCANCEL, IDCANCEL) = IDCANCEL then
      exit;
  end;
end;

const
  CurrentThumbnailClsid = '{D37B4D58-7F16-4BC9-9A07-4C0E51610E91}';
  CurrentPreviewClsid = '{D37B4D58-7F16-4BC9-9A07-4C0E51610E92}';
  KnownStaleThumbnailClsid = '{a71bc054-942c-4a5f-a4f7-d8b6f3e6c1d2}';
  KnownStalePreviewClsid = '{8f4c479b-84c1-4d56-8a5e-44e281f275e1}';
  ThumbnailShellExKey = '{e357fccd-a995-4576-b01f-234630154e96}';
  PreviewShellExKey = '{8895b1c6-b41f-4c1c-a562-0d564250836f}';

// Older builds registered this assembly under other CLSIDs and hooked them into
// ProgIDs (the PixInsight one, Fit.File, Fits.File, ...). ProgID-level handlers win
// over the extension-level ones we register, so those leftovers shadow the current
// registration - e.g. a stale, surrogate-less preview CLSID made previews of large
// .fit files come up blank. A handler value is stale if it is not our current CLSID
// and either is a known old one or its COM registration points at our own assembly.
// Anything else (another product's handler) is never touched.
function PointsAtOurAssembly(Clsid: String): Boolean;
var
  CodeBase: String;
begin
  Result := RegQueryStringValue(HKLM, 'Software\Classes\CLSID\' + Clsid + '\InprocServer32', 'CodeBase', CodeBase) and
            (Pos('xisffastviewer.exe', Lowercase(CodeBase)) > 0);
end;

function IsStaleHandler(Value, CurrentClsid, KnownStaleClsid: String): Boolean;
begin
  Result := (Value <> '') and (CompareText(Value, CurrentClsid) <> 0) and
            ((CompareText(Value, KnownStaleClsid) = 0) or PointsAtOurAssembly(Value));
end;

procedure RemoveStaleClsid(Clsid: String);
begin
  if PointsAtOurAssembly(Clsid) then
    RegDeleteKeyIncludingSubkeys(HKLM, 'Software\Classes\CLSID\' + Clsid);
  RegDeleteValue(HKLM, 'Software\Microsoft\Windows\CurrentVersion\PreviewHandlers', Clsid);
end;

procedure RemoveStaleShellEx(ClassName, HandlerKey, CurrentClsid, KnownStaleClsid: String);
var
  Key, Value: String;
begin
  Key := 'Software\Classes\' + ClassName + '\ShellEx\' + HandlerKey;
  if RegQueryStringValue(HKLM, Key, '', Value) and IsStaleHandler(Value, CurrentClsid, KnownStaleClsid) then
  begin
    RegDeleteKeyIncludingSubkeys(HKLM, Key);
    RemoveStaleClsid(Value);
  end;
end;

procedure CleanStaleRegistrations();
var
  Classes: TArrayOfString;
  i: Integer;
begin
  if RegGetSubkeyNames(HKLM, 'Software\Classes', Classes) then
    for i := 0 to GetArrayLength(Classes) - 1 do
      if Pos('.', Classes[i]) <> 1 then
      begin
        RemoveStaleShellEx(Classes[i], ThumbnailShellExKey, CurrentThumbnailClsid, KnownStaleThumbnailClsid);
        RemoveStaleShellEx(Classes[i], PreviewShellExKey, CurrentPreviewClsid, KnownStalePreviewClsid);
      end;
  RemoveStaleClsid(KnownStaleThumbnailClsid);
  RemoveStaleClsid(KnownStalePreviewClsid);
end;
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    KillExplorerAndLockingProcesses();
    WaitForFileUnlockedOrAsk(ExpandConstant('{app}\XisfFastViewer.exe'));
  end
  else if CurStep = ssPostInstall then
  begin
    CleanStaleRegistrations();
    SHChangeNotify($08000000, 0, 0, 0); // SHCNE_ASSOCCHANGED
    RelaunchExplorerIfNeeded();
  end;
end;

// Safety net: if the user aborts mid-install (ssPostInstall never reached), make
// sure Explorer still comes back instead of leaving the desktop/taskbar gone.
procedure DeinitializeSetup();
begin
  RelaunchExplorerIfNeeded();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    KillExplorerAndLockingProcesses();
  end
  else if CurUninstallStep = usPostUninstall then
  begin
    SHChangeNotify($08000000, 0, 0, 0);
    RelaunchExplorerIfNeeded();
  end;
end;

procedure DeinitializeUninstall();
begin
  RelaunchExplorerIfNeeded();
end;