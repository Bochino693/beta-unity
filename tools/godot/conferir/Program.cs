using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;
using StrikeKids;

class ModelosBytes : IModelosCenario
{
    readonly string pasta;
    readonly Dictionary<string, (CaixaG c, V3[] p)> cache = new Dictionary<string, (CaixaG, V3[])>();
    public ModelosBytes(string pasta) { this.pasta = pasta; }
    public bool Existe(string nome) => File.Exists(Path.Combine(pasta, nome + ".bytes"));
    (CaixaG, V3[]) Ler(string nome)
    {
        if (cache.TryGetValue(nome, out var r)) return r;
        using var br = new BinaryReader(File.OpenRead(Path.Combine(pasta, nome + ".bytes")));
        br.ReadBytes(4);
        int np = br.ReadUInt16();
        V3 min = new V3(1e9f, 1e9f, 1e9f), max = new V3(-1e9f, -1e9f, -1e9f);
        V3[] primeira = null;
        for (int k = 0; k < np; k++)
        {
            br.ReadBytes(br.ReadByte()); br.ReadInt16(); br.ReadSingle(); br.ReadSingle(); br.ReadSingle(); br.ReadBytes(br.ReadByte());
            int fl = br.ReadByte(); int nv = (int)br.ReadUInt32(); float esc = br.ReadSingle();
            if (nv == 0) { br.ReadUInt32(); continue; }
            float q = esc / 32767f;
            var V = new V3[nv];
            for (int i = 0; i < nv; i++) { float x = br.ReadInt16() * q, y = br.ReadInt16() * q, z = br.ReadInt16() * q; V[i] = new V3(-x, y, z); }
            br.ReadBytes(nv * 3); br.ReadBytes(nv * 4); if ((fl & 1) != 0) br.ReadBytes(nv * 8);
            int ni = (int)br.ReadUInt32(); br.ReadBytes(ni * 2);
            foreach (var v in V) { for (int i = 0; i < 3; i++) { if (v[i] < min[i]) min[i] = v[i]; if (v[i] > max[i]) max[i] = v[i]; } }
            if (primeira == null) primeira = V;
        }
        r = (new CaixaG(min, max - min), primeira);
        cache[nome] = r;
        return r;
    }
    public CaixaG CaixaDe(string nome) => Ler(nome).Item1;
    public V3[] Pontos(string nome) => Ler(nome).Item2;
}

static class Programa
{
    static string F(float v) => v.ToString("0.0000", CultureInfo.InvariantCulture);
    static void Despejar(string tema, No no, Pose pai, string nivel, StringBuilder sb)
    {
        foreach (var c in no.filhos)
        {
            var xf = pai * c.Transform;
            string n = c.nivel == 1 ? "A" : c.nivel == 2 ? "L" : nivel;
            if (c.malha != null)
            {
                var b = xf.b;
                sb.AppendLine($"P|{tema}|{n}|{c.malha}{(c.vento != null ? "v" : "")}|{F(xf.o.x)} {F(xf.o.y)} {F(xf.o.z)}|{F(b.xx)} {F(b.xy)} {F(b.xz)} {F(b.yx)} {F(b.yy)} {F(b.yz)} {F(b.zx)} {F(b.zy)} {F(b.zz)}");
            }
            else if (c.material == "borboleta" || c.material == "fio")
                sb.AppendLine($"P|{tema}|{n}|?{c.nome}|{F(xf.o.x)} {F(xf.o.y)} {F(xf.o.z)}|{F(xf.b.xx)} {F(xf.b.xy)} {F(xf.b.xz)} {F(xf.b.yx)} {F(xf.b.yy)} {F(xf.b.yz)} {F(xf.b.zx)} {F(xf.b.zy)} {F(xf.b.zz)}");
            else if (c.material == "particulas")
                sb.AppendLine($"PART|{tema}|{xf.o.x.ToString("0.000", CultureInfo.InvariantCulture)} {xf.o.y.ToString("0.000", CultureInfo.InvariantCulture)} {xf.o.z.ToString("0.000", CultureInfo.InvariantCulture)}");
            Despejar(tema, c, xf, n, sb);
        }
    }
    static void Main(string[] a)
    {
        var r = new PlanoCenario.Sorteio(2026);
        Console.Error.WriteLine("RANDF " + string.Join(" ", new[] { r.Randf(), r.Randf(), r.Randf() }));
        var r2 = new PlanoCenario.Sorteio(77);
        Console.Error.WriteLine($"RANGE {r2.Faixa(3.6, 15.0):0.000000000} {r2.Faixa(-34.0, 7.0):0.000000000}");
        var m = new ModelosBytes(a[0]);
        var t0 = DateTime.Now;
        var p = new PlanoCenario(m, Environment.GetEnvironmentVariable("CENARIO") != "completo", Environment.GetEnvironmentVariable("DETALHES") != "0");
        p.Construir();
        Console.Error.WriteLine("plano em " + (DateTime.Now - t0).TotalMilliseconds + " ms");
        var sb = new StringBuilder();
        foreach (var tema in PlanoCenario.ORDEM)
        {
            Despejar(tema, p.grupos[tema], Pose.Identidade, "-", sb);
            var tipos = new List<string>(); foreach (var it in p.anim[tema]) tipos.Add(it.tipo);
            sb.AppendLine($"ANIM|{tema}|{string.Join(",", tipos)}");
            var sons = new List<string>(); foreach (var v in p.vozes[tema]) sons.Add(v.som);
            sb.AppendLine($"VOZ|{tema}|{string.Join(",", sons)}");
        }
        foreach (var w in p.avisos) Console.Error.WriteLine("AVISO " + w);
        Console.Write(sb.ToString());
    }
}
