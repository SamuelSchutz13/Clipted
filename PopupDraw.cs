using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

partial class Popup
{
    public static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        int d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    static void Fill(Graphics g, Color c, Rectangle r, int radius)
    {
        using (var b = new SolidBrush(c)) using (var p = RoundRect(r, radius)) g.FillPath(b, p);
    }

    void PaintHeader(object sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(logo, D(16), (header.Height - D(20)) / 2, D(20), D(20));
        var f = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
        int x = D(44);
        TextRenderer.DrawText(g, "Clipted", TitleFont, new Rectangle(x, 0, D(150), header.Height), t.Fg, f);
        x += TextRenderer.MeasureText(g, "Clipted", TitleFont, Size.Empty, f).Width + D(8);
        int n = store.Items.Count;
        TextRenderer.DrawText(g, n == 1 ? "1 item" : n + " itens", Small, new Rectangle(x, D(1), D(120), header.Height), t.Muted, f);
    }

    void PaintSearch(object sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var box = new Rectangle(D(12), D(4), searchBar.Width - D(24), D(36));
        Fill(e.Graphics, t.Field, box, D(8));
        TextRenderer.DrawText(e.Graphics, "\uE721", Icons, new Rectangle(box.X + D(4), box.Y, D(30), box.Height), t.Muted, Center);
    }

    Thumb GetThumb(string name)
    {
        Thumb th;
        if (thumbs.TryGetValue(name, out th)) return th;
        th = new Thumb();
        try
        {
            using (var src = Image.FromStream(new MemoryStream(File.ReadAllBytes(store.ImagePath(name)))))
            {
                th.Full = src.Size;
                float k = Math.Min(1f, Math.Min((float)D(104) / src.Width, (float)D(68) / src.Height));
                var bmp = new Bitmap(Math.Max(1, (int)(src.Width * k)), Math.Max(1, (int)(src.Height * k)));
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(src, 0, 0, bmp.Width, bmp.Height);
                }
                th.Img = bmp;
            }
        }
        catch (Exception) { }
        thumbs[name] = th;
        return th;
    }

    void DrawItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= list.Items.Count) return;
        var it = (Entry)list.Items[e.Index];
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle r = e.Bounds;
        bool active = e.Index == hover || (e.State & DrawItemState.Selected) != 0;
        using (var b = new SolidBrush(t.Bg)) g.FillRectangle(b, r);
        if (active) Fill(g, t.Hover, new Rectangle(r.X + D(6), r.Y + D(2), r.Width - D(12), r.Height - D(4)), D(8));

        string title, sub;
        Rectangle tile;
        if (it.Kind == Kind.Image)
        {
            Thumb th = GetThumb(it.Data);
            tile = new Rectangle(r.X + D(14), r.Y + (r.Height - D(68)) / 2, D(104), D(68));
            Fill(g, t.Tile, tile, D(6));
            if (th.Img != null)
                g.DrawImage(th.Img, tile.X + (tile.Width - th.Img.Width) / 2, tile.Y + (tile.Height - th.Img.Height) / 2, th.Img.Width, th.Img.Height);
            title = "Imagem";
            sub = (th.Img != null ? th.Full.Width + " × " + th.Full.Height + "  ·  " : "") + Ago(it.Time) + (active ? "  ·  Shift+clique amplia" : "");
        }
        else
        {
            tile = new Rectangle(r.X + D(14), r.Y + (r.Height - D(36)) / 2, D(36), D(36));
            Fill(g, t.Tile, tile, D(8));
            if (it.Kind == Kind.Files)
            {
                string[] paths = it.Data.Split('\n');
                TextRenderer.DrawText(g, "\uE8A5", Icons, tile, t.Accent, Center);
                title = Path.GetFileName(paths[0].TrimEnd('\\')) + (paths.Length > 1 ? "  +" + (paths.Length - 1) : "");
                sub = (paths.Length > 1 ? paths.Length + " arquivos" : Path.GetDirectoryName(paths[0].TrimEnd('\\'))) + "  ·  " + Ago(it.Time);
            }
            else
            {
                bool link = Link(it) != null;
                if (link) TextRenderer.DrawText(g, "", Icons, tile, t.Accent, Center);
                else TextRenderer.DrawText(g, "Aa", Bold, tile, t.Accent, Center);
                title = Regex.Replace(it.Data.Length > 400 ? it.Data.Substring(0, 400) : it.Data, @"\s+", " ").Trim();
                sub = Ago(it.Time) + (link && active ? "  ·  Shift+clique abre o link" : "");
            }
        }

        int x = tile.Right + D(12), w = r.Right - D(48) - x, mid = r.Y + r.Height / 2;
        TextRenderer.DrawText(g, title, Font, new Rectangle(x, mid - D(19), w, D(20)), t.Fg, OneLine);
        TextRenderer.DrawText(g, sub, Small, new Rectangle(x, mid + D(2), w, D(17)), t.Muted, OneLine);

        if (active)
        {
            var del = new Rectangle(r.Right - D(42), mid - D(15), D(30), D(30));
            if (hoverX && e.Index == hover) Fill(g, t.Border, del, D(15));
            TextRenderer.DrawText(g, "\uE711", Icons, del, t.Fg, Center);
        }
    }

    static string Ago(DateTime time)
    {
        TimeSpan d = DateTime.Now - time;
        if (d.TotalMinutes < 1) return "agora";
        if (d.TotalHours < 1) return "há " + (int)d.TotalMinutes + " min";
        if (d.TotalDays < 1) return "há " + (int)d.TotalHours + " h";
        return time.ToString("dd/MM/yyyy HH:mm");
    }
}
