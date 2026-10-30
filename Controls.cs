using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

class Theme { public Color Bg, Fg, Muted, Hover, Border, Field, Tile, Accent, Danger; }

class Thumb { public Image Img; public Size Full; }

class SettingRow { public Rectangle R; public string Title, Desc; public int TextWidth; public bool First; }

class PillButton : Control
{
    static readonly Font GlyphFont = new Font("Segoe MDL2 Assets", 9f);
    public Theme T;
    public string Glyph;
    public bool Danger;
    bool hot;

    public PillButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.PushButton;
    }

    protected virtual bool Pressed { get { return false; } }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, ClientRectangle);
        var box = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = Popup.RoundRect(box, Height / 4))
        using (var b = new SolidBrush(hot ? T.Hover : T.Bg))
        using (var pen = new Pen(Danger ? T.Danger : Focused || Pressed ? T.Accent : T.Border))
        {
            g.FillPath(b, path);
            g.DrawPath(pen, path);
        }
        PaintContent(g);
    }

    protected virtual void PaintContent(Graphics g)
    {
        Color c = Danger ? T.Danger : T.Fg;
        var f = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        if (Glyph == null)
        {
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, c, f | TextFormatFlags.HorizontalCenter);
            return;
        }
        int gw = TextRenderer.MeasureText(g, Glyph, GlyphFont, Size.Empty, f).Width;
        int tw = TextRenderer.MeasureText(g, Text, Font, Size.Empty, f).Width;
        int gap = Height / 5;
        int x = (Width - gw - gap - tw) / 2;
        TextRenderer.DrawText(g, Glyph, GlyphFont, new Rectangle(x, 0, gw, Height), c, f);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(x + gw + gap, 0, tw + 1, Height), c, f);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) OnClick(EventArgs.Empty);
        base.OnKeyDown(e);
    }

    protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
}

class Dropdown : PillButton
{
    static readonly Font Glyphs = new Font("Segoe MDL2 Assets", 8f);
    public string[] Options = new string[0];
    public event EventHandler SelectedIndexChanged;
    readonly ContextMenuStrip menu = new ContextMenuStrip();
    int index = -1;
    DateTime closedAt;

    public int SelectedIndex
    {
        get { return index; }
        set
        {
            if (index == value) return;
            index = value;
            Text = Options[value];
            Invalidate();
            if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
        }
    }

    public Dropdown()
    {
        AccessibleRole = AccessibleRole.ComboBox;
        menu.ShowImageMargin = false;
        menu.Padding = new Padding(0, 4, 0, 4);
        menu.Renderer = new MenuRenderer(this);
        menu.HandleCreated += (s, e) => Popup.RoundCorners(menu.Handle);
        menu.Closed += (s, e) => { closedAt = DateTime.Now; Invalidate(); };
    }

    public void Open()
    {
        menu.Items.Clear();
        menu.Font = Font;
        menu.MinimumSize = new Size(Width, 0);
        for (int i = 0; i < Options.Length; i++)
        {
            int n = i;
            var item = new ToolStripMenuItem(Options[i]) { Checked = i == index, AutoSize = false, Size = new Size(Width, Height), Margin = Padding.Empty };
            item.Click += (s, e) => SelectedIndex = n;
            menu.Items.Add(item);
        }
        menu.Show(this, new Point(0, Height + 4));
        Invalidate();
    }

    protected override bool Pressed { get { return menu.Visible; } }

    protected override void PaintContent(Graphics g)
    {
        var f = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
        TextRenderer.DrawText(g, index >= 0 ? Options[index] : "", Font, new Rectangle(Height / 3, 0, Width - Height, Height), T.Fg, f);
        TextRenderer.DrawText(g, menu.Visible ? "" : "", Glyphs, new Rectangle(Width - Height, 0, Height, Height), T.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button == MouseButtons.Left && (DateTime.Now - closedAt).TotalMilliseconds > 200) Open();
    }

    protected override bool IsInputKey(Keys k) { return k == Keys.Up || k == Keys.Down || base.IsInputKey(k); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Up && index > 0) SelectedIndex = index - 1;
        else if (e.KeyCode == Keys.Down && index < Options.Length - 1) SelectedIndex = index + 1;
        else if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter || e.KeyCode == Keys.F4) Open();
    }

    class MenuRenderer : ToolStripRenderer
    {
        static readonly Font Check = new Font("Segoe MDL2 Assets", 8f);
        readonly Dropdown owner;
        public MenuRenderer(Dropdown owner) { this.owner = owner; }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var b = new SolidBrush(owner.T.Bg)) e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (var p = new Pen(owner.T.Border)) e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
            if (e.Item.Selected) using (var b = new SolidBrush(owner.T.Hover)) using (var p = Popup.RoundRect(r, 4)) g.FillPath(b, p);
            if (((ToolStripMenuItem)e.Item).Checked)
                TextRenderer.DrawText(g, "", Check, new Rectangle(r.Right - r.Height, r.Y, r.Height, r.Height), owner.T.Accent,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            int h = e.Item.Height;
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, new Rectangle(owner.Height / 3, 0, e.Item.Width - owner.Height / 3 - h, h),
                ((ToolStripMenuItem)e.Item).Checked ? owner.T.Accent : owner.T.Fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }
}

class Toggle : Control
{
    public Color On, Off, Knob;
    public event EventHandler CheckedChanged;
    bool on;

    public bool Checked
    {
        get { return on; }
        set
        {
            if (on == value) return;
            on = value;
            AccessibleDescription = on ? "ligado" : "desligado";
            Invalidate();
            if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
        }
    }

    public Toggle()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.CheckButton;
        AccessibleDescription = "desligado";
    }

    protected override void OnClick(EventArgs e) { Focus(); Checked = !Checked; base.OnClick(e); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) Checked = !Checked; base.OnKeyDown(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, ClientRectangle);
        var track = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var b = new SolidBrush(Checked ? On : Off)) using (var p = Popup.RoundRect(track, track.Height / 2)) g.FillPath(b, p);
        int k = track.Height - 6;
        using (var b = new SolidBrush(Knob)) g.FillEllipse(b, Checked ? track.Right - k - 3 : 3, 3, k, k);
        if (Focused && ShowFocusCues) using (var pen = new Pen(On)) using (var p = Popup.RoundRect(track, track.Height / 2)) g.DrawPath(pen, p);
    }
}
