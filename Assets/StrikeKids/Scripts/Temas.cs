using System;
using System.Collections.Generic;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// OS 4 CENÁRIOS — um por rodada (e a atração vai passando por todos).
    /// Cada um tem céu, luz, neblina, chão e cores da pista próprios (CONFIG,
    /// aplicado pelo Mundo) e um grupo de decoração que se mexe:
    ///   parque — Parque de Diversões: roda-gigante, carrossel, tendas, balões
    ///   espaco — Viagem ao Espaço: planeta com anel, foguetes, disco voador
    ///   mar    — Fundo do Mar: navio pirata, corais, algas, peixinhos, bolhas
    ///   dinos  — Selva dos Dinos: vulcão, dinossauros, palmeiras, pterossauros
    ///
    /// As posições vêm das edições Godot (temas.gd), com o X espelhado
    /// (Medidas.G). O que fica parado em cada cenário vira poucas malhas
    /// (lote estático da Unity): poucos desenhos por quadro.
    /// </summary>
    public class Temas : MonoBehaviour
    {
        /// <summary>a Selva dos Dinos é sempre a PRIMEIRA fase (e o começo da atração)</summary>
        public static readonly string[] ORDEM = { "dinos", "parque", "espaco", "mar" };

        public class Config
        {
            public Color[] ceu;            // topo, meio, horizonte, chão
            public Color ambiente; public float ambienteForca;
            public Color neblina; public float neblinaDensidade;
            public Color sol; public float solForca;
            public Color corSol = new Color(1f, 0.95f, 0.75f); public float forcaSol = 1f;
            public float estrelas, raios;
            public Color neon, bumper1, bumper2;
            public Color[] chao;           // base, base escura, detalhe, detalhe 2
            public Color[] portal;
        }

        static Color H(string h) => Materiais.Hex(h);

        public static readonly Dictionary<string, Config> CONFIG = new Dictionary<string, Config>
        {
            ["parque"] = new Config
            {
                ceu = new[] { H("#2F7FFF"), H("#6CBBFF"), H("#E4F4FF"), H("#8CD18C") },
                ambiente = new Color(0.62f, 0.7f, 0.86f), ambienteForca = 0.62f,
                neblina = new Color(0.84f, 0.92f, 1.0f), neblinaDensidade = 0.0045f,
                sol = new Color(1.0f, 0.95f, 0.86f), solForca = 1.05f,
                neon = H("#FF4FC8"), bumper1 = H("#FFFFFF"), bumper2 = H("#FF3B50"),
                chao = new[] { H("#6BCB5D"), H("#5BB54E"), H("#FFFFFF"), H("#FFE873") },
                portal = new[] { H("#FFC21A"), H("#E8384F") },
            },
            ["espaco"] = new Config
            {
                ceu = new[] { H("#040A26"), H("#0E2560"), H("#2C6AB8"), H("#14244E") },
                estrelas = 1f, corSol = H("#CFE0FF"), forcaSol = 0.35f,
                ambiente = new Color(0.6f, 0.7f, 1.0f), ambienteForca = 0.78f,
                neblina = new Color(0.14f, 0.24f, 0.5f), neblinaDensidade = 0.006f,
                sol = new Color(0.9f, 0.94f, 1.0f), solForca = 1.05f,
                neon = H("#4FE3FF"), bumper1 = H("#FFFFFF"), bumper2 = H("#3B8BFF"),
                chao = new[] { H("#9AA8C4"), H("#8494B3"), H("#C3CEE2"), H("#E2F5FF") },
                portal = new[] { H("#4FD8FF"), H("#3B2FA8") },
            },
            ["mar"] = new Config
            {
                ceu = new[] { H("#0A4F86"), H("#1A94C0"), H("#86E6DE"), H("#E8D29A") },
                raios = 1f, corSol = H("#E6FFF8"), forcaSol = 0.5f,
                ambiente = new Color(0.5f, 0.82f, 0.92f), ambienteForca = 0.72f,
                neblina = new Color(0.32f, 0.72f, 0.84f), neblinaDensidade = 0.014f,
                sol = new Color(0.88f, 1.0f, 0.96f), solForca = 1.0f,
                neon = H("#5CFFD8"), bumper1 = H("#FFFFFF"), bumper2 = H("#FF7A5C"),
                chao = new[] { H("#F9DCA2"), H("#EDC98A"), H("#FFB09E"), H("#9EDDE6") },
                portal = new[] { H("#2EE6C8"), H("#FF7A5C") },
            },
            ["dinos"] = new Config
            {
                ceu = new[] { H("#3FA7E8"), H("#9BD8F0"), H("#FFE4AE"), H("#6BBF59") },
                corSol = H("#FFE7B0"),
                ambiente = new Color(0.72f, 0.72f, 0.62f), ambienteForca = 0.62f,
                neblina = new Color(0.92f, 0.9f, 0.78f), neblinaDensidade = 0.006f,
                sol = new Color(1.0f, 0.93f, 0.8f), solForca = 1.1f,
                neon = H("#B6FF3B"), bumper1 = H("#FFE65A"), bumper2 = H("#2E9A3E"),
                chao = new[] { H("#46A44E"), H("#57B948"), H("#FF9F33"), H("#FFE65A") },
                portal = new[] { H("#9BE04A"), H("#FF8A1E") },
            },
        };

        public static readonly Color[] CORES_BICHOS = { H("#FF9F2E"), H("#FFD43B"), H("#4FD8FF"), H("#FF4B5C"), H("#8DFF6A"), H("#2E7BFF") };

        /// <summary>Lugares de destaque perto da pista (Godot: x, z, giro em radianos).</summary>
        static readonly float[][] DESTAQUES =
        {
            new[] { -2.55f, -1.2f, 0.3f }, new[] { 2.7f, -2.7f, -0.4f }, new[] { -3.9f, 3.4f, 0.4f }, new[] { 3.9f, 2.6f, 0.0f },
            new[] { 2.3f, -6.0f, 2.0f }, new[] { -2.3f, -6.8f, 1.0f }, new[] { -2.4f, -4.2f, 0.0f }, new[] { 2.45f, -8.7f, 0.5f },
        };

        /// <summary>Lugares atrás da mureta dos jogadores (Godot: x, z, giro).</summary>
        static readonly float[][] ATRAS =
        {
            new[] { -5.4f, 10.6f, 0.4f }, new[] { 5.3f, 11.2f, -0.5f }, new[] { -2.5f, 12.9f, 0.2f }, new[] { 2.6f, 13.6f, -0.2f },
            new[] { -8.6f, 13.2f, 0.8f }, new[] { 8.4f, 12.4f, -0.9f },
        };

        public string atual = "";
        readonly Dictionary<string, GameObject> _grupos = new Dictionary<string, GameObject>();
        readonly Dictionary<string, GameObject> _estaticos = new Dictionary<string, GameObject>();
        readonly Dictionary<string, List<Anim>> _anim = new Dictionary<string, List<Anim>>();
        readonly Dictionary<string, List<Efeitos.Emissor>> _emissores = new Dictionary<string, List<Efeitos.Emissor>>();
        float _t;
        bool _leve;

        /// <summary>Uma decoração que se mexe (os tipos das edições Godot e os dinossauros novos).</summary>
        class Anim
        {
            public string tipo;
            public Transform no;
            public Transform[] partes;
            public Vector3 basePos, eixo;
            public Quaternion baseRot;
            public float fase, vel, raio, escala = 1f, giro, limite, ang;
            public Vector3 amp, raio3, centro;
            public Vector2 raio2;
            public float balanco, olhar, vira, girar;
            public bool temBalanco, temVira;
        }

        // ------------------------------------------------------------ chão
        static readonly float[][] MORROS =
        {
            new[] { -9f, -20f, 6f, 2.2f }, new[] { 10f, -26f, 7f, 2.6f }, new[] { -14f, -42f, 9f, 3.4f }, new[] { 15f, -48f, 10f, 3.8f },
            new[] { 0f, -64f, 16f, 5.0f }, new[] { -20f, -10f, 6f, 1.8f }, new[] { 21f, -8f, 7f, 2.2f }, new[] { -26f, -30f, 9f, 4.0f },
            new[] { 27f, -34f, 10f, 4.5f }, new[] { -6f, -52f, 6f, 2.0f }, new[] { 6f, -58f, 7f, 2.4f },
        };

        /// <summary>A altura do chão (a mesma fórmula do tools/modelador/pista.py), em coordenadas da Unity.</summary>
        public static float AlturaTerreno(float x, float z)
        {
            float h = 0f;
            foreach (var m in MORROS)
            {
                float mx = -m[0];
                h += m[3] * Mathf.Exp(-((x - mx) * (x - mx) + (z - m[1]) * (z - m[1])) / (2f * m[2] * m[2]));
            }
            float p = Mathf.Clamp01((Mathf.Abs(x) - 3.2f) / 4f);
            p = p * p * (3f - 2f * p);
            return h * p + 0.06f * Mathf.Sin(-x * 0.7f) * Mathf.Sin(z * 0.55f) * p - 0.02f - 0.22f * (1f - p);
        }

        // ------------------------------------------------------------ montagem
        public void Construir(bool leve)
        {
            _leve = leve;
            foreach (var nome in ORDEM)
            {
                var g = new GameObject(nome);
                g.transform.SetParent(transform, false);
                g.SetActive(false);
                _grupos[nome] = g;
                var e = new GameObject("parado");
                e.transform.SetParent(g.transform, false);
                _estaticos[nome] = e;
                _anim[nome] = new List<Anim>();
                _emissores[nome] = new List<Efeitos.Emissor>();
            }
            Parque();
            Espaco();
            Mar();
            Dinos();
            Atras("parque", new[] { ("carrossel", 0.9f), ("tenda", 1.0f), ("arvore_verde", 1.4f), ("pirulito", 1.3f), ("arvore_laranja", 1.4f), ("cupcake", 1.5f) });
            Atras("espaco", new[] { ("foguete", 1.1f), ("pedra_lua", 1.7f), ("cristal", 1.5f), ("cristal", 1.1f), ("pedra_lua", 1.3f), ("foguete", 0.9f) });
            Atras("mar", new[] { ("coral_roxo", 1.5f), ("concha", 1.3f), ("alga", 1.6f), ("coral", 1.5f), ("alga", 1.3f), ("bau", 1.2f) });
            Atras("dinos", new[] { ("palmeira", 1.1f), ("samambaia", 1.5f), ("ovo_dino", 1.3f), ("pedra", 1.4f), ("palmeira", 0.95f), ("flor_gigante", 1.4f) });
            // o que fica parado vira poucas malhas (uma por material)
            foreach (var nome in ORDEM) StaticBatchingUtility.Combine(_estaticos[nome]);
        }

        public void Mostrar(string nome)
        {
            atual = nome;
            foreach (var k in _grupos)
            {
                k.Value.SetActive(k.Key == nome);
                foreach (var e in _emissores[k.Key]) e.emitindo = k.Key == nome;
            }
        }

        public static string Proximo(string nome) => ORDEM[(Array.IndexOf(ORDEM, nome) + 1) % ORDEM.Length];

        int Q(int n) => _leve ? Mathf.Max(1, Mathf.RoundToInt(n * 0.45f)) : n;

        /// <summary>Um objeto do cenário (Malhas/cenario/&lt;nome&gt;) no grupo do tema.
        /// parado = vai para o lote estático. pos/giro nas coordenadas da Unity (giro: graus em Y).</summary>
        Transform Obj(string tema, string nome, Vector3 pos, float giro = 0f, float esc = 1f, bool parado = true,
            Transform pai = null, Dictionary<string, Transform> nos = null, Color? tinta = null)
        {
            var pc = Malhas.Ler("cenario/" + nome);
            Transform raiz = pai ?? (parado ? _estaticos[tema].transform : _grupos[tema].transform);
            Func<Malhas.Parte, Material> mat = null;
            if (tinta.HasValue)
            {
                var m = Materiais.Novo("Facetado", nome + "_tinta");
                m.SetColor(Materiais.ID_TINTA, tinta.Value);
                mat = p => p.material == "luz" ? Materiais.SemLuz : m;
            }
            var go = Malhas.Montar(pc, raiz, nos, mat);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, giro, 0);
            go.transform.localScale = Vector3.one * esc;
            return go.transform;
        }

        float Chao(float x, float z) => AlturaTerreno(x, z);

        Anim Animar(string tema, string tipo, Transform no)
        {
            var a = new Anim { tipo = tipo, no = no, basePos = no.localPosition, baseRot = no.localRotation, escala = no.localScale.x };
            _anim[tema].Add(a);
            return a;
        }

        /// <summary>Espalha objetos pelo terreno, longe da pista (mesma regra das edições Godot).</summary>
        void Espalhar(string tema, string[] tipos, int qtd, int semente, Dictionary<string, float> escala = null, List<Vector2> evitar = null)
        {
            var rnd = new System.Random(semente);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var usados = evitar != null ? new List<Vector2>(evitar) : new List<Vector2>();
            int colocados = 0, tentativas = 0;
            while (colocados < qtd && tentativas < 2000)
            {
                tentativas++;
                float s = rnd.NextDouble() < 0.5 ? -1f : 1f;
                float x = s * R(3.6f, 15f);
                float z = R(-34f, 7f);
                if (z > -2f && Mathf.Abs(x) < 5f) continue;
                var p = new Vector2(x, z);
                bool ok = true;
                foreach (var u in usados) if (Vector2.Distance(u, p) < 2.6f) { ok = false; break; }
                if (!ok) continue;
                usados.Add(p);
                string tipo = tipos[rnd.Next(tipos.Length)];
                float esc = R(0.9f, 1.35f) * (escala != null && escala.TryGetValue(tipo, out float e) ? e : 1f);
                Obj(tema, tipo, new Vector3(x, Chao(x, z) - 0.05f, z), R(0f, 360f), esc);
                colocados++;
            }
        }

        void Destaques(string tema, (string nome, float esc)[] itens)
        {
            for (int i = 0; i < Mathf.Min(itens.Length, DESTAQUES.Length); i++)
            {
                var d = DESTAQUES[i];
                float x = -d[0], z = d[1];
                float y = (z > 0f && Mathf.Abs(x) < 3.3f) ? 0f : Chao(x, z);
                Obj(tema, itens[i].nome, new Vector3(x, y - 0.02f, z), -d[2] * Mathf.Rad2Deg, itens[i].esc);
            }
        }

        void Atras(string tema, (string nome, float esc)[] itens)
        {
            for (int i = 0; i < Mathf.Min(itens.Length, ATRAS.Length); i++)
            {
                var a = ATRAS[i];
                float x = -a[0], z = a[1];
                Obj(tema, itens[i].nome, new Vector3(x, Chao(x, z) - 0.05f, z), -(Mathf.PI + a[2]) * Mathf.Rad2Deg, itens[i].esc);
            }
        }

        void Torres(string tema, string nome, float esc = 1f)
        {
            foreach (float s in new[] { -1f, 1f })
                Obj(tema, nome, new Vector3(-s * 2.75f, -0.02f, Medidas.Z_FUNDO + 0.4f), s * 25f, esc);
        }

        void Nuvens(string tema, int qtd, int semente)
        {
            var rnd = new System.Random(semente);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            for (int i = 0; i < Q(qtd); i++)
            {
                float lado = i % 2 == 0 ? -1f : 1f;
                var n = Obj(tema, "nuvem", new Vector3(lado * R(4f, 22f), R(6f, 15f), R(-60f, -8f)), R(0f, 360f), R(1.2f, 2.6f), false);
                var a = Animar(tema, "nuvem", n);
                a.vel = R(0.15f, 0.4f);
            }
        }

        Efeitos.Emissor Particulas(string tema, Vector3 pos, Efeitos.Config c, float porSegundo)
        {
            var e = Efeitos.I.NovoEmissor(c, porSegundo);
            e.pos = pos;
            _emissores[tema].Add(e);
            return e;
        }

        // ------------------------------------------------------------ PARQUE
        void Parque()
        {
            const string T = "parque";
            Obj(T, "arco_iris", new Vector3(0, -0.05f, Medidas.Z_FUNDO - 0.5f), 0f, 0.95f);
            Torres(T, "torre");
            Espalhar(T, new[] { "arvore_verde", "arvore_laranja", "arvore_escura", "pirulito", "cogumelo", "bengala", "presente", "donut",
                "cupcake", "arvore_verde", "arvore_escura", "arvore_laranja" }, Q(40), 2026,
                new Dictionary<string, float> { ["arvore_verde"] = 1.25f, ["arvore_laranja"] = 1.25f, ["arvore_escura"] = 1.25f });
            Destaques(T, new[] { ("pirulito", 0.75f), ("pirulito", 0.85f), ("presente", 0.8f), ("cupcake", 0.75f), ("bengala", 0.9f),
                ("bengala", 0.9f), ("cogumelo", 0.9f), ("donut", 0.85f) });
            // roda-gigante (gira) com as cabines sempre de pé
            var rg = new GameObject("roda_gigante").transform;
            rg.SetParent(_grupos[T].transform, false);
            rg.localPosition = new Vector3(6.8f, Chao(6.8f, -27f) - 0.05f, -27f);
            rg.localRotation = Quaternion.Euler(0, -0.35f * Mathf.Rad2Deg, 0);
            Obj(T, "roda_base", Vector3.zero, 0f, 1f, false, rg);
            var eixo = new GameObject("eixo").transform;
            eixo.SetParent(rg, false);
            eixo.localPosition = new Vector3(0, 4.6f, 0);
            var roda = Obj(T, "roda", Vector3.zero, 0f, 1f, false, eixo);
            var cabines = new Transform[8];
            for (int i = 0; i < 8; i++) cabines[i] = Obj(T, "cabine", Vector3.zero, 0f, 1f, false, eixo, null, CORES_BICHOS[i % CORES_BICHOS.Length]);
            var ar = Animar(T, "roda", roda);
            ar.partes = cabines;
            ar.raio = 3.8f;
            ar.vel = 0.16f;
            // carrossel (o topo gira devagar) e tendas
            var nosC = new Dictionary<string, Transform>();
            var car = Obj(T, "carrossel", new Vector3(-6.4f, Chao(-6.4f, -21f) - 0.05f, -21f), 0f, 1.2f, false, null, nosC);
            var ac = Animar(T, "carrossel", nosC.TryGetValue("topo", out var topo) ? topo : car);
            ac.vel = 0.5f;
            Obj(T, "tenda", new Vector3(9.5f, Chao(9.5f, -13f) - 0.05f, -13f), -0.6f * Mathf.Rad2Deg, 1.1f);
            Obj(T, "tenda", new Vector3(-10.5f, Chao(-10.5f, -33f) - 0.05f, -33f), 0.4f * Mathf.Rad2Deg, 1.3f);
            var nosC2 = new Dictionary<string, Transform>();
            var car2 = Obj(T, "carrossel", new Vector3(12f, Chao(12f, -38f) - 0.05f, -38f), 0f, 1f, false, null, nosC2);
            var ac2 = Animar(T, "carrossel", nosC2.TryGetValue("topo", out var topo2) ? topo2 : car2);
            ac2.vel = 0.4f;
            Nuvens(T, 14, 77);
            Baloes(T);
        }

        void Baloes(string tema)
        {
            var rnd = new System.Random(91);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var cores = new[] { H("#FF4B5C"), H("#FFD43B"), H("#4FC3F7"), H("#5ED16A"), H("#2E7BFF"), H("#FF9F2E") };
            for (int i = 0; i < (_leve ? 8 : 16); i++)
            {
                float lado = i % 2 == 0 ? -1f : 1f;
                var pos = new Vector3(lado * R(1.8f, 5.5f), R(2.2f, 4.8f), R(-19f, -4f));
                if (i < 4) pos = new Vector3(lado * R(2f, 2.8f), R(2.6f, 3.4f), Medidas.Z_FUNDO + R(0f, 1.5f));
                var b = Obj(tema, "balao", pos, 0f, R(0.8f, 1.15f), false, null, null, cores[i % cores.Length]);
                var a = Animar(tema, "boiar", b);
                a.fase = R(0f, Mathf.PI * 2f);
                a.vel = R(0.6f, 1.1f);
                a.amp = new Vector3(0.12f, 0.18f, 0f);
                a.balanco = 0.12f;
                a.temBalanco = true;
            }
        }

        // ------------------------------------------------------------ ESPAÇO
        void Espaco()
        {
            const string T = "espaco";
            var rnd = new System.Random(404);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            // planeta com anel atrás dos pinos: gira em volta do eixo do ANEL
            var pa = Obj(T, "planeta_anel", new Vector3(-0.3f, 3.7f, Medidas.Z_FUNDO - 2.6f), 0f, 1.15f, false);
            pa.localRotation = Quaternion.Euler(0.18f * Mathf.Rad2Deg, 0, -0.32f * Mathf.Rad2Deg);
            var ap = Animar(T, "girar_proprio", pa);
            ap.vel = 0.12f;
            ap.eixo = new Vector3(0.336f, 0.894f, 0.296f).normalized;
            var foguetes = new List<Vector2>();
            foreach (float s in new[] { -1f, 1f })
            {
                float fx = -s * 3.35f, fz = -11f;
                Obj(T, "foguete", new Vector3(fx, Chao(fx, fz) - 0.03f, fz), s * 35f, 0.95f);
                foguetes.Add(new Vector2(fx, fz));
            }
            // disco voador passeando por cima do fim da pista
            var ovni = Obj(T, "ovni", new Vector3(0, 5f, -12.1f), 0f, 1f, false);
            var ao = Animar(T, "passeio", ovni);
            ao.centro = new Vector3(0, 5f, -12.1f);
            ao.raio3 = new Vector3(3.1f, 0.25f, 0.65f);
            ao.vel = 0.3f;
            ao.girar = 1.6f;
            // planetinhas coloridos no céu
            for (int i = 0; i < Q(8); i++)
            {
                float lado = i % 2 == 0 ? -1f : 1f;
                var pos = new Vector3(lado * R(6f, 22f), R(5f, 15f), R(-58f, -14f));
                var pl = Obj(T, "planeta", pos, R(0f, 360f), R(1.6f, 4.2f), false, null, null, CORES_BICHOS[i % CORES_BICHOS.Length]);
                var a = Animar(T, "girar", pl);
                a.vel = R(0.05f, 0.2f);
            }
            // estrelas fofas flutuando ao lado da pista
            for (int i = 0; i < Q(6); i++)
            {
                float lado = i % 2 == 0 ? -1f : 1f;
                var b = new Vector3(lado * R(2.1f, 3.4f), R(2.2f, 3.8f), R(-17f, -3f));
                var e = Obj(T, "estrela_3d", b, 0f, R(0.6f, 0.9f), false);
                var a = Animar(T, "boiar", e);
                a.fase = R(0f, Mathf.PI * 2f);
                a.vel = R(0.7f, 1.1f);
                a.amp = new Vector3(0f, 0.2f, 0f);
                a.olhar = lado * 0.35f;
                a.vira = 0.45f;
                a.temVira = true;
            }
            Espalhar(T, new[] { "cristal", "pedra_lua", "cristal", "pedra_lua", "pedra_lua", "foguete" }, Q(38), 77,
                new Dictionary<string, float> { ["cristal"] = 1.3f, ["pedra_lua"] = 1.6f, ["foguete"] = 0.8f }, foguetes);
            Destaques(T, new[] { ("cristal", 0.9f), ("cristal", 1.0f), ("pedra_lua", 1.0f), ("cristal", 0.8f), ("pedra_lua", 1.1f),
                ("cristal", 1.0f), ("pedra_lua", 1.0f), ("cristal", 1.1f) });
            // estrelas cadentes de vez em quando
            Particulas(T, new Vector3(0, 13f, -30f), new Efeitos.Config
            {
                tipo = Efeitos.Tipo.Cadente, vida = 1.6f, tam = 0.6f, caixa = new Vector3(14f, 3f, 8f), direcao = new Vector3(-1f, -0.35f, 0f),
                abertura = 5f, vMin = 14f, vMax = 18f, cor = new Color(1f, 0.95f, 0.8f), curva = new[] { new Vector2(0, 0.3f), new Vector2(0.2f, 1f), new Vector2(1, 0) },
            }, 1.9f);
        }

        // ------------------------------------------------------------ MAR
        void Mar()
        {
            const string T = "mar";
            var rnd = new System.Random(505);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            float nx = -0.4f, nz = Medidas.Z_FUNDO - 6.5f;
            var navio = Obj(T, "navio", new Vector3(nx, Chao(nx, nz) - 0.06f, nz), -38f, 1.3f);
            navio.localRotation = Quaternion.Euler(0, -38f, -0.05f * Mathf.Rad2Deg);
            var ocupado = new List<Vector2> { new Vector2(nx, nz), new Vector2(nx - 3.2f, nz + 4.1f), new Vector2(nx + 2.4f, nz - 3.1f) };
            Obj(T, "coral", new Vector3(2.75f, -0.02f, Medidas.Z_FUNDO + 0.4f), -25f, 2.1f);
            Obj(T, "coral_roxo", new Vector3(-2.75f, -0.02f, Medidas.Z_FUNDO + 0.4f), 25f, 2.3f);
            Espalhar(T, new[] { "coral", "coral_roxo", "alga", "alga", "estrela_mar", "concha", "pedra", "coral" }, Q(44), 31,
                new Dictionary<string, float> { ["coral"] = 1.5f, ["coral_roxo"] = 1.5f, ["alga"] = 1.3f, ["estrela_mar"] = 1.6f, ["concha"] = 0.7f, ["pedra"] = 1.2f },
                ocupado);
            Destaques(T, new[] { ("coral", 0.8f), ("alga", 0.8f), ("estrela_mar", 1.0f), ("bau", 0.9f), ("coral_roxo", 0.9f), ("alga", 0.9f),
                ("concha", 0.5f), ("estrela_mar", 1.0f) });
            // algas balançando na correnteza
            for (int i = 0; i < Q(10); i++)
            {
                float lado = i % 2 == 0 ? -1f : 1f;
                float x = lado * R(3.6f, 9f), z = R(-26f, -2f);
                var al = Obj(T, "alga", new Vector3(x, Chao(x, z) - 0.05f, z), R(0f, 360f), R(1.0f, 1.6f), false);
                var a = Animar(T, "alga", al);
                a.fase = R(0f, Mathf.PI * 2f);
            }
            // cardumes de peixinhos
            for (int c = 0; c < (_leve ? 3 : 5); c++)
            {
                var centro = new Vector3((c % 2 == 0 ? -1f : 1f) * R(3f, 7f), R(2.6f, 5.5f), R(-20f, -4f));
                var cor = CORES_BICHOS[c % CORES_BICHOS.Length];
                float vel = R(0.25f, 0.45f) * (c % 2 == 0 ? 1f : -1f);
                for (int k = 0; k < (_leve ? 3 : 5); k++)
                {
                    var nos = new Dictionary<string, Transform>();
                    var p = Obj(T, "peixe", centro, 0f, R(0.7f, 1.0f), false, null, nos, cor);
                    var a = Animar(T, "peixe", p);
                    a.centro = centro + new Vector3(R(-0.4f, 0.4f), R(-0.3f, 0.3f), 0);
                    a.raio2 = new Vector2(R(2.2f, 3f), R(1.4f, 2f));
                    a.vel = vel;
                    a.fase = k * 0.22f;
                    a.ang = R(0f, Mathf.PI * 2f);
                    if (nos.TryGetValue("rabo", out var rabo)) a.partes = new[] { rabo };
                }
            }
            // águas-vivas subindo e descendo
            for (int i = 0; i < Q(7); i++)
            {
                float lado = i % 2 == 0 ? -1f : 1f;
                var b = new Vector3(lado * R(2.2f, 5f), R(2.4f, 5.2f), R(-19f, -2f));
                var cor = new[] { H("#FFB36B"), H("#9FF0C8"), H("#8FE9FF") }[i % 3];
                var av = Obj(T, "agua_viva", b, 0f, R(0.7f, 1f), false, null, null, cor);
                var a = Animar(T, "agua_viva", av);
                a.fase = R(0f, Mathf.PI * 2f);
                a.vel = R(0.8f, 1.2f);
            }
            // tartarugas atravessando o mar lá no alto
            for (int i = 0; i < Q(2); i++)
            {
                var nos = new Dictionary<string, Transform>();
                var tt = Obj(T, "tartaruga", new Vector3(0, 7.5f + i * 2f, -24f - i * 10f), i == 0 ? -90f : 90f, 1.8f + i * 0.4f, false, null, nos);
                var a = Animar(T, "atravessar", tt);
                a.vel = 1.1f * (i == 0 ? -1f : 1f);
                a.limite = 28f;
                var nad = new List<Transform>();
                foreach (var k in new[] { "nadadeira_ed", "nadadeira_ef", "nadadeira_dd", "nadadeira_df" })
                    if (nos.TryGetValue(k, out var n)) nad.Add(n);
                a.partes = nad.ToArray();
            }
            // bolhas subindo
            for (int i = 0; i < (_leve ? 3 : 8); i++)
            {
                float lado = i % 2 == 0 ? -1f : 1f;
                var pos = new Vector3(lado * R(1.8f, 6f), 0.1f, R(-18f, 1f));
                Particulas(T, pos, new Efeitos.Config
                {
                    tipo = Efeitos.Tipo.Bolha, vida = 4f, tam = 0.16f, raioEsfera = 0.25f, direcao = Vector3.up, abertura = 12f,
                    vMin = 0.5f, vMax = 0.9f, gravidade = new Vector3(0, 0.25f, 0), escalaMin = 0.4f, escalaMax = 1.4f,
                    cor = new Color(0.85f, 1f, 1f, 0.85f),
                }, 2.5f);
            }
        }

        // ------------------------------------------------------------ DINOS
        void Dinos()
        {
            const string T = "dinos";
            var rnd = new System.Random(606);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            Obj(T, "vulcao", new Vector3(-5.5f, -0.4f, -48f), -0.3f * Mathf.Rad2Deg, 1.9f);
            Obj(T, "vulcao", new Vector3(16f, -0.4f, -40f), 0.5f * Mathf.Rad2Deg, 1.1f);
            Particulas(T, new Vector3(-5.5f, 9.3f, -48f), new Efeitos.Config
            {
                tipo = Efeitos.Tipo.Fumaca, vida = 5f, tam = 2.4f, direcao = new Vector3(-0.25f, 1f, 0f), abertura = 18f, vMin = 0.8f, vMax = 1.3f,
                gravidade = new Vector3(-0.15f, 0.15f, 0f), escalaMin = 0.8f, escalaMax = 1.6f,
                curva = new[] { new Vector2(0, 0.4f), new Vector2(1, 1.6f) },
                cor = Color.white, rampa = new[] { new Color(0.95f, 0.9f, 0.95f, 0.85f), new Color(0.85f, 0.85f, 0.95f, 0f) },
            }, _leve ? 1.6f : 2.8f);
            Torres(T, "palmeira", 0.85f);
            // dinossauros: o pescoçudo espiando atrás dos pinos, os outros pelo cenário
            Dino(T, "dino", new Vector3(-3.7f, -0.02f, Medidas.Z_FUNDO - 0.9f), 0.55f * Mathf.Rad2Deg, 1.0f, 0f);
            Dino(T, "dino", new Vector3(8.5f, Chao(8.5f, -25f) - 0.05f, -25f), -0.7f * Mathf.Rad2Deg, 1.5f, 1.7f);
            Dino(T, "trex", new Vector3(-7.2f, Chao(-7.2f, -17f) - 0.05f, -17f), -35f, 1.1f, 0.6f);
            Dino(T, "triceratops", new Vector3(6.2f, Chao(6.2f, -12f) - 0.05f, -12f), 40f, 1.0f, 2.4f);
            Dino(T, "estegossauro", new Vector3(-6.0f, Chao(-6.0f, -31f) - 0.05f, -31f), 20f, 1.2f, 3.1f);
            Dino(T, "ovo_chocando", new Vector3(4.4f, Chao(4.4f, -6.0f) - 0.03f, -6.0f), -20f, 1.1f, 0.9f);
            // (nada nasce em cima dos dinossauros)
            var ocupado = new List<Vector2> { new Vector2(8.5f, -25f), new Vector2(-7.2f, -17f), new Vector2(6.2f, -12f),
                new Vector2(-6.0f, -31f), new Vector2(4.4f, -6.0f), new Vector2(-5.5f, -48f), new Vector2(16f, -40f) };
            // pterossauros voando em círculos
            for (int i = 0; i < Q(3); i++)
            {
                var nos = new Dictionary<string, Transform>();
                var p = Obj(T, "ptero", Vector3.zero, 0f, R(0.9f, 1.3f), false, null, nos);
                var a = Animar(T, "voo", p);
                a.centro = new Vector3(R(-3f, 3f), R(6f, 8.5f), R(-22f, -9f));
                a.raio = R(4f, 7f);
                a.vel = R(0.25f, 0.4f) * (i % 2 == 0 ? 1f : -1f);
                a.fase = R(0f, Mathf.PI * 2f);
                var asas = new List<Transform>();
                if (nos.TryGetValue("asa_e", out var ae)) asas.Add(ae);
                if (nos.TryGetValue("asa_d", out var ad)) asas.Add(ad);
                a.partes = asas.ToArray();
            }
            Espalhar(T, new[] { "palmeira", "samambaia", "pedra", "flor_gigante", "samambaia", "palmeira", "ovo_dino", "pedra" }, Q(44), 2027,
                new Dictionary<string, float> { ["palmeira"] = 1.0f, ["samambaia"] = 1.4f, ["pedra"] = 1.3f, ["flor_gigante"] = 1.3f, ["ovo_dino"] = 1.2f },
                ocupado);
            Destaques(T, new[] { ("samambaia", 1.0f), ("flor_gigante", 1.0f), ("ovo_dino", 1.0f), ("flor_gigante", 0.9f), ("samambaia", 1.2f),
                ("pedra", 1.0f), ("ovo_dino", 1.0f), ("samambaia", 1.1f) });
            Nuvens(T, 10, 88);
        }

        /// <summary>Um dinossauro com as partes que se mexem (pescoço, cabeça, mandíbula, rabo, bracinhos).</summary>
        void Dino(string tema, string nome, Vector3 pos, float giro, float esc, float fase)
        {
            var nos = new Dictionary<string, Transform>();
            var d = Obj(tema, nome, pos, giro, esc, false, null, nos);
            var a = Animar(tema, "dino", d);
            a.fase = fase;
            a.giro = giro;
            var partes = new List<Transform>();
            foreach (var k in new[] { "pescoco", "cabeca", "mandibula", "rabo", "braco_e", "braco_d" })
                partes.Add(nos.TryGetValue(k, out var t) ? t : null);
            a.partes = partes.ToArray();
            a.raio2 = new Vector2(nome == "dino" ? 1f : 0.6f, nome == "trex" ? 1f : 0f);
        }

        // ------------------------------------------------------------ animação
        void Update()
        {
            if (atual == "" || !_anim.ContainsKey(atual)) return;
            float dt = Time.deltaTime;
            _t = Mathf.Repeat(_t + dt, 3600f);
            foreach (var a in _anim[atual]) Passo(a, dt);
        }

        void Passo(Anim a, float dt)
        {
            var no = a.no;
            switch (a.tipo)
            {
                case "girar":
                    no.Rotate(0, -a.vel * dt * Mathf.Rad2Deg, 0, Space.Self);
                    break;
                case "carrossel":
                    no.Rotate(0, -a.vel * dt * Mathf.Rad2Deg, 0, Space.Self);
                    break;
                case "nuvem":
                    {
                        var p = no.localPosition;
                        p.x -= a.vel * dt;
                        if (p.x < -30f) p.x = 30f;
                        no.localPosition = p;
                        break;
                    }
                case "boiar":
                    {
                        no.localPosition = a.basePos + new Vector3(Mathf.Sin(_t * 0.5f * a.vel + a.fase) * a.amp.x,
                            Mathf.Sin(_t * 0.9f * a.vel + a.fase * 2f) * a.amp.y, 0);
                        float y = a.temVira ? -(a.olhar + Mathf.Sin(_t * 0.6f * a.vel + a.fase) * a.vira) * Mathf.Rad2Deg : 0f;
                        float z = a.temBalanco ? -Mathf.Sin(_t * 0.7f * a.vel + a.fase) * a.balanco * Mathf.Rad2Deg : 0f;
                        no.localRotation = Quaternion.Euler(0, y, z);
                        break;
                    }
                case "girar_proprio":
                    no.Rotate(a.eixo, -a.vel * dt * Mathf.Rad2Deg, Space.Self);
                    break;
                case "passeio":
                    {
                        float ang = _t * a.vel;
                        no.localPosition = a.centro + new Vector3(-Mathf.Sin(ang) * a.raio3.x, Mathf.Sin(2f * ang) * a.raio3.y, Mathf.Cos(ang) * a.raio3.z);
                        a.ang = Mathf.Repeat(a.ang + a.girar * dt, Mathf.PI * 2f);
                        no.localRotation = Quaternion.Euler(0, 0, Mathf.Cos(ang) * 0.2f * Mathf.Rad2Deg) * Quaternion.Euler(0, -a.ang * Mathf.Rad2Deg, 0);
                        break;
                    }
                case "roda":
                    {
                        // a roda gira em Z; as cabines ficam penduradas, sempre de pé
                        a.ang += a.vel * dt;
                        no.localRotation = Quaternion.Euler(0, 0, -a.ang * Mathf.Rad2Deg);
                        for (int i = 0; i < a.partes.Length; i++)
                        {
                            float ang = a.ang + Mathf.PI * 2f * i / a.partes.Length;
                            a.partes[i].localPosition = new Vector3(-Mathf.Cos(ang), Mathf.Sin(ang), 0) * a.raio;
                            a.partes[i].localRotation = Quaternion.Euler(0, 0, -Mathf.Sin(_t * 1.3f + i) * 0.06f * Mathf.Rad2Deg);
                        }
                        break;
                    }
                case "alga":
                    no.localRotation = a.baseRot * Quaternion.Euler(Mathf.Sin(_t * 0.8f + a.fase) * 4f, 0, -Mathf.Sin(_t * 1.1f + a.fase * 1.7f) * 5f);
                    break;
                case "peixe":
                    {
                        float ang = _t * a.vel - a.fase * Mathf.Sign(a.vel);
                        no.localPosition = a.centro + new Vector3(-Mathf.Cos(ang) * a.raio2.x, Mathf.Sin(_t * 1.7f + a.ang) * 0.12f, Mathf.Sin(ang) * a.raio2.y);
                        var dir = new Vector3(Mathf.Sin(ang) * a.raio2.x, 0, Mathf.Cos(ang) * a.raio2.y) * Mathf.Sign(a.vel);
                        float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                        no.localRotation = Quaternion.Euler(0, yaw + Mathf.Sin(_t * 9f + a.ang) * 7f, 0);
                        if (a.partes != null && a.partes.Length > 0)
                            a.partes[0].localRotation = Quaternion.Euler(0, Mathf.Sin(_t * 9f + a.ang + 1.2f) * 28f, 0);
                        break;
                    }
                case "agua_viva":
                    {
                        float pulso = Mathf.Sin(_t * 2.2f * a.vel + a.fase);
                        no.localPosition = a.basePos + new Vector3(0, Mathf.Sin(_t * 0.6f * a.vel + a.fase) * 0.5f, 0);
                        no.localScale = new Vector3(1f + pulso * 0.06f, 1f - pulso * 0.08f, 1f + pulso * 0.06f) * a.escala;
                        break;
                    }
                case "atravessar":
                    {
                        var p = no.localPosition;
                        p.x += a.vel * dt;
                        if (p.x > a.limite) p.x = -a.limite;
                        else if (p.x < -a.limite) p.x = a.limite;
                        p.y = a.basePos.y + Mathf.Sin(_t * 0.8f) * 0.15f;
                        no.localPosition = p;
                        if (a.partes != null)
                            for (int i = 0; i < a.partes.Length; i++)
                            {
                                float s = (i % 2 == 0 ? 1f : -1f);
                                a.partes[i].localRotation = Quaternion.Euler(0, s * Mathf.Sin(_t * 2.4f + i) * 25f, Mathf.Sin(_t * 2.4f + i * 0.5f) * 18f);
                            }
                        break;
                    }
                case "voo":
                    {
                        float ang = _t * a.vel + a.fase;
                        no.localPosition = a.centro + new Vector3(-Mathf.Cos(ang) * a.raio, Mathf.Sin(_t * 1.4f + a.fase) * 0.4f, Mathf.Sin(ang) * a.raio);
                        var dir = new Vector3(Mathf.Sin(ang), 0, Mathf.Cos(ang)) * Mathf.Sign(a.vel);
                        no.localRotation = Quaternion.Euler(0, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg,
                            (Mathf.Sign(a.vel) * 0.35f - Mathf.Sin(_t * 3f) * 0.05f) * Mathf.Rad2Deg);
                        if (a.partes != null)
                        {
                            float bate = Mathf.Sin(_t * 6f + a.fase) * 32f;
                            for (int i = 0; i < a.partes.Length; i++)
                                a.partes[i].localRotation = Quaternion.Euler(0, 0, i == 0 ? bate : -bate);
                        }
                        break;
                    }
                case "dino":
                    {
                        // respira e olha em volta: pescoço e cabeça balançam devagar, o rabo abana,
                        // a mandíbula do T-rex abre de vez em quando (um "rugido" de criança)
                        float f = a.fase;
                        no.localRotation = Quaternion.Euler(0, a.giro + Mathf.Sin(_t * 0.35f + f) * 7f, Mathf.Sin(_t * 0.9f + f) * 1.4f);
                        var p = a.partes;
                        if (p[0] != null) p[0].localRotation = Quaternion.Euler(Mathf.Sin(_t * 0.6f + f) * 6f * a.raio2.x, Mathf.Sin(_t * 0.4f + f) * 12f, 0);
                        if (p[1] != null) p[1].localRotation = Quaternion.Euler(Mathf.Sin(_t * 0.9f + f + 1f) * 8f, Mathf.Sin(_t * 0.5f + f + 2f) * 18f, Mathf.Sin(_t * 0.7f + f) * 5f);
                        if (p[2] != null)
                        {
                            float ciclo = Mathf.Repeat(_t * 0.25f + f * 0.1f, 1f);
                            float abre = ciclo < 0.12f ? Mathf.Sin(ciclo / 0.12f * Mathf.PI) : 0f;
                            p[2].localRotation = Quaternion.Euler(abre * 24f + Mathf.Sin(_t * 1.3f) * 2f, 0, 0);
                        }
                        if (p[3] != null) p[3].localRotation = Quaternion.Euler(Mathf.Sin(_t * 0.8f + f) * 4f, Mathf.Sin(_t * 1.6f + f) * 14f, 0);
                        if (p[4] != null) p[4].localRotation = Quaternion.Euler(Mathf.Sin(_t * 2.2f + f) * 20f, 0, 0);
                        if (p[5] != null) p[5].localRotation = Quaternion.Euler(Mathf.Sin(_t * 2.2f + f + 1.5f) * 20f, 0, 0);
                        break;
                    }
            }
        }
    }
}
