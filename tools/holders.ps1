# Lists the programs that hold the installed handler files open (Restart Manager).
# Used by installer.iss, which embeds this script as a base64 -EncodedCommand (see the
# comment above HoldersScript there for how to regenerate it).
# Input (environment): XISF_RM_DIR = install folder, XISF_RM_OUT = result file.
try {
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices; using System.Collections.Generic;
public static class RM {
  [StructLayout(LayoutKind.Sequential)] struct FT { public uint lo, hi; }
  [StructLayout(LayoutKind.Sequential)] struct UP { public int pid; public FT start; }
  [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct PI {
    public UP p; [MarshalAs(UnmanagedType.ByValTStr, SizeConst=256)] public string app;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst=64)] public string svc; public int type, status, ts; [MarshalAs(UnmanagedType.Bool)] public bool rest; }
  [DllImport("rstrtmgr.dll", CharSet=CharSet.Unicode)] static extern int RmStartSession(out uint h, int f, string k);
  [DllImport("rstrtmgr.dll")] static extern int RmEndSession(uint h);
  [DllImport("rstrtmgr.dll", CharSet=CharSet.Unicode)] static extern int RmRegisterResources(uint h, uint n, string[] f, uint a, IntPtr b, uint s, IntPtr c);
  [DllImport("rstrtmgr.dll")] static extern int RmGetList(uint h, out uint need, ref uint n, [In,Out] PI[] info, out uint reasons);
  public static List<string> Who(string path) {
    var r = new List<string>(); uint h; string key = Guid.NewGuid().ToString();
    if (RmStartSession(out h, 0, key) != 0) return r;
    try { RmRegisterResources(h, 1, new[]{path}, 0, IntPtr.Zero, 0, IntPtr.Zero);
      uint need=0, n=0, reasons; RmGetList(h, out need, ref n, null, out reasons);
      if (need>0) { n=need; var a=new PI[n]; if (RmGetList(h, out need, ref n, a, out reasons)==0) for(int i=0;i<n;i++) r.Add(a[i].app+" (PID "+a[i].p.pid+")"); }
    } finally { RmEndSession(h); }
    return r;
  }
}
'@
$names = foreach ($f in 'XisfFastViewer.exe','SharpShell.dll') { [RM]::Who((Join-Path $env:XISF_RM_DIR $f)) }
$names | Sort-Object -Unique | Set-Content -Path $env:XISF_RM_OUT -Encoding ASCII
} catch { }
