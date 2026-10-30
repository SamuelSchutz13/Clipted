using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

partial class Popup : Form
{
    [DllImport("user32.dll")] static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll")] static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wp, string lp);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    const int WM_CLIPBOARDUPDATE = 0x031D;

    static readonly Theme LightTheme = new Theme
    {
        Bg = Color.FromArgb(250, 250, 251), Fg = Color.FromArgb(24, 24, 28), Muted = Color.FromArgb(108, 110, 120),
        Hover = Color.FromArgb(236, 238, 245), Border = Color.FromArgb(212, 213, 220), Field = Color.FromArgb(238, 239, 243),
        Tile = Color.FromArgb(229, 233, 252), Accent = Color.FromArgb(58, 75, 224), Danger = Color.FromArgb(196, 43, 28)
    };
    static readonly Theme DarkTheme = new Theme
    {
        Bg = Color.FromArgb(30, 30, 34), Fg = Color.FromArgb(236, 236, 240), Muted = Color.FromArgb(150, 152, 162),
        Hover = Color.FromArgb(46, 47, 55), Border = Color.FromArgb(72, 72, 80), Field = Color.FromArgb(44, 45, 52),
        Tile = Color.FromArgb(48, 52, 86), Accent = Color.FromArgb(142, 162, 255), Danger = Color.FromArgb(255, 138, 128)
    };
    static readonly Font Icons = new Font("Segoe MDL2 Assets", 10f);
    static readonly Font Small = new Font("Segoe UI", 8.5f);
    static readonly Font Bold = new Font("Segoe UI Semibold", 9.5f);
    static readonly Font TitleFont = new Font("Segoe UI Semibold", 11f);
    const TextFormatFlags OneLine = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
    const TextFormatFlags Center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;

    readonly Store store;
    readonly float scale;
    readonly Bitmap logo;
    Theme t;
    readonly ListBox list = new ListBox();
    readonly Label empty = new Label();
    readonly Panel header = new Panel(), searchBar = new Panel(), settings = new Panel();
    readonly TextBox search = new TextBox();
    readonly Button btnSettings;
    readonly PillButton btnClear = new PillButton();
    readonly Timer clearTimer = new Timer { Interval = 3000 };
    readonly Dropdown ddLimit = new Dropdown(), ddExpiry = new Dropdown(), ddTheme = new Dropdown();
    List<int> limitValues, expiryValues;
    readonly Toggle startup = new Toggle();
    readonly PillButton btnFolder = new PillButton();
    readonly Panel preview = new Panel();
    Image previewImg;
    Entry previewEntry;
    readonly ToolTip tips = new ToolTip();
    static readonly Font SectionFont = new Font("Segoe UI Semibold", 8f);
    readonly List<Rectangle> cards = new List<Rectangle>();
    readonly List<SettingRow> rows = new List<SettingRow>(), sections = new List<SettingRow>();
    SettingRow storageRow;
    int layoutY;
    readonly Dictionary<string, Thumb> thumbs = new Dictionary<string, Thumb>();
    int hover = -1;
    bool hoverX;
    DateTime hiddenAt;
    uint ownSeq;

    int D(int px) { return (int)Math.Round(px * scale); }

    public Popup(Store store)
    {
        this.store = store;
        using (var g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f;
        using (Icon ic = Program.AppIcon(D(20))) logo = ic.ToBitmap();

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;
        Text = "Clipted";
        Font = new Font("Segoe UI", 9.5f);
        Size = new Size(D(400), D(580));
        Padding = new Padding(1);

        btnSettings = IconButton("Configurações", (s, e) => ShowSettings(!settings.Visible));
        btnSettings.Text = "\uE713";
        header.Dock = DockStyle.Top;
        header.Height = D(48);
        header.Controls.Add(btnSettings);
        header.Paint += PaintHeader;

        searchBar.Dock = DockStyle.Top;
        searchBar.Height = D(48);
        search.BorderStyle = BorderStyle.None;
        search.AccessibleName = "Pesquisar no histórico";
        search.HandleCreated += (s, e) => SendMessage(search.Handle, 0x1501, (IntPtr)1, "Pesquisar no histórico");
        search.TextChanged += (s, e) => RefreshList();
        search.KeyDown += SearchKey;
        searchBar.Controls.Add(search);
        searchBar.Paint += PaintSearch;
        searchBar.Resize += (s, e) => search.SetBounds(D(46), D(4) + (D(36) - search.Height) / 2, searchBar.Width - D(70), search.Height);

        settings.Dock = DockStyle.Fill;
        settings.Visible = false;
        settings.Paint += PaintSettings;
        limitValues = Options(new[] { 50, 100, 200, 500, 1000, 5000, 0 }, store.Limit);
        ddLimit.Options = limitValues.Select(n => n == 0 ? "Sem limite" : n.ToString("N0") + " itens").ToArray();
        ddLimit.SelectedIndex = limitValues.IndexOf(store.Limit);
        expiryValues = Options(new[] { 60, 720, 1440, 10080, 43200, 129600, 0 }, store.ExpiryMinutes);
        ddExpiry.Options = expiryValues.Select(m => ExpiryLabel(m)).ToArray();
        ddExpiry.SelectedIndex = expiryValues.IndexOf(store.ExpiryMinutes);
        ddTheme.Options = new[] { "Claro", "Escuro" };
        ddTheme.SelectedIndex = store.Dark ? 1 : 0;
        foreach (Control c in new Control[] { ddLimit, ddExpiry, ddTheme, btnFolder, btnClear }) c.Size = new Size(D(128), D(32));
        startup.Size = new Size(D(42), D(22));
        startup.Checked = StartsWithWindows();
        btnFolder.Text = "Abrir pasta";
        btnClear.Text = "Limpar tudo";
        btnClear.Glyph = "";

        layoutY = D(4);
        Section("HISTÓRICO");
        AddSetting("Limite de itens", "Os mais antigos saem primeiro", ddLimit);
        AddSetting("Apagar itens antigos", "Depois de quanto tempo", ddExpiry);
        Section("APARÊNCIA");
        AddSetting("Tema", "Aparência do painel", ddTheme);
        Section("SISTEMA");
        AddSetting("Iniciar com o Windows", "Abre o Clipted quando o PC liga", startup);
        Section("ARMAZENAMENTO");
        storageRow = AddSetting("Dados salvos", "", btnFolder);
        AddSetting("Limpar histórico", "Apaga todos os itens e imagens", btnClear);

        ddLimit.SelectedIndexChanged += SettingsChanged;
        ddExpiry.SelectedIndexChanged += SettingsChanged;
        ddTheme.SelectedIndexChanged += (s, e) =>
        {
            if ((ddTheme.SelectedIndex == 1) == store.Dark) return;
            store.Dark = ddTheme.SelectedIndex == 1;
            store.Save();
            ApplyTheme();
        };
        startup.CheckedChanged += (s, e) => SetStartup(startup.Checked);
        btnFolder.Click += (s, e) => { Directory.CreateDirectory(store.Folder); Process.Start("explorer.exe", "\"" + store.Folder + "\""); };
        btnClear.Click += ClearAll;
        clearTimer.Tick += (s, e) => ArmClear(false);

        list.Dock = DockStyle.Fill;
        list.BorderStyle = BorderStyle.None;
        list.DrawMode = DrawMode.OwnerDrawVariable;
        list.IntegralHeight = false;
        list.MeasureItem += (s, e) => e.ItemHeight = ((Entry)list.Items[e.Index]).Kind == Kind.Image ? D(88) : D(58);
        list.DrawItem += DrawItem;
        list.MouseMove += (s, e) =>
        {
            int i = list.IndexFromPoint(e.Location);
            bool x = i >= 0 && e.X >= list.ClientSize.Width - D(48);
            if (i != hover || x != hoverX) { hover = i; hoverX = x; list.Invalidate(); }
        };
        list.MouseLeave += (s, e) => { hover = -1; list.Invalidate(); };
        list.MouseClick += ListClick;
        list.KeyDown += ListKey;
        list.KeyPress += (s, e) =>
        {
            if (char.IsControl(e.KeyChar)) return;
            search.Focus();
            search.SelectedText = e.KeyChar.ToString();
            e.Handled = true;
        };
        empty.Dock = DockStyle.Fill;
        empty.TextAlign = ContentAlignment.MiddleCenter;

        preview.Dock = DockStyle.Fill;
        preview.Visible = false;
        preview.Cursor = Cursors.Hand;
        preview.Paint += PaintPreview;
        preview.Click += (s, e) => ShowPreview(null);

        Controls.AddRange(new Control[] { list, empty, settings, preview, searchBar, header });
        ApplyTheme();
        RefreshList();
        CreateHandle();
    }

    Button IconButton(string tip, EventHandler click)
    {
        var b = new Button { Font = Icons, Dock = DockStyle.Right, Width = D(40), FlatStyle = FlatStyle.Flat, AccessibleName = tip };
        b.FlatAppearance.BorderSize = 0;
        b.Click += click;
        tips.SetToolTip(b, tip);
        return b;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        AddClipboardFormatListener(Handle);
        RoundCorners(Handle);
    }

    public static void RoundCorners(IntPtr hwnd)
    {
        int round = 2;
        DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(int));
    }

    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x80; return cp; }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_CLIPBOARDUPDATE) OnClipboardChanged();
        base.WndProc(ref m);
    }

    void OnClipboardChanged()
    {
        if (GetClipboardSequenceNumber() == ownSeq) return;
        try
        {
            if (Clipboard.ContainsData("ExcludeClipboardContentFromMonitorProcessing")) return;
            DateTime now = DateTime.Now;
            if (Clipboard.ContainsFileDropList() && !Clipboard.ContainsImage())
            {
                StringCollection files = Clipboard.GetFileDropList();
                if (files.Count == 0) return;
                store.Add(Kind.Files, string.Join("\n", files.Cast<string>()), now);
            }
            else if (Clipboard.ContainsText())
            {
                string text = Clipboard.GetText();
                if (text.Trim().Length == 0) return;
                store.Add(Kind.Text, text, now);
            }
            else if (Clipboard.ContainsImage())
            {
                using (Image img = Clipboard.GetImage())
                {
                    if (img == null) return;
                    store.Add(Kind.Image, store.SaveImage(img), now);
                }
            }
            else return;
            store.Save();
            RefreshList();
        }
        catch (ExternalException) { }
        catch (IOException) { }
    }

    public void Toggle()
    {
        if (Visible || (DateTime.Now - hiddenAt).TotalMilliseconds < 300) Hide();
        else ShowPopup();
    }

    public void ShowPopup()
    {
        Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(wa.Right - Width - D(12), wa.Bottom - Height - D(12));
        hover = -1;
        search.Clear();
        ShowPreview(null);
        ShowSettings(false);
        Show();
        Activate();
        search.Focus();
    }

    public void Tick()
    {
        if (store.Prune(DateTime.Now)) { store.Save(); RefreshList(); }
        else if (Visible) list.Invalidate();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        Hide();
        hiddenAt = DateTime.Now;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            if (preview.Visible) ShowPreview(null);
            else if (settings.Visible) ShowSettings(false);
            else if (search.Text.Length > 0) search.Clear();
            else Hide();
            e.Handled = e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Enter && preview.Visible)
        {
            Use(previewEntry);
            e.Handled = e.SuppressKeyPress = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
        base.OnFormClosing(e);
    }

    void SearchKey(object sender, KeyEventArgs e)
    {
        if (list.Items.Count == 0) return;
        if (e.KeyCode == Keys.Down) { list.Focus(); list.SelectedIndex = 0; e.Handled = true; }
        else if (e.KeyCode == Keys.Enter)
        {
            if (!(e.Shift && ShiftAction((Entry)list.Items[0]))) Use((Entry)list.Items[0]);
            e.Handled = e.SuppressKeyPress = true;
        }
    }

    void ListKey(object sender, KeyEventArgs e)
    {
        int i = list.SelectedIndex;
        if (e.KeyCode == Keys.Up && i <= 0) { search.Focus(); e.Handled = true; }
        else if (i < 0) return;
        else if (e.KeyCode == Keys.Enter) { if (!(e.Shift && ShiftAction((Entry)list.Items[i]))) Use((Entry)list.Items[i]); }
        else if (e.KeyCode == Keys.Delete) Delete((Entry)list.Items[i]);
    }

    void ClearAll(object sender, EventArgs e)
    {
        if (store.Items.Count == 0) return;
        if (!btnClear.Danger)
        {
            ArmClear(true);
            clearTimer.Start();
            return;
        }
        ArmClear(false);
        store.Items.Clear();
        store.Save();
        RefreshList();
        storageRow.Desc = StorageText();
        settings.Invalidate();
    }

    void ArmClear(bool on)
    {
        clearTimer.Stop();
        btnClear.Danger = on;
        btnClear.Text = on ? "Confirmar" : "Limpar tudo";
        btnClear.Invalidate();
    }

    void ListClick(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        int i = list.IndexFromPoint(e.Location);
        if (i < 0 || !list.GetItemRectangle(i).Contains(e.Location)) return;
        var it = (Entry)list.Items[i];
        if (e.X >= list.ClientSize.Width - D(48)) Delete(it);
        else if (!((ModifierKeys & Keys.Shift) != 0 && ShiftAction(it))) Use(it);
    }

    void Use(Entry it)
    {
        try
        {
            if (it.Kind == Kind.Text) Clipboard.SetText(it.Data);
            else if (it.Kind == Kind.Files)
            {
                var files = new StringCollection();
                files.AddRange(it.Data.Split('\n'));
                Clipboard.SetFileDropList(files);
            }
            else using (var img = new Bitmap(new MemoryStream(File.ReadAllBytes(store.ImagePath(it.Data))))) Clipboard.SetImage(img);
        }
        catch (Exception) { return; }
        ownSeq = GetClipboardSequenceNumber();
        store.Items.Remove(it);
        store.Items.Insert(0, it);
        it.Time = DateTime.Now;
        store.Save();
        Hide();
    }

    void Delete(Entry it)
    {
        int i = list.Items.IndexOf(it);
        store.Items.Remove(it);
        store.Save();
        RefreshList();
        if (list.Items.Count > 0 && list.Focused) list.SelectedIndex = Math.Min(i, list.Items.Count - 1);
    }

    void RefreshList()
    {
        string q = search.Text.Trim();
        int top = list.TopIndex;
        list.BeginUpdate();
        list.Items.Clear();
        foreach (Entry it in store.Items)
            if (q.Length == 0 || (it.Kind != Kind.Image && it.Data.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0))
                list.Items.Add(it);
        list.EndUpdate();
        if (top < list.Items.Count) list.TopIndex = top;
        bool other = settings.Visible || preview.Visible;
        list.Visible = !other && list.Items.Count > 0;
        empty.Visible = !other && list.Items.Count == 0;
        empty.Text = q.Length > 0 ? "Nada encontrado." : "Nada copiado ainda.\nUse Ctrl+C em qualquer lugar —\ntexto, imagens ou arquivos.";
        header.Invalidate();
    }

    void ApplyTheme()
    {
        t = store.Dark ? DarkTheme : LightTheme;
        BackColor = t.Border;
        header.BackColor = searchBar.BackColor = settings.BackColor = preview.BackColor = list.BackColor = empty.BackColor = t.Bg;
        empty.ForeColor = t.Muted;
        search.BackColor = t.Field;
        search.ForeColor = t.Fg;
        btnSettings.ForeColor = t.Fg;
        btnSettings.BackColor = settings.Visible ? t.Hover : t.Bg;
        btnSettings.FlatAppearance.MouseOverBackColor = t.Hover;
        btnSettings.FlatAppearance.MouseDownBackColor = t.Border;
        foreach (Control c in settings.Controls) { c.BackColor = t.Bg; c.ForeColor = t.Fg; }
        startup.BackColor = t.Field;
        startup.On = t.Accent;
        startup.Off = t.Border;
        startup.Knob = Color.White;
        foreach (PillButton b in new[] { ddLimit, ddExpiry, ddTheme, btnFolder, btnClear }) { b.T = t; b.BackColor = t.Field; }
        ddTheme.SelectedIndex = store.Dark ? 1 : 0;
        SetWindowTheme(list.Handle, store.Dark ? "DarkMode_Explorer" : "Explorer", null);
        Invalidate(true);
    }
}
