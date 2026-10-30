using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

partial class Popup
{
    public static Uri Link(Entry it)
    {
        if (it.Kind != Kind.Text || it.Data.Length > 2048) return null;
        string s = it.Data.Trim();
        if (s.Any(char.IsWhiteSpace)) return null;
        if (s.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) s = "https://" + s;
        Uri u;
        return Uri.TryCreate(s, UriKind.Absolute, out u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps) ? u : null;
    }

    bool ShiftAction(Entry it)
    {
        return it.Kind == Kind.Image ? ShowPreview(it) : OpenLink(it);
    }

    bool ShowPreview(Entry it)
    {
        if (previewImg != null) { previewImg.Dispose(); previewImg = null; }
        previewEntry = null;
        if (it != null)
        {
            try { previewImg = new Bitmap(new MemoryStream(File.ReadAllBytes(store.ImagePath(it.Data)))); }
            catch (Exception) { return false; }
            previewEntry = it;
        }
        preview.Visible = previewImg != null;
        searchBar.Visible = !preview.Visible && !settings.Visible;
        RefreshList();
        preview.Invalidate();
        return true;
    }

    void PaintPreview(object sender, PaintEventArgs e)
    {
        if (previewImg == null) return;
        Graphics g = e.Graphics;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        int hint = D(44);
        var area = new Rectangle(D(16), D(8), preview.Width - D(32), preview.Height - D(8) - hint);
        float k = Math.Min(1f, Math.Min((float)area.Width / previewImg.Width, (float)area.Height / previewImg.Height));
        int w = Math.Max(1, (int)(previewImg.Width * k)), h = Math.Max(1, (int)(previewImg.Height * k));
        var dest = new Rectangle(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h);
        g.DrawImage(previewImg, dest);
        using (var pen = new Pen(t.Border)) g.DrawRectangle(pen, dest.X - 1, dest.Y - 1, dest.Width + 1, dest.Height + 1);
        TextRenderer.DrawText(g, previewImg.Width + " × " + previewImg.Height + "   ·   Enter copia   ·   Esc ou clique volta", Small,
            new Rectangle(0, preview.Height - hint, preview.Width, hint), t.Muted, Center);
    }

    bool OpenLink(Entry it)
    {
        Uri u = Link(it);
        if (u == null) return false;
        try { Process.Start(u.AbsoluteUri); }
        catch (Exception) { return true; }
        Hide();
        return true;
    }
}
