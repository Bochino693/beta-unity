using System;
using System.Collections.Generic;

namespace StrikeKids
{
    // A GEOMETRIA DA EDIÇÃO GODOT, EM C# PURO (sem a Unity): os mesmos
    // Vector2/Vector3/Basis/Transform/AABB/Spatial do Godot 3, com as mesmas
    // contas (o giro por eixo e ângulo, o Euler YXZ, a escala da base). O
    // cenário é PLANEJADO nas coordenadas do Godot (PlanoCenario.cs, a cópia
    // do temas.gd da 7.6) e só na hora de pôr na tela vira Unity (Temas.cs:
    // o X espelhado, Medidas.G). Assim o plano pode ser conferido peça por
    // peça contra o do Godot (tools/godot/conferir_cenario.*), sem a Unity.

    /// <summary>Vector2 do Godot (float).</summary>
    public struct V2
    {
        public float x, y;
        public V2(float x, float y) { this.x = x; this.y = y; }
        public V2(double x, double y) { this.x = (float)x; this.y = (float)y; }
        public static readonly V2 Zero = new V2(0f, 0f);
        public static V2 operator +(V2 a, V2 b) => new V2(a.x + b.x, a.y + b.y);
        public static V2 operator -(V2 a, V2 b) => new V2(a.x - b.x, a.y - b.y);
        public static V2 operator *(V2 a, float k) => new V2(a.x * k, a.y * k);
        public static V2 operator /(V2 a, float k) => new V2(a.x / k, a.y / k);
        public float Length() => (float)Math.Sqrt(x * x + y * y);
        public float Dot(V2 b) => x * b.x + y * b.y;
        public float DistanceTo(V2 b) => (this - b).Length();
        public V2 Lerp(V2 b, float t) => new V2(x + (b.x - x) * t, y + (b.y - y) * t);
    }

    /// <summary>Vector3 do Godot (float).</summary>
    public struct V3
    {
        public float x, y, z;
        public V3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public V3(double x, double y, double z) { this.x = (float)x; this.y = (float)y; this.z = (float)z; }
        public static readonly V3 Zero = new V3(0f, 0f, 0f);
        public static readonly V3 Um = new V3(1f, 1f, 1f);
        public static readonly V3 Cima = new V3(0f, 1f, 0f);
        public static readonly V3 Direita = new V3(1f, 0f, 0f);
        public static readonly V3 Frente = new V3(0f, 0f, -1f);   // Vector3.FORWARD
        public static V3 operator +(V3 a, V3 b) => new V3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static V3 operator -(V3 a, V3 b) => new V3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static V3 operator -(V3 a) => new V3(-a.x, -a.y, -a.z);
        public static V3 operator *(V3 a, float k) => new V3(a.x * k, a.y * k, a.z * k);
        public static V3 operator *(V3 a, V3 b) => new V3(a.x * b.x, a.y * b.y, a.z * b.z);
        public static V3 operator /(V3 a, float k) => new V3(a.x / k, a.y / k, a.z / k);
        public float this[int i]
        {
            get => i == 0 ? x : (i == 1 ? y : z);
            set { if (i == 0) x = value; else if (i == 1) y = value; else z = value; }
        }
        public float Length() => (float)Math.Sqrt(x * x + y * y + z * z);
        public float Dot(V3 b) => x * b.x + y * b.y + z * b.z;
        public V3 Cross(V3 b) => new V3(y * b.z - z * b.y, z * b.x - x * b.z, x * b.y - y * b.x);
        public V3 Normalized()
        {
            float l = x * x + y * y + z * z;
            if (l == 0f) return Zero;
            l = (float)Math.Sqrt(l);
            return new V3(x / l, y / l, z / l);
        }
        public float DistanceTo(V3 b) => (this - b).Length();
        public override string ToString() => string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###}, {2:0.###})", x, y, z);
    }

    /// <summary>Uma cor (r, g, b, a) do Godot.</summary>
    public struct Cor
    {
        public float r, g, b, a;
        public Cor(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static readonly Cor Branco = new Cor(1f, 1f, 1f, 1f);
        public static Cor Hex(string h)
        {
            h = h.TrimStart('#');
            int v = Convert.ToInt32(h.Substring(0, 6), 16);
            float a = h.Length >= 8 ? Convert.ToInt32(h.Substring(6, 2), 16) / 255f : 1f;
            return new Cor(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, a);
        }
        public bool Igual(Cor o) => r == o.r && g == o.g && b == o.b && a == o.a;
        /// <summary>O to_html() do Godot (a chave dos materiais).</summary>
        public string Html()
        {
            int C(float v) => Math.Max(0, Math.Min(255, (int)Math.Round(v * 255f)));
            return string.Format("{0:x2}{1:x2}{2:x2}{3:x2}", C(a), C(r), C(g), C(b));
        }
    }

    /// <summary>Basis do Godot 3 (linhas: m[linha, coluna]).</summary>
    public struct Base
    {
        public float xx, xy, xz, yx, yy, yz, zx, zy, zz;   // elements[0..2][0..2]
        public static readonly Base Identidade = new Base { xx = 1f, yy = 1f, zz = 1f };

        public float this[int l, int c]
        {
            get
            {
                switch (l * 3 + c)
                {
                    case 0: return xx; case 1: return xy; case 2: return xz;
                    case 3: return yx; case 4: return yy; case 5: return yz;
                    case 6: return zx; case 7: return zy; default: return zz;
                }
            }
            set
            {
                switch (l * 3 + c)
                {
                    case 0: xx = value; break; case 1: xy = value; break; case 2: xz = value; break;
                    case 3: yx = value; break; case 4: yy = value; break; case 5: yz = value; break;
                    case 6: zx = value; break; case 7: zy = value; break; default: zz = value; break;
                }
            }
        }

        /// <summary>Basis(eixo, ângulo): a mesma conta do Godot (set_axis_angle).</summary>
        public static Base Giro(V3 e, double angulo)
        {
            var b = new Base();
            float phi = (float)angulo;   // (o Godot recebe o ângulo em float)
            float cosine = (float)Math.Cos(phi);
            float sine = (float)Math.Sin(phi);
            float sx = e.x * e.x, sy = e.y * e.y, sz = e.z * e.z;
            b.xx = sx + cosine * (1f - sx);
            b.yy = sy + cosine * (1f - sy);
            b.zz = sz + cosine * (1f - sz);
            float t = 1f - cosine;
            float xyzt = e.x * e.y * t, zyxs = e.z * sine;
            b.xy = xyzt - zyxs;
            b.yx = xyzt + zyxs;
            xyzt = e.x * e.z * t; zyxs = e.y * sine;
            b.xz = xyzt + zyxs;
            b.zx = xyzt - zyxs;
            xyzt = e.y * e.z * t; zyxs = e.x * sine;
            b.yz = xyzt - zyxs;
            b.zy = xyzt + zyxs;
            return b;
        }

        /// <summary>Basis(Vector3 euler): Euler YXZ (Y * X * Z).</summary>
        public static Base Euler(V3 e)
        {
            float c = (float)Math.Cos(e.x), s = (float)Math.Sin(e.x);
            var xm = new Base { xx = 1f, yy = c, yz = -s, zy = s, zz = c };
            c = (float)Math.Cos(e.y); s = (float)Math.Sin(e.y);
            var ym = new Base { xx = c, xz = s, yy = 1f, zx = -s, zz = c };
            c = (float)Math.Cos(e.z); s = (float)Math.Sin(e.z);
            var zm = new Base { xx = c, xy = -s, yx = s, yy = c, zz = 1f };
            return ym * xm * zm;
        }

        public static Base Diagonal(V3 s) => new Base { xx = s.x, yy = s.y, zz = s.z };

        public static Base operator *(Base a, Base b)
        {
            var r = new Base();
            for (int l = 0; l < 3; l++)
                for (int c = 0; c < 3; c++)
                    r[l, c] = a[l, 0] * b[0, c] + a[l, 1] * b[1, c] + a[l, 2] * b[2, c];
            return r;
        }

        public V3 Xform(V3 v) => new V3(xx * v.x + xy * v.y + xz * v.z, yx * v.x + yy * v.y + yz * v.z, zx * v.x + zy * v.y + zz * v.z);

        /// <summary>basis.scaled(s): cada LINHA vezes a escala (S * B).</summary>
        public Base Scaled(V3 s)
        {
            var b = this;
            b.xx *= s.x; b.xy *= s.x; b.xz *= s.x;
            b.yx *= s.y; b.yy *= s.y; b.yz *= s.y;
            b.zx *= s.z; b.zy *= s.z; b.zz *= s.z;
            return b;
        }

        public V3 Coluna(int c) => new V3(this[0, c], this[1, c], this[2, c]);

        public void PorColuna(int c, V3 v) { this[0, c] = v.x; this[1, c] = v.y; this[2, c] = v.z; }

        public float Determinante() =>
            xx * (yy * zz - zy * yz) - yx * (xy * zz - zy * xz) + zx * (xy * yz - yy * xz);

        public V3 Escala()
        {
            float sinal = Math.Sign(Determinante());
            return new V3(Coluna(0).Length(), Coluna(1).Length(), Coluna(2).Length()) * sinal;
        }

        public Base Ortonormal()
        {
            V3 x = Coluna(0), y = Coluna(1), z = Coluna(2);
            x = x.Normalized();
            y = y - x * x.Dot(y);
            y = y.Normalized();
            z = z - x * x.Dot(z) - y * y.Dot(z);
            z = z.Normalized();
            var b = new Base();
            b.PorColuna(0, x);
            b.PorColuna(1, y);
            b.PorColuna(2, z);
            return b;
        }

        /// <summary>get_euler_yxz (o get_euler do Godot 3).</summary>
        public V3 EulerYXZ()
        {
            float m12 = yz;
            if (m12 < 1f)
            {
                if (m12 > -1f)
                {
                    if (yx == 0f && xy == 0f && xz == 0f && zx == 0f && xx == 1f)
                        return new V3((float)Math.Atan2(-m12, yy), 0f, 0f);
                    return new V3((float)Math.Asin(-m12), (float)Math.Atan2(xz, zz), (float)Math.Atan2(yx, yy));
                }
                return new V3((float)(Math.PI * 0.5), (float)-Math.Atan2(-xy, xx), 0f);
            }
            return new V3((float)(-Math.PI * 0.5), (float)-Math.Atan2(-xy, xx), 0f);
        }

        /// <summary>get_rotation: o Euler da base ortonormal (sem a escala).</summary>
        public V3 Rotacao()
        {
            var m = Ortonormal();
            if (m.Determinante() < 0f) m = m.Scaled(new V3(-1f, -1f, -1f));
            return m.EulerYXZ();
        }

        /// <summary>set_euler_scale: Basis(euler) * escala.</summary>
        public static Base EulerEscala(V3 euler, V3 esc) => Euler(euler) * Diagonal(esc);
    }

    /// <summary>Transform do Godot 3.</summary>
    public struct Pose
    {
        public Base b;
        public V3 o;
        public Pose(Base b, V3 o) { this.b = b; this.o = o; }
        public static readonly Pose Identidade = new Pose(Base.Identidade, V3.Zero);
        public static Pose operator *(Pose a, Pose c) => new Pose(a.b * c.b, a.b.Xform(c.o) + a.o);
        public V3 Xform(V3 v) => b.Xform(v) + o;

        public CaixaG Xform(CaixaG c)
        {
            V3 min = c.pos, max = c.pos + c.tam;
            V3 tmin = o, tmax = o;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    float e = b[i, j] * min[j];
                    float f = b[i, j] * max[j];
                    if (e < f) { tmin[i] += e; tmax[i] += f; }
                    else { tmin[i] += f; tmax[i] += e; }
                }
            }
            return new CaixaG(tmin, tmax - tmin);
        }
    }

    /// <summary>AABB do Godot.</summary>
    public struct CaixaG
    {
        public V3 pos, tam;
        public CaixaG(V3 pos, V3 tam) { this.pos = pos; this.tam = tam; }
        public V3 Fim => pos + tam;
    }

    /// <summary>
    /// UM NÓ DO CENÁRIO (o Spatial / MeshInstance do Godot): a pose local com
    /// as mesmas regras — mexer na translação, no giro (Euler YXZ) ou na
    /// escala mexe na transformação, e ler o giro depois de pôr uma
    /// transformação inteira dá o Euler dela (como o Spatial guarda os dois).
    /// </summary>
    public class No
    {
        public string nome = "";
        public No pai;
        public readonly List<No> filhos = new List<No>();

        // o que ele mostra
        public string malha;           // a malha do nível alto (Malhas/cenario/<malha>), null = só um nó
        public string malhaLeve;       // a do nível leve (a mesma = não troca)
        public string modelo;          // o modelo (sem o sufixo do nível)
        public string material = "";  // "atlas" | "asas" | "nuvem" | "fio" | "borboleta" | "lampadas" | "particulas" | ""
        public string temaAtlas = "";
        public Cor tinta = Cor.Branco;
        public float borda = 0.16f;    // parâmetros do material (o resto vem do padrão)
        public float luzMinima;
        public float[] vento;          // [metros no topo, rapidez] = balança ao vento
        public bool materialProprio;   // um material só dele (o cristal que brilha)
        public int variante;           // (borboleta: qual das malhas)
        public bool mexe;              // se mexe (fica fora do lote)
        public bool troca;             // troca de malha com o nível (as que se mexem)
        public int nivel;              // 0 = nos dois níveis, 1 = só no alto, 2 = só no leve
        public object dados;           // partículas, lâmpadas, o fio...
        public object vivo;            // a peça na tela (Temas.cs)

        Pose _local = Pose.Identidade;
        V3 _rot, _esc = V3.Um;
        bool _sujo;

        public No() { }
        public No(string nome) { this.nome = nome; }

        public void Juntar(No filho)
        {
            filho.pai = this;
            filhos.Add(filho);
        }

        public void Tirar(No filho)
        {
            filhos.Remove(filho);
            filho.pai = null;
        }

        void Vetores()
        {
            if (!_sujo) return;
            _esc = _local.b.Escala();
            _rot = _local.b.Rotacao();
            _sujo = false;
        }

        public Pose Transform
        {
            get => _local;
            set { _local = value; _sujo = true; }
        }

        public V3 Translation
        {
            get => _local.o;
            set => _local.o = value;
        }

        public V3 Rotation
        {
            get { Vetores(); return _rot; }
            set { Vetores(); _rot = value; _local.b = Base.EulerEscala(_rot, _esc); }
        }

        public V3 Scale
        {
            get { Vetores(); return _esc; }
            set { Vetores(); _esc = value; _local.b = Base.EulerEscala(_rot, _esc); }
        }

        public void RotY(float y) { var r = Rotation; r.y = y; Rotation = r; }
        public void RotZ(float z) { var r = Rotation; r.z = z; Rotation = r; }

        /// <summary>rotate_y: gira em volta do Y do mundo (do pai).</summary>
        public void RotateY(float a)
        {
            _local.b = Base.Giro(V3.Cima, a) * _local.b;
            _sujo = true;
        }

        /// <summary>rotate_object_local: gira em volta de um eixo da própria peça.</summary>
        public void RotateObjectLocal(V3 eixo, float a)
        {
            _local.b = _local.b * Base.Giro(eixo, a);
            _sujo = true;
        }

        /// <summary>A pose no grupo da fase (o grupo e os de cima não têm transformação).</summary>
        public Pose Global => pai == null ? _local : pai.Global * _local;

        /// <summary>A pose nas coordenadas do Godot já como vetores (para a Unity).</summary>
        public void Partes(out V3 pos, out V3 rot, out V3 esc)
        {
            Vetores();
            pos = _local.o;
            rot = _rot;
            esc = _esc;
        }
    }
}
