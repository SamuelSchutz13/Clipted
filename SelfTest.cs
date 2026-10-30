using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

static partial class Program
{
    static int SelfTest()
    {
        string dir = Path.Combine(Path.GetTempPath(), "clipted-test-" + Guid.NewGuid());
        try
        {
            var s = new Store(dir) { Limit = 3 };
            var t0 = new DateTime(2026, 1, 1, 12, 0, 0);
            s.Add(Kind.Text, "a", t0); s.Add(Kind.Text, "b", t0); s.Add(Kind.Text, "a", t0);
            Check(s.Items.Count == 2 && s.Items[0].Data == "a", "item repetido sobe pro topo sem duplicar");
            s.Add(Kind.Files, @"C:\x\a.txt" + "\n" + @"C:\x\b.txt", t0); s.Add(Kind.Text, "d\r\nlinha 2 ção", t0);
            Check(s.Items.Count == 3 && s.Items[2].Data == "a", "limite remove os mais antigos");
            s.ExpiryValue = 1; s.ExpiryUnit = 1;
            s.Items[2].Time = t0.AddHours(-2);
            Check(s.Prune(t0) && s.Items.Count == 2, "expiração remove itens velhos");

            using (var bmp = new Bitmap(3, 2))
            {
                string n1 = s.SaveImage(bmp), n2 = s.SaveImage(bmp);
                Check(n1 == n2 && File.Exists(s.ImagePath(n1)), "mesma imagem vira o mesmo arquivo");
                s.Limit = 10;
                s.Add(Kind.Image, n1, t0); s.Add(Kind.Image, n2, t0);
                Check(s.Items.Count == 3 && s.Items[0].Kind == Kind.Image, "imagem repetida não duplica");
                s.Save();
                Check(File.Exists(s.ImagePath(n1)), "imagem do histórico é mantida");
                s.Items.RemoveAt(0);
                s.Save();
                Check(!File.Exists(s.ImagePath(n1)), "imagem excluída é apagada do disco");
            }

            var r = new Store(dir);
            r.Load();
            Check(r.Items.Count == 2 && r.Items[0].Data == "d\r\nlinha 2 ção" && r.Items[0].Time == t0, "histórico salvo e recarregado");
            Check(r.Items[1].Kind == Kind.Files && r.Items[1].Data.Split('\n').Length == 2, "arquivos salvos e recarregados");
            Check(r.Limit == 10 && r.ExpiryValue == 1 && r.ExpiryUnit == 1, "configurações salvas e recarregadas");

            File.WriteAllText(Path.Combine(dir, "history.txt"), t0.Ticks + " " + Convert.ToBase64String(Encoding.UTF8.GetBytes("antigo")) + "\r\n");
            var old = new Store(dir);
            old.Load();
            Check(old.Items.Count == 1 && old.Items[0].Kind == Kind.Text && old.Items[0].Data == "antigo", "formato da versão anterior ainda carrega");

            Func<string, bool> isLink = text => Popup.Link(new Entry { Kind = Kind.Text, Data = text }) != null;
            Check(isLink(" https://www.youtube.com/ ") && isLink("http://x.com/a?b=1") && isLink("www.google.com"), "links reconhecidos");
            var e1 = new Store(dir) { ExpiryMinutes = 10080 };
            Check(e1.ExpiryValue == 7 && e1.ExpiryUnit == 2 && e1.ExpiryMinutes == 10080, "7 dias vira valor 7 + unidade dias");
            e1.ExpiryMinutes = 720;
            Check(e1.ExpiryValue == 12 && e1.ExpiryUnit == 1, "12 horas vira valor 12 + unidade horas");
            Check(Popup.ExpiryLabel(0) == "Nunca" && Popup.ExpiryLabel(60) == "1 hora" && Popup.ExpiryLabel(43200) == "30 dias" && Popup.ExpiryLabel(90) == "90 minutos", "rótulos de tempo");

            Check(!isLink("veja https://x.com") && !isLink("javascript:alert(1)") && !isLink(@"file:///C:/x.txt") && !isLink("texto"), "não-links ignorados");

            Console.WriteLine("selftest OK");
            return 0;
        }
        catch (Exception ex) { Console.WriteLine("selftest FALHOU: " + ex.Message); return 1; }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    static void Check(bool ok, string what) { if (!ok) throw new Exception(what); }
}
