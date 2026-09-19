using System;
using System.IO;
using System.Windows.Forms;

namespace XisfExplorerPreview
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string initialFile = null;
            if (args != null && args.Length > 0 && File.Exists(args[0]))
            {
                initialFile = args[0];
            }

            Application.Run(new FastViewerForm(initialFile));
        }
    }
}