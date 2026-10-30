using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

enum Kind { Text, Image, Files }

class Entry
{
    public Kind Kind;
    public string Data;
    public DateTime Time;
    public override string ToString() { return Kind == Kind.Image ? "Imagem" : Kind == Kind.Files ? "Arquivos: " + Data : Data; }
}

class Store
{
    public List<Entry> Items = new List<Entry>();
    public int Limit = 200;
    public int ExpiryValue = 0;
    public int ExpiryUnit = 2;
    public bool Dark;
    readonly string dir;

    public Store(string dir) { this.dir = dir; }

    public string Folder { get { return dir; } }

    public int ExpiryMinutes
    {
        get { return ExpiryValue * (ExpiryUnit == 0 ? 1 : ExpiryUnit == 1 ? 60 : 1440); }
        set
        {
            if (value % 1440 == 0) { ExpiryValue = value / 1440; ExpiryUnit = 2; }
            else if (value % 60 == 0) { ExpiryValue = value / 60; ExpiryUnit = 1; }
            else { ExpiryValue = value; ExpiryUnit = 0; }
        }
    }
    public string ImageDir { get { return Path.Combine(dir, "images"); } }
    public string ImagePath(string name) { return Path.Combine(ImageDir, name + ".png"); }

    public void Add(Kind kind, string data, DateTime now)
    {
        Items.RemoveAll(e => e.Kind == kind && e.Data == data);
        Items.Insert(0, new Entry { Kind = kind, Data = data, Time = now });
        Prune(now);
    }

    public string SaveImage(Image img)
    {
        byte[] png;
        using (var ms = new MemoryStream()) { img.Save(ms, ImageFormat.Png); png = ms.ToArray(); }
        string name;
        using (var sha = SHA1.Create()) name = BitConverter.ToString(sha.ComputeHash(png)).Replace("-", "").ToLowerInvariant();
        Directory.CreateDirectory(ImageDir);
        if (!File.Exists(ImagePath(name))) File.WriteAllBytes(ImagePath(name), png);
        return name;
    }

    public bool Prune(DateTime now)
    {
        int before = Items.Count;
        if (ExpiryValue > 0)
        {
            DateTime cut = now.AddMinutes(-ExpiryMinutes);
            Items.RemoveAll(e => e.Time < cut);
        }
        if (Limit > 0 && Items.Count > Limit) Items.RemoveRange(Limit, Items.Count - Limit);
        return Items.Count != before;
    }

    public void Load()
    {
        object light = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
        Dark = light is int && (int)light == 0;

        string s = Path.Combine(dir, "settings.txt");
        if (File.Exists(s))
            foreach (string line in File.ReadAllLines(s))
            {
                string[] kv = line.Split('=');
                int n;
                if (kv.Length != 2 || !int.TryParse(kv[1], out n)) continue;
                if (kv[0] == "limit") Limit = Math.Min(Math.Max(n, 0), 100000);
                else if (kv[0] == "expiryValue") ExpiryValue = Math.Min(Math.Max(n, 0), 9999);
                else if (kv[0] == "expiryUnit") ExpiryUnit = Math.Min(Math.Max(n, 0), 2);
                else if (kv[0] == "dark") Dark = n == 1;
            }

        string h = Path.Combine(dir, "history.txt");
        if (File.Exists(h))
            foreach (string line in File.ReadAllLines(h))
            {
                string[] p = line.Split(' ');
                long ticks;
                int kind = 0;
                if (p.Length < 2 || p.Length > 3 || !long.TryParse(p[0], out ticks)) continue;
                if (p.Length == 3 && (!int.TryParse(p[1], out kind) || kind < 0 || kind > 2)) continue;
                try { Items.Add(new Entry { Time = new DateTime(ticks), Kind = (Kind)kind, Data = Encoding.UTF8.GetString(Convert.FromBase64String(p[p.Length - 1])) }); }
                catch (Exception) { }
            }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(dir);
            Write("settings.txt", new[] { "limit=" + Limit, "expiryValue=" + ExpiryValue, "expiryUnit=" + ExpiryUnit, "dark=" + (Dark ? 1 : 0) });
            Write("history.txt", Items.Select(e => e.Time.Ticks + " " + (int)e.Kind + " " + Convert.ToBase64String(Encoding.UTF8.GetBytes(e.Data))));
            if (Directory.Exists(ImageDir))
            {
                var keep = new HashSet<string>(Items.Where(e => e.Kind == Kind.Image).Select(e => ImagePath(e.Data)), StringComparer.OrdinalIgnoreCase);
                foreach (string f in Directory.GetFiles(ImageDir, "*.png")) if (!keep.Contains(f)) File.Delete(f);
            }
        }
        catch (Exception) { }
    }

    void Write(string name, IEnumerable<string> lines)
    {
        string path = Path.Combine(dir, name), tmp = path + ".tmp";
        File.WriteAllLines(tmp, lines);
        if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
    }
}
