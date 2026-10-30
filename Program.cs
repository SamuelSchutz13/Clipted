using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

static partial class Program
{
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--selftest") return SelfTest();

        bool first;
        using (new Mutex(true, "Clipted.SingleInstance", out first))
        {
            if (!first) return 0;
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var store = new Store(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clipted"));
            store.Load();
            store.Prune(DateTime.Now);
            if (Popup.StartsWithWindows()) Popup.SetStartup(true);
            var popup = new Popup(store);

            var menu = new ContextMenuStrip();
            menu.Items.Add("Abrir", null, (s, e) => popup.ShowPopup());
            menu.Items.Add("Sair", null, (s, e) => Application.Exit());
            var tray = new NotifyIcon { Icon = AppIcon(SystemInformation.SmallIconSize.Width), Text = "Clipted — histórico de cópias", ContextMenuStrip = menu, Visible = true };
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) popup.Toggle(); };

            var timer = new System.Windows.Forms.Timer { Interval = 60000 };
            timer.Tick += (s, e) => popup.Tick();
            timer.Start();

            Application.Run();
            tray.Visible = false;
        }
        return 0;
    }

    public static Icon AppIcon(int size)
    {
        using (Stream s = typeof(Program).Assembly.GetManifestResourceStream("Clipted.ico")) return new Icon(s, size, size);
    }
}
