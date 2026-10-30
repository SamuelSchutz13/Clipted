using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

partial class Popup
{
    void Section(string title)
    {
        if (cards.Count > 0) layoutY += D(12);
        sections.Add(new SettingRow { Title = title, R = new Rectangle(D(20), layoutY, D(300), D(26)) });
        layoutY += D(26);
        cards.Add(new Rectangle(D(12), layoutY, Width - Padding.Horizontal - D(24), 0));
    }

    SettingRow AddSetting(string title, string desc, params Control[] controls)
    {
        Rectangle card = cards[cards.Count - 1];
        var row = new SettingRow { Title = title, Desc = desc, R = new Rectangle(card.X, layoutY, card.Width, D(58)), First = layoutY == card.Y };
        int right = row.R.Right - D(14);
        for (int i = controls.Length - 1; i >= 0; i--)
        {
            Control c = controls[i];
            c.Location = new Point(right - c.Width, row.R.Y + (row.R.Height - c.Height) / 2);
            c.AccessibleName = title;
            right = c.Left - D(6);
            settings.Controls.Add(c);
        }
        row.TextWidth = right - row.R.X - D(20);
        rows.Add(row);
        layoutY += row.R.Height;
        cards[cards.Count - 1] = new Rectangle(card.X, card.Y, card.Width, layoutY - card.Y);
        return row;
    }

    void PaintSettings(object sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (SettingRow s in sections) TextRenderer.DrawText(g, s.Title, SectionFont, s.R, t.Muted, OneLine | TextFormatFlags.VerticalCenter);
        foreach (Rectangle c in cards) Fill(g, t.Field, c, D(10));
        using (var line = new Pen(t.Bg))
            foreach (SettingRow r in rows)
            {
                if (!r.First) g.DrawLine(line, r.R.X + D(14), r.R.Y, r.R.Right - D(14), r.R.Y);
                int x = r.R.X + D(14), mid = r.R.Y + r.R.Height / 2;
                TextRenderer.DrawText(g, r.Title, Font, new Rectangle(x, mid - D(19), r.TextWidth, D(20)), t.Fg, OneLine);
                TextRenderer.DrawText(g, r.Desc, Small, new Rectangle(x, mid + D(2), r.TextWidth, D(17)), t.Muted, OneLine);
            }
    }

    void ShowSettings(bool on)
    {
        if (on) ShowPreview(null);
        ArmClear(false);
        settings.Visible = on;
        searchBar.Visible = !on;
        if (on)
        {
            storageRow.Desc = StorageText();
            settings.Invalidate();
        }
        RefreshList();
        btnSettings.BackColor = on ? t.Hover : t.Bg;
    }

    string StorageText()
    {
        long bytes = 0;
        try { if (Directory.Exists(store.Folder)) bytes = new DirectoryInfo(store.Folder).GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); }
        catch (Exception) { }
        int n = store.Items.Count;
        return (n == 1 ? "1 item" : n + " itens") + "  ·  " + (bytes / 1048576.0).ToString("0.0") + " MB em disco";
    }

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool StartsWithWindows()
    {
        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue("Clipted") != null;
    }

    public static void SetStartup(bool on)
    {
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            if (on) k.SetValue("Clipted", "\"" + Application.ExecutablePath + "\"");
            else k.DeleteValue("Clipted", false);
        }
    }

    static List<int> Options(int[] presets, int current)
    {
        var list = presets.ToList();
        if (!list.Contains(current)) list.Add(current);
        return list.OrderBy(n => n == 0 ? int.MaxValue : n).ToList();
    }

    public static string ExpiryLabel(int minutes)
    {
        if (minutes == 0) return "Nunca";
        if (minutes % 1440 == 0) return minutes / 1440 + (minutes == 1440 ? " dia" : " dias");
        if (minutes % 60 == 0) return minutes / 60 + (minutes == 60 ? " hora" : " horas");
        return minutes + (minutes == 1 ? " minuto" : " minutos");
    }

    void SettingsChanged(object sender, EventArgs e)
    {
        store.Limit = limitValues[ddLimit.SelectedIndex];
        store.ExpiryMinutes = expiryValues[ddExpiry.SelectedIndex];
        store.Prune(DateTime.Now);
        store.Save();
        RefreshList();
    }
}
