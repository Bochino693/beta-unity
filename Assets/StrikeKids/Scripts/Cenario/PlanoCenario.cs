using System;
using System.Collections.Generic;

namespace StrikeKids
{
    /// <summary>O que o plano precisa saber das malhas do cenário (nas coordenadas do Godot).</summary>
    public interface IModelosCenario
    {
        /// <summary>Existe Malhas/cenario/&lt;nome&gt;?</summary>
        bool Existe(string nome);
        /// <summary>A caixa da malha inteira.</summary>
        CaixaG CaixaDe(string nome);
        /// <summary>Os pontos da primeira parte (a superfície 0 do Godot), na ordem do arquivo.</summary>
        V3[] Pontos(string nome);
    }

    /// <summary>As partículas de um ponto do cenário (o CPUParticles do Godot).</summary>
    public class ParticulasCenario
    {
        public string textura;          // fumaca, bolha, faisca
        public bool somar;
        public int qtd;
        public float vida, tam;
        public V3 direcao = new V3(1f, 0f, 0f);
        public float abertura = 45f;
        public V3 gravidade = new V3(0f, -9.8f, 0f);
        public float vMin, vMax;
        public float escMin = 1f, escMax = 1f;
        public float[] curva;           // pares (t, escala)
        public Cor[] rampa;             // cor ao longo da vida
        public Cor cor = Cor.Branco;
        public V3 caixa;                // meia-caixa de onde nascem
        public float esfera;            // raio da esfera de onde nascem
        public bool umaVez;             // (as faíscas do baú: um punhado de cada vez)
        public float explosividade;
        public object vivo;             // o emissor na tela
    }

    /// <summary>Uma fileira de lâmpadas (Mundo.LampadasEm).</summary>
    public class LampadasCenario
    {
        public List<V3> pontos = new List<V3>();
        public int fase0;
    }

    /// <summary>O fio de um balão (um cilindro fino).</summary>
    public class FioCenario
    {
        public float raio, altura;
        public int gomos;
    }

    /// <summary>Uma peça que se mexe (os itens de animação do temas.gd).</summary>
    public class AnimCenario
    {
        public string tipo;
        public No no, roda, corpo;
        public List<No> cabines;
        public ParticulasCenario particulas, faiscas, fumaca;
        public float vel, raio, fase, giro, limite, sobe, nado, ang, altura, cada, asas;
        public float balanco, girar, olhar, vira;
        public bool temBalanco, temGirar, temVira;
        public V3 centro, basePos, baseRot, amp, raio3, eixo;
        public V2 raio2;
        // o que muda enquanto anda (as ações das vozes e as contas de cada quadro)
        public float musica, balancar, pirueta, rugir, tremer, k, mostra;
        public int dir;
        public bool temBaseY;
        public float baseY, cauda;
        public bool temEscala;
        public float escala;
        public object mat;              // o material dele (cristal que brilha, asas)
    }

    /// <summary>Uma voz de ambiente (sons/amb_*): sai de `no` com a `acao` junto.</summary>
    public class VozCenario
    {
        public No no;
        public string som;
        public float cadaMin, cadaMax, vol, tom;
        public string acao;
        public AnimCenario item;
        public float prox;
    }

    /// <summary>
    /// O PLANO DOS 4 CENÁRIOS — a cópia do temas.gd da edição Godot 7.6 (a que
    /// roda na TV Box), nas coordenadas do Godot: as mesmas peças, nos mesmos
    /// lugares (as mesmas sementes e o mesmo sorteador, o PCG32 do Godot), os
    /// mesmos patamares, a mesma conferência para nada entrar no morro nem
    /// atravessar outra peça, os mesmos níveis de detalhe, as mesmas
    /// animações, vozes e partículas. Não usa a Unity: o Temas.cs põe o plano
    /// na tela (com o X espelhado) e o anima; aqui ele pode ser conferido
    /// contra o do Godot (tools/godot/conferir_cenario.*).
    /// </summary>
    public class PlanoCenario
    {
        public static readonly string[] ORDEM = { "dinos", "parque", "espaco", "mar" };

        public static readonly Cor[] CORES_BICHOS = { Cor.Hex("#FF9F2E"), Cor.Hex("#FFD43B"), Cor.Hex("#4FD8FF"), Cor.Hex("#FF4B5C"), Cor.Hex("#8DFF6A"), Cor.Hex("#2E7BFF") };

        // ------------------------------------------------------------ medidas (Medidas.gd)
        public const float Z_PINO1 = -10.5f;
        public const float Z_FUNDO = -13.7f;

        static readonly V3 BAU_PERTO = new V3(1.75f, 0f, -4.4f);
        static readonly double[][] DESTAQUES =
        {
            new[] { -2.55, -1.2, 0.3 }, new[] { 2.7, -2.7, -0.4 }, new[] { -3.9, 3.4, 0.4 }, new[] { 3.9, 2.6, 0.0 },
            new[] { 2.3, -6.0, 2.0 }, new[] { -2.3, -6.8, 1.0 }, new[] { -2.4, -4.2, 0.0 }, new[] { 2.45, -8.7, 0.5 },
        };
        static readonly double[][] ATRAS =
        {
            new[] { -5.4, 10.6, 0.4 }, new[] { 5.3, 11.2, -0.5 }, new[] { -2.5, 12.9, 0.2 }, new[] { 2.6, 13.6, -0.2 },
            new[] { -8.6, 13.2, 0.8 }, new[] { 8.4, 12.4, -0.9 },
        };

        const double TAU = Math.PI * 2.0;
        const double PI = Math.PI;

        // ------------------------------------------------------------ o terreno (mundo.gd)
        static readonly double[][] MORROS =
        {
            new[] { -9.0, -20, 6, 2.2 }, new[] { 10.0, -26, 7, 2.6 }, new[] { -14.0, -42, 9, 3.4 }, new[] { 15.0, -48, 10, 3.8 },
            new[] { 0.0, -64, 16, 5.0 }, new[] { -20.0, -10, 6, 1.8 }, new[] { 21.0, -8, 7, 2.2 }, new[] { -26.0, -30, 9, 4.0 },
            new[] { 27.0, -34, 10, 4.5 }, new[] { -6.0, -52, 6, 2.0 }, new[] { 6.0, -58, 7, 2.4 },
        };

        /// <summary>PATAMARES (7.5/7.6): x, z, raio plano, raio até onde ele se mistura com o morro.</summary>
        public static readonly double[][] PATAMARES =
        {
            new[] { 5.4, -17.8, 2.5, 4.3 }, new[] { -7.0, -27.6, 3.4, 5.0 },
            new[] { -9.5, -13.0, 2.1, 3.6 }, new[] { 10.5, -33.0, 2.4, 4.0 }, new[] { -17.0, -42.0, 1.8, 3.2 }, new[] { -6.6, -6.8, 1.7, 2.9 },
        };
        static double[] _alturaPatamar;

        /// <summary>A altura do chão (a fórmula do tools/modelos/modelos.py, com os patamares), no Godot.</summary>
        public static double AlturaTerreno(double x, double z)
        {
            double h = AlturaMorros(x, z);
            if (_alturaPatamar == null)
            {
                var a = new double[PATAMARES.Length];
                for (int i = 0; i < PATAMARES.Length; i++) a[i] = AlturaMorros(PATAMARES[i][0], PATAMARES[i][1]);
                _alturaPatamar = a;
            }
            for (int i = 0; i < PATAMARES.Length; i++)
            {
                var pt = PATAMARES[i];
                double d = new V2(x - pt[0], z - pt[1]).Length();
                if (d < pt[3])
                {
                    double u = Clamp((d - pt[2]) / (pt[3] - pt[2]), 0.0, 1.0);
                    h = Lerp(_alturaPatamar[i], h, u * u * (3.0 - 2.0 * u));
                }
            }
            return h;
        }

        static double AlturaMorros(double x, double z)
        {
            double h = 0.0;
            foreach (var m in MORROS)
                h += m[3] * Math.Exp(-((x - m[0]) * (x - m[0]) + (z - m[1]) * (z - m[1])) / (2.0 * m[2] * m[2]));
            double p = Clamp((Math.Abs(x) - 3.2) / 4.0, 0.0, 1.0);
            p = p * p * (3.0 - 2.0 * p);
            return h * p + 0.06 * Math.Sin(x * 0.7) * Math.Sin(z * 0.55) * p - 0.02 - 0.22 * (1.0 - p);
        }

        static double Clamp(double v, double a, double b) => v < a ? a : (v > b ? b : v);
        static double Lerp(double a, double b, double t) => a + (b - a) * t;

        // ------------------------------------------------------------ o sorteador do Godot
        /// <summary>O RandomNumberGenerator do Godot 3 (PCG32, randf com a precisão dele).</summary>
        public class Sorteio
        {
            ulong _estado, _inc;

            public Sorteio(ulong semente)
            {
                _inc = (1442695040888963407UL << 1) | 1UL;
                _estado = 0UL;
                Proximo();
                _estado += semente;
                Proximo();
            }

            uint Proximo()
            {
                ulong velho = _estado;
                _estado = velho * 6364136223846793005UL + _inc;
                uint x = (uint)(((velho >> 18) ^ velho) >> 27);
                int rot = (int)(velho >> 59);
                return (x >> rot) | (x << ((-rot) & 31));
            }

            public long Randi() => Proximo();

            public float Randf()
            {
                uint p = Proximo();
                if (p == 0) return 0f;
                int zeros = 0;
                for (uint b = 0x80000000u; (p & b) == 0; b >>= 1) zeros++;
                return (float)((float)(Proximo() | 0x80000001u) * Math.Pow(2.0, -32 - zeros));
            }

            public float Faixa(double de, double ate) => Randf() * (float)(ate - de) + (float)de;
        }

        // ------------------------------------------------------------ o plano
        public bool leve = true;          // Mundo.leve (o padrão da edição da TV Box)
        public bool detalhes = true;      // vento, enfeites que se mexem, borboletas, vozes
        public readonly IModelosCenario modelos;
        public readonly Dictionary<string, No> grupos = new Dictionary<string, No>();
        public readonly Dictionary<string, List<AnimCenario>> anim = new Dictionary<string, List<AnimCenario>>();
        public readonly Dictionary<string, List<VozCenario>> vozes = new Dictionary<string, List<VozCenario>>();
        readonly Dictionary<string, List<No>> _duplas = new Dictionary<string, List<No>>();
        readonly Dictionary<string, List<KeyValuePair<V2, double>>> _ocupado = new Dictionary<string, List<KeyValuePair<V2, double>>>();
        readonly Dictionary<string, V3[]> _amostras = new Dictionary<string, V3[]>();
        readonly Dictionary<string, CaixaG> _caixas = new Dictionary<string, CaixaG>();
        readonly Dictionary<string, bool> _existe = new Dictionary<string, bool>();
        /// <summary>Avisos do plano (peça que encosta em outra).</summary>
        public readonly List<string> avisos = new List<string>();

        public PlanoCenario(IModelosCenario modelos, bool leve, bool detalhes)
        {
            this.modelos = modelos;
            this.leve = leve;
            this.detalhes = detalhes;
        }

        bool Existe(string nome)
        {
            if (!_existe.TryGetValue(nome, out bool e))
            {
                e = modelos.Existe(nome);
                _existe[nome] = e;
            }
            return e;
        }

        public void Construir()
        {
            foreach (var nome in ORDEM)
            {
                grupos[nome] = new No(nome);
                anim[nome] = new List<AnimCenario>();
                vozes[nome] = new List<VozCenario>();
                _duplas[nome] = new List<No>();
                _ocupado[nome] = new List<KeyValuePair<V2, double>>();
            }
            Parque();
            Espaco();
            Mar();
            Dinos();
            Atras("parque", new[] { ("carrossel", 0.9), ("tenda", 1.0), ("arvore_verde", 1.4), ("pirulito", 1.3), ("arvore_laranja", 1.4), ("cupcake", 1.5) });
            Atras("espaco", new[] { ("foguete", 1.1), ("pedra_lua", 1.7), ("cristal", 1.5), ("cristal", 1.1), ("pedra_lua", 1.3), ("foguete", 0.9) });
            Atras("mar", new[] { ("coral_roxo", 1.5), ("concha", 1.3), ("alga", 1.6), ("coral", 1.5), ("alga", 1.3), ("estrela_mar", 1.4) });
            Atras("dinos", new[] { ("palmeira", 1.1), ("samambaia", 1.5), ("ovo_dino", 1.3), ("pedra", 1.4), ("palmeira", 0.95), ("flor_gigante", 1.4) });
            // o que se mexe fica fora do lote; as peças paradas que têm uma
            // versão mais leve ficam no nível "alto" e a gêmea dela no "leve";
            // as que se mexem (ou estão penduradas em outra) trocam de malha
            foreach (var nome in ORDEM)
            {
                var fora = new HashSet<No>();
                foreach (var item in anim[nome])
                {
                    if (item.no != null) fora.Add(item.no);
                    if (item.roda != null) fora.Add(item.roda);
                    if (item.cabines != null) foreach (var c in item.cabines) fora.Add(c);
                }
                foreach (var v in vozes[nome]) fora.Add(v.no);
                foreach (var n in fora) n.mexe = true;
                var g = grupos[nome];
                foreach (var mi in _duplas[nome])
                {
                    if (fora.Contains(mi) || mi.pai != g || mi.filhos.Count > 0)
                    {
                        mi.troca = true;
                        continue;
                    }
                    var gemea = new No(mi.nome)
                    {
                        malha = mi.malhaLeve, malhaLeve = mi.malhaLeve, modelo = mi.modelo, material = mi.material,
                        temaAtlas = mi.temaAtlas, tinta = mi.tinta, borda = mi.borda, luzMinima = mi.luzMinima, vento = mi.vento,
                        nivel = 2, Transform = mi.Transform,
                    };
                    g.Juntar(gemea);
                    mi.nivel = 1;
                    mi.malhaLeve = mi.malha;
                }
            }
        }

        public static string Proximo(string nome) => ORDEM[(Array.IndexOf(ORDEM, nome) + 1) % ORDEM.Length];

        int Q(int n) => leve ? Math.Max(1, (int)Math.Round(n * 0.65, MidpointRounding.AwayFromZero)) : n;

        // ================================================================ peças
        /// <summary>A fase de cada objeto (o atlas onde está o desenho dele).</summary>
        public static readonly Dictionary<string, string> TEMA_DO_MODELO = new Dictionary<string, string>
        {
            ["ptero"] = "dinos", ["dino"] = "dinos", ["vulcao"] = "dinos", ["palmeira"] = "dinos", ["samambaia"] = "dinos",
            ["flor_gigante"] = "dinos", ["ovo_dino"] = "dinos", ["pedra"] = "dinos", ["saturno"] = "espaco", ["foguete"] = "espaco",
            ["ovni"] = "espaco", ["planeta"] = "espaco", ["estrela_3d"] = "espaco", ["cristal"] = "espaco", ["pedra_lua"] = "espaco",
            ["navio"] = "mar", ["bau"] = "mar", ["bau_tampa"] = "mar", ["coral"] = "mar", ["coral_roxo"] = "mar", ["alga"] = "mar",
            ["estrela_mar"] = "mar", ["concha"] = "mar", ["peixe"] = "mar", ["peixe_azul"] = "mar", ["peixe_amarelo"] = "mar",
            ["agua_viva"] = "mar", ["tartaruga"] = "mar", ["anemona"] = "mar", ["tricera"] = "dinos", ["satelite"] = "espaco",
        };

        public static string TemaDoModelo(string nome) => TEMA_DO_MODELO.TryGetValue(nome, out var t) ? t : "parque";

        // AS TRÊS VERSÕES DE CADA OBJETO: <nome> (de perto), <nome>_meio e
        // <nome>_longe, escolhidas pelo TAMANHO NA TELA (o raio da peça pela
        // distância até a pista); e os dois níveis do cenário (o leve é um
        // passo mais leve em cada peça).
        const double PERTO = 0.26;
        const double LONGE = 0.115;
        static readonly string[] SUFIXOS = { "", "_meio", "_longe" };

        string MalhaNivel(string nome, double raio, double dist, int passos)
        {
            int nivel = raio >= PERTO * dist ? 0 : (raio >= LONGE * dist ? 1 : 2);
            nivel = Math.Min(nivel + passos, 2);
            while (nivel > 0)
            {
                string c = nome + SUFIXOS[nivel];
                if (Existe(c)) return c;
                nivel--;
            }
            return nome;
        }

        // a tinta própria de alguns modelos (o ouro do baú debaixo d'água, a
        // sombra mais clara do Saturno)
        static readonly Dictionary<string, (Cor tinta, float luzMinima)> TINTA_MODELO = new Dictionary<string, (Cor, float)>
        {
            ["bau"] = (new Cor(1.3f, 1.0f, 0.82f), 0.6f),
            ["saturno"] = (Cor.Branco, 0.64f),
        };

        // VENTO NAS PLANTAS (detalhes extras): [metros que o topo anda, rapidez];
        // só as de perto da pista
        const double VENTO_PERTO = 9.0;
        static readonly Dictionary<string, float[]> VENTO = new Dictionary<string, float[]>
        {
            ["palmeira"] = new[] { 0.11f, 1.1f }, ["samambaia"] = new[] { 0.05f, 1.7f }, ["flor_gigante"] = new[] { 0.06f, 1.4f },
            ["arvore_verde"] = new[] { 0.05f, 1.0f }, ["arvore_laranja"] = new[] { 0.05f, 1.0f }, ["arvore_escura"] = new[] { 0.05f, 1.0f },
            ["alga"] = new[] { 0.13f, 1.2f }, ["coral"] = new[] { 0.025f, 0.9f }, ["coral_roxo"] = new[] { 0.03f, 0.9f },
        };

        No Obj(string tema, string nome, V3 pos, double rotY = 0.0, double esc = 1.0, Cor? tinta = null, No pai = null, double distLod = -1.0)
        {
            var mi = new No(nome) { modelo = nome, material = "atlas", temaAtlas = tema };
            string mLeve = null;
            if (Existe(nome))
            {
                var caixa = CaixaDe(nome);
                double raio = caixa.tam.Length() * 0.5 * esc;
                double dist = distLod >= 0.0 ? distLod : (pai == null ? AtePista(pos) : 0.0);
                mi.malha = MalhaNivel(nome, raio, dist, 0);
                mLeve = MalhaNivel(nome, raio, dist, 1);
            }
            var t = tinta ?? Cor.Branco;
            if (t.Igual(Cor.Branco) && TINTA_MODELO.TryGetValue(nome, out var tm))
            {
                t = tm.tinta;
                mi.luzMinima = tm.luzMinima;
            }
            mi.tinta = t;
            if (detalhes && VENTO.TryGetValue(nome, out var cfg) && mi.malha != null && pai == null && AtePista(pos) < VENTO_PERTO)
                mi.vento = cfg;
            mi.malhaLeve = mLeve ?? mi.malha;
            if (mLeve != null && mLeve != mi.malha) _duplas[tema].Add(mi);
            mi.Translation = pos;
            mi.RotY((float)rotY);
            mi.Scale = V3.Um * (float)esc;
            (pai ?? grupos[tema]).Juntar(mi);
            if (nome == "bau") TampaDoBau(tema, mi);
            return mi;
        }

        /// <summary>Distância (no chão) até a pista, da linha de falta (z = 4) até os pinos (z = -10).</summary>
        public static double AtePista(V3 pos)
        {
            double dz = Math.Max(pos.z - 4.0, Math.Max(-10.0 - pos.z, 0.0));
            return new V3(pos.x, Math.Max(pos.y - 2.0, 0.0), dz).Length();
        }

        // O BAÚ DO TESOURO: o corpo e a TAMPA (com a origem na dobradiça); abre
        // na vez da voz dele, mostra o tesouro, fecha e abre de novo
        public static readonly V3 BAU_DOBRADICA = new V3(0.0f, 0.452f, -0.297f);
        public const float BAU_ABERTO = -1.85f;
        public const float BAU_ABRINDO = 0.9f;
        public const float BAU_FECHANDO = 0.55f;
        public const float BAU_MOSTRA = 5.5f;

        void TampaDoBau(string tema, No corpo)
        {
            if (!Existe("bau_tampa")) return;
            var t = new No("bau_tampa")
            {
                malha = "bau_tampa", malhaLeve = "bau_tampa", modelo = "bau_tampa", material = corpo.material,
                temaAtlas = corpo.temaAtlas, tinta = corpo.tinta, luzMinima = corpo.luzMinima,
            };
            corpo.pai.Juntar(t);
            t.Transform = corpo.Transform * new Pose(Base.Identidade, BAU_DOBRADICA);
            var item = new AnimCenario { tipo = "bau", no = t, corpo = corpo };
            if (detalhes)
            {
                var f = Particulas(tema, V3.Zero, "faisca", 14, 1.3f, 0.16f, true);
                f.umaVez = true;
                f.explosividade = 0.6f;
                f.direcao = V3.Cima;
                f.abertura = 35f;
                f.gravidade = new V3(0f, -1.2f, 0f);
                f.vMin = 1.0f; f.vMax = 1.8f;
                f.cor = new Cor(1.0f, 0.86f, 0.35f);
                item.faiscas = f;
            }
            Animar(tema, item);
            Voz(tema, corpo, "brilho", 12f, 20f, 0.75f, "abrir", item);
        }

        double Chao(double x, double z) => AlturaTerreno(x, z);

        void Animar(string tema, AnimCenario item) => anim[tema].Add(item);

        // NADA ATRAVESSANDO NADA, TUDO NO CHÃO: cada peça marca o lugar dela
        // (a pegada) e é assentada no terreno
        const double INCLINA_MAX = 0.16;
        const double FOLGA_PEGADA = 0.25;

        CaixaG CaixaDe(string nome)
        {
            if (!_caixas.TryGetValue(nome, out var c))
            {
                c = Existe(nome) ? modelos.CaixaDe(nome) : new CaixaG();
                _caixas[nome] = c;
            }
            return c;
        }

        double Pegada(string nome, double esc)
        {
            if (!Existe(nome)) return 0.5 * esc;
            var caixa = CaixaDe(nome);
            double rx = Math.Max(Math.Abs(caixa.pos.x), Math.Abs(caixa.Fim.x));
            double rz = Math.Max(Math.Abs(caixa.pos.z), Math.Abs(caixa.Fim.z));
            return Math.Sqrt(rx * rx + rz * rz) * 0.82 * esc;
        }

        void Ocupar(string tema, V2 centro, double raio) => _ocupado[tema].Add(new KeyValuePair<V2, double>(centro, raio));

        bool Livre(string tema, V2 centro, double raio)
        {
            foreach (var o in _ocupado[tema])
                if (o.Key.DistanceTo(centro) < o.Value + raio + FOLGA_PEGADA) return false;
            return true;
        }

        /// <summary>A pose de uma peça posta em (x, z): inclinada com o morro e encostada no chão,
        /// ou de pé no ponto mais baixo de um círculo.</summary>
        Pose PoseNoChao(Base b, CaixaG caixa, double x, double z, double raio, double enterrar, bool inclinar, double raioBase = -1.0)
        {
            if (!inclinar)
                return new Pose(b, new V3(x, MaisBaixo(x, z, raioBase < 0.0 ? raio * 0.7 : raioBase) - enterrar, z));
            double r = Math.Max(raio * 0.7, 0.15);
            var g = new V2(Chao(x + r, z) - Chao(x - r, z), Chao(x, z + r) - Chao(x, z - r)) / (float)(2.0 * r);
            double subida = g.Length();
            V2 dir = subida > 0.0001 ? g / (float)subida : V2.Zero;
            double ang = Math.Min(Math.Atan(subida), INCLINA_MAX);
            // o que é alto e fino cresce de pé: quase não inclina
            if (caixa.tam.y > 1.6 * Math.Max(caixa.tam.x, caixa.tam.z)) ang *= 0.3;
            var giro = b;
            if (ang > 0.001)
            {
                var normal = new V3(-dir.x * Math.Sin(ang), Math.Cos(ang), -dir.y * Math.Sin(ang));
                giro = Base.Giro(V3.Cima.Cross(normal).Normalized(), ang) * giro;
            }
            double t = Math.Tan(ang);
            double y = Chao(x, z);
            for (int k = 0; k < 12; k++)
            {
                double a = TAU * k / 12.0;
                var q = new V2(Math.Cos(a), Math.Sin(a)) * (float)r;
                y = Math.Min(y, Chao(x + q.x, z + q.y) - t * q.Dot(dir));
            }
            return new Pose(giro, new V3(x, y - enterrar, z));
        }

        // NADA DENTRO DO MORRO (7.5/7.6): a peça é conferida pelos PONTOS DO
        // MODELO; a saia dele (o que fica abaixo da origem) não conta
        const double ENTRA_MAX = 0.06;
        const double PROCURA_MAX = 3.5;
        const double CORREDOR = 2.3;
        const int GRADE = 12;

        V3[] Amostra(string nome)
        {
            if (_amostras.TryGetValue(nome, out var feito)) return feito;
            var pts = new List<V3>();
            foreach (var suf in new[] { "_meio", "", "_longe" })
            {
                string c = nome + suf;
                if (!Existe(c)) continue;
                var vs = modelos.Pontos(c);
                if (vs == null || vs.Length == 0) continue;
                var caixa = modelos.CaixaDe(c);
                double cx = Math.Max(caixa.tam.x, 0.001f) / (double)GRADE;
                double cz = Math.Max(caixa.tam.z, 0.001f) / (double)GRADE;
                var ordem = new List<int>();
                var baixos = new Dictionary<int, V3>();
                foreach (var v in vs)
                {
                    int k = Math.Min((int)(((double)v.x - caixa.pos.x) / cx), GRADE - 1) * GRADE + Math.Min((int)(((double)v.z - caixa.pos.z) / cz), GRADE - 1);
                    if (!baixos.TryGetValue(k, out var b))
                    {
                        baixos[k] = v;
                        ordem.Add(k);
                    }
                    else if (v.y < b.y) baixos[k] = v;
                }
                foreach (int k in ordem) pts.Add(baixos[k]);
                int passo = Math.Max(1, vs.Length / 30);
                for (int i = 0; i < vs.Length; i += passo) pts.Add(vs[i]);
                break;
            }
            var r = pts.ToArray();
            _amostras[nome] = r;
            return r;
        }

        double Fundura(V3[] pts, Pose xf, double basta = double.PositiveInfinity)
        {
            double fundo = 0.0;
            foreach (var v in pts)
            {
                var w = xf.Xform(v);
                fundo = Math.Max(fundo, Chao(w.x, w.z) - w.y);
                if (fundo > basta) return fundo;
            }
            return fundo;
        }

        double Entra(V3[] pts, Pose xf, CaixaG caixa, double enterrar, double basta = double.PositiveInfinity)
        {
            double saia = Math.Max(-caixa.pos.y, 0.0f) * xf.b.Escala().y;
            return Fundura(pts, xf, basta + saia + enterrar) - saia - enterrar;
        }

        bool NoCorredor(V3[] pts, Pose xf)
        {
            foreach (var v in pts)
            {
                var w = xf.Xform(v);
                if (Math.Abs(w.x) < CORREDOR && w.z > Z_FUNDO - 3.0 && w.z < 4.0) return true;
            }
            return false;
        }

        Pose LugarFirme(string tema, V3[] pts, Base b, CaixaG caixa, double x, double z, double raio,
            double enterrar, bool inclinar, double raioBase = -1.0)
        {
            var melhor = PoseNoChao(b, caixa, x, z, raio, enterrar, inclinar, raioBase);
            if (pts.Length == 0) return melhor;
            double menor = Entra(pts, melhor, caixa, enterrar);
            if (menor <= ENTRA_MAX) return melhor;
            int anel = 1;
            while (anel * 0.5 <= PROCURA_MAX + 0.01)
            {
                int voltas = 8 + anel * 2;
                for (int k = 0; k < voltas; k++)
                {
                    double a = TAU * k / voltas;
                    var c = new V2(x, z) + new V2(Math.Cos(a), Math.Sin(a)) * anel * 0.5f;
                    if (Math.Sign(c.x) != Math.Sign(x) || Math.Abs(c.x) < Math.Abs(x) - 1.0) continue;
                    if (!Livre(tema, c, raio - FOLGA_PEGADA)) continue;
                    var xf = PoseNoChao(b, caixa, c.x, c.y, raio, enterrar, inclinar, raioBase);
                    if (NoCorredor(pts, xf)) continue;
                    double f = Entra(pts, xf, caixa, enterrar, menor);
                    if (f <= ENTRA_MAX) return xf;
                    if (f < menor)
                    {
                        menor = f;
                        melhor = xf;
                    }
                }
                anel++;
            }
            return melhor;
        }

        static Base GiroEscala(double giro, double esc) => Base.Giro(V3.Cima, giro).Scaled(V3.Um * (float)esc);

        void Espalhar(string tema, string[] tipos, int qtd, ulong semente, Dictionary<string, double> escala = null)
        {
            var rnd = new Sorteio(semente);
            int colocados = 0, tentativas = 0;
            while (colocados < qtd && tentativas < 4000)
            {
                tentativas++;
                double s = rnd.Randf() < 0.5 ? -1.0 : 1.0;
                double x = s * rnd.Faixa(3.6, 15.0);
                double z = rnd.Faixa(-34.0, 7.0);
                if (z > -2.0 && Math.Abs(x) < 5.0) continue;
                string tipo = tipos[rnd.Randi() % tipos.Length];
                double esc = rnd.Faixa(0.9, 1.35) * (escala != null && escala.TryGetValue(tipo, out double e) ? e : 1.0);
                double r = Pegada(tipo, esc);
                var p = new V2(x, z);
                if (!Livre(tema, p, r)) continue;
                double giro = rnd.Randf() * TAU;
                var xf = PoseNoChao(GiroEscala(giro, esc), CaixaDe(tipo), x, z, r, 0.04, true);
                if (Entra(Amostra(tipo), xf, CaixaDe(tipo), 0.04, ENTRA_MAX) > ENTRA_MAX) continue;
                Ocupar(tema, p, r);
                var mi = Obj(tema, tipo, new V3(x, 0.0, z), giro, esc);
                mi.Transform = xf;
                colocados++;
            }
        }

        No Grande(string tema, string nome, double x, double z, double giro = 0.0, double esc = 1.0,
            double enterrar = 0.06, bool inclinar = true, bool procurar = true)
        {
            double r = Pegada(nome, esc);
            var b = GiroEscala(giro, esc);
            var xf = PoseNoChao(b, CaixaDe(nome), x, z, r, enterrar, inclinar);
            if (procurar) xf = LugarFirme(tema, Amostra(nome), b, CaixaDe(nome), x, z, r, enterrar, inclinar);
            x = xf.o.x;
            z = xf.o.z;
            if (!Livre(tema, new V2(x, z), r - FOLGA_PEGADA))
                avisos.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "cenário {0}: {1} em ({2:0.0}, {3:0.0}) encosta em outra peça", tema, nome, x, z));
            Ocupar(tema, new V2(x, z), r);
            var mi = Obj(tema, nome, new V3(x, 0.0, z), giro, esc);
            mi.Transform = xf;
            return mi;
        }

        No Talvez(string tema, string nome, double x, double z, double giro = 0.0, double esc = 1.0)
        {
            double r = Pegada(nome, esc);
            if (!Livre(tema, new V2(x, z), r)) return null;
            var xf = PoseNoChao(GiroEscala(giro, esc), CaixaDe(nome), x, z, r, 0.02, true);
            if (Entra(Amostra(nome), xf, CaixaDe(nome), 0.02, ENTRA_MAX) > ENTRA_MAX) return null;
            Ocupar(tema, new V2(x, z), r);
            var mi = Obj(tema, nome, new V3(x, 0.0, z), giro, esc);
            mi.Transform = xf;
            return mi;
        }

        /// <summary>A altura que deixa a QUILHA de uma peça comprida toda no chão ou abaixo dele.</summary>
        double PousarQuilha(No mi, double de, double ate, double meia)
        {
            double y = double.PositiveInfinity;
            var b = mi.Transform.b;
            for (int k = 0; k < 8; k++)
            {
                double z = Lerp(de, ate, k / 7.0);
                foreach (double l in new[] { -meia, 0.0, meia })
                {
                    var w = b.Xform(new V3(l, 0.0, z));
                    y = Math.Min(y, Chao(mi.Translation.x + w.x, mi.Translation.z + w.z) - w.y);
                }
            }
            return y;
        }

        double MaisBaixo(double x, double z, double r)
        {
            double y = Chao(x, z);
            for (int k = 0; k < 12; k++)
            {
                double a = TAU * k / 12.0;
                y = Math.Min(y, Chao(x + Math.Cos(a) * r, z + Math.Sin(a) * r));
            }
            return y;
        }

        List<No> Destaques(string tema, (string nome, double esc)[] itens)
        {
            var feitos = new List<No>();
            for (int i = 0; i < Math.Min(itens.Length, DESTAQUES.Length); i++)
            {
                var d = DESTAQUES[i];
                var it = itens[i];
                double x = d[0];
                double z = d[1];
                double r = Pegada(it.nome, it.esc);
                // (os que ficam no piso de quem joga não são assentados no morro)
                bool noPiso = z > 0.0 && Math.Abs(x) < 3.3;
                var xf = Pose.Identidade;
                if (!noPiso)
                {
                    var b = GiroEscala(d[2], it.esc);
                    xf = LugarFirme(tema, Amostra(it.nome), b, CaixaDe(it.nome), x, z, r, 0.02, true);
                    x = xf.o.x;
                    z = xf.o.z;
                }
                Ocupar(tema, new V2(x, z), r);
                var mi = Obj(tema, it.nome, new V3(x, -0.02, z), d[2], it.esc);
                if (!noPiso) mi.Transform = xf;
                feitos.Add(mi);
            }
            return feitos;
        }

        /// <summary>ENFEITES QUE SE MEXEM (detalhes extras): alguns destaques ganham uma animação.</summary>
        void Vida(string tema, List<No> feitos, (int qual, string tipo)[] quais)
        {
            if (!detalhes) return;
            int i = 0;
            foreach (var q in quais)
            {
                var no = feitos[q.qual];
                var item = new AnimCenario { tipo = q.tipo, no = no, basePos = no.Translation, giro = no.Rotation.y, fase = i * 1.37f, cada = 3.6f + i * 0.7f };
                switch (q.tipo)
                {
                    case "pular": item.altura = 0.16f; break;
                    case "chocar": item.cada = 4.5f + i * 1.1f; break;
                    case "levitar": item.vel = 0.9f + 0.2f * i; break;
                    case "girar": item.vel = i % 2 == 0 ? 0.35f : -0.3f; break;
                    case "olhar": item.vel = 0.5f + 0.15f * i; break;
                    case "brilhar": no.materialProprio = true; break;
                }
                Animar(tema, item);
                i++;
            }
        }

        void Atras(string tema, (string nome, double esc)[] itens)
        {
            for (int i = 0; i < Math.Min(itens.Length, ATRAS.Length); i++)
            {
                var a = ATRAS[i];
                double x = a[0], z = a[1];
                string nome = itens[i].nome;
                double esc = itens[i].esc;
                double giro = PI + a[2];
                var b = GiroEscala(giro, esc);
                var xf = LugarFirme(tema, Amostra(nome), b, CaixaDe(nome), x, z, Pegada(nome, esc), 0.05, true);
                var mi = Obj(tema, nome, xf.o, giro, esc);
                mi.Transform = xf;
            }
        }

        // OS DOIS PILARES do gabinete ganham um ENFEITE DA FASE em cima (7.6)
        const double PILAR_X = 1.93;
        const double PILAR_TOPO = 2.76;
        static readonly V3 OLHO_DA_PISTA = new V3(0.0f, 1.05f, 2.2f);

        void NosPilares(string tema, string nome, double esc, double giro = 0.0, bool dePe = false, double largura = 1.0)
        {
            var caixa = CaixaDe(nome);
            foreach (double s in new[] { -1.0, 1.0 })
            {
                var b = Base.Giro(V3.Cima, giro * s);
                if (dePe) b = b * Base.Giro(V3.Direita, PI / 2.0);
                b = b.Scaled(new V3(esc * largura, esc, esc * largura));
                double baixo = double.PositiveInfinity;
                for (int k = 0; k < 8; k++)
                {
                    var c = caixa.pos + new V3(caixa.tam.x * (k & 1), caixa.tam.y * ((k >> 1) & 1), caixa.tam.z * ((k >> 2) & 1));
                    baixo = Math.Min(baixo, b.Xform(c).y);
                }
                var pos = new V3(s * PILAR_X, PILAR_TOPO - baixo, Z_PINO1 + 0.75);
                var mi = Obj(tema, nome, pos, 0.0, esc, null, null, pos.DistanceTo(OLHO_DA_PISTA));
                mi.Transform = new Pose(b, pos);
            }
        }

        void OcuparLinha(string tema, V2 a, V2 b, double raio)
        {
            int n = Math.Max((int)Math.Ceiling(a.DistanceTo(b) / raio), 1);
            for (int i = 0; i <= n; i++) Ocupar(tema, a.Lerp(b, (float)i / n), raio);
        }

        ParticulasCenario Particulas(string tema, V3 pos, string textura, int qtd, float vida, float tam, bool somar = false)
        {
            var p = new ParticulasCenario { textura = textura, qtd = qtd, vida = vida, tam = tam, somar = somar };
            var no = new No("particulas") { material = "particulas", dados = p, Translation = pos };
            grupos[tema].Juntar(no);
            Animar(tema, new AnimCenario { tipo = "nada", particulas = p, no = no });
            return p;
        }

        No Emissor(string tema, V3 pos)
        {
            var e = new No("emissor") { Translation = pos };
            grupos[tema].Juntar(e);
            return e;
        }

        void Voz(string tema, No no, string som, float cadaMin, float cadaMax, float vol = 0.6f, string acao = "", AnimCenario item = null, float tom = 1f)
        {
            vozes[tema].Add(new VozCenario { no = no, som = som, cadaMin = cadaMin, cadaMax = cadaMax, vol = vol, tom = tom, acao = acao, item = item });
        }

        // ================================================================ PARQUE
        void Parque()
        {
            const string T = "parque";
            // arco-íris com lâmpadas
            Obj(T, "arco_iris", new V3(0.0, -0.05, Z_FUNDO - 0.5), 0.0, 0.95);
            OcuparLinha(T, new V2(-3.1, Z_FUNDO - 0.5), new V2(3.1, Z_FUNDO - 0.5), 0.45);
            double R = 3.0 * 0.95 - 0.42 * 0.95 - 0.08;
            var lamp = new LampadasCenario { fase0 = 100 };
            for (int i = 0; i < 40; i++)
            {
                double a = PI * (i + 0.5) / 40.0;
                lamp.pontos.Add(new V3(Math.Cos(a) * R, Math.Sin(a) * R - 0.05, Z_FUNDO - 0.5 + 0.38));
            }
            grupos[T].Juntar(new No("lampadas") { material = "lampadas", dados = lamp });
            // as TORRES de circo em cima dos pilares (7.6)
            NosPilares(T, "torre", 0.5, 0.0, false, 1.25);
            // roda-gigante (gira) com as cabines sempre de pé, no lugar firme
            var pts = new List<V3>(Amostra("roda_base"));
            var cab = CaixaDe("cabine");
            for (int k = 0; k < 11; k++)
            {
                double a = PI + PI * (k + 0.5) / 11.0;
                foreach (double lado in new double[] { cab.pos.x, cab.Fim.x })
                    pts.Add(new V3(Math.Cos(a) * 3.8 + lado, 4.6 + Math.Sin(a) * 3.8 + cab.pos.y, 0.0));
            }
            var lugar = LugarFirme(T, pts.ToArray(), Base.Giro(V3.Cima, 0.35), new CaixaG(), -7.0, -27.6, 2.0, 0.05, false, 2.0);
            double rx = lugar.o.x, rz = lugar.o.z;
            var rg = new No("roda_gigante") { Transform = lugar };
            grupos[T].Juntar(rg);
            var aoLado = new V2(Math.Cos(0.35), -Math.Sin(0.35)) * 3.4f;
            OcuparLinha(T, new V2(rx, rz) - aoLado, new V2(rx, rz) + aoLado, 1.3);
            Obj(T, "roda_base", V3.Zero, 0.0, 1.0, null, rg);
            var eixo = new No("eixo") { Translation = new V3(0f, 4.6f, 0f) };
            rg.Juntar(eixo);
            var roda = Obj(T, "roda", V3.Zero, 0.0, 1.0, null, eixo);
            var cabines = new List<No>();
            double longeRoda = AtePista(rg.Translation + new V3(0f, 4.6f, 0f));
            for (int i = 0; i < 8; i++)
                cabines.Add(Obj(T, "cabine", V3.Zero, 0.0, 1.0, CORES_BICHOS[i % CORES_BICHOS.Length], eixo, longeRoda));
            Animar(T, new AnimCenario { tipo = "roda", roda = roda, cabines = cabines, raio = 3.8f, vel = 0.16f });
            // carrossel (gira devagar) e tendas, cada um no seu patamar
            var car = Grande(T, "carrossel", 5.4, -17.8, 0.0, 1.2, 0.05, false);
            var giraCar = new AnimCenario { tipo = "girar", no = car, vel = 0.5f };
            Animar(T, giraCar);
            Voz(T, car, "amb_carrossel", 16f, 26f, 0.7f, "musica", giraCar);
            Grande(T, "tenda", -9.5, -13.0, 0.6, 1.1, 0.04, false);
            Grande(T, "tenda", 10.5, -33.0, -0.4, 1.3, 0.04, false);
            var car2 = Grande(T, "carrossel", -17.0, -42.0, 0.0, 1.0, 0.05, false);
            Animar(T, new AnimCenario { tipo = "girar", no = car2, vel = -0.35f });
            var d = Destaques(T, new[] { ("pirulito", 0.75), ("pirulito", 0.85), ("presente", 0.8), ("cupcake", 0.75),
                ("bengala", 0.9), ("bengala", 0.9), ("cogumelo", 0.9), ("donut", 0.85) });
            Vida(T, d, new[] { (0, "olhar"), (1, "olhar"), (2, "pular"), (3, "pulsar"), (6, "pular"), (7, "girar") });
            // POSTES DE LUZ dos dois lados da pista, canteiros e arbustos floridos
            foreach (double z in new[] { 1.0, -3.6, -8.2 })
                foreach (double s in new[] { -1.0, 1.0 })
                    Talvez(T, "poste", s * 3.55, z, PI / 2 * s, 1.0);
            foreach (var p in new[] { new[] { -3.3, -2.4 }, new[] { 3.4, -5.6 }, new[] { -3.6, -9.6 }, new[] { 3.7, 0.2 }, new[] { -4.6, 1.0 }, new[] { 4.8, -1.5 } })
                Talvez(T, (int)p[1] % 2 == 0 ? "flores" : "arbusto", p[0], p[1], p[0] * 0.7, 0.9);
            Espalhar(T, new[] { "arvore_verde", "arvore_laranja", "arvore_escura", "pirulito", "cogumelo", "bengala",
                "presente", "donut", "cupcake", "arvore_verde", "arvore_escura", "arvore_laranja", "arbusto", "flores",
                "arbusto", "flores" }, Q(60), 2026,
                new Dictionary<string, double> { ["arvore_verde"] = 1.25, ["arvore_laranja"] = 1.25, ["arvore_escura"] = 1.25, ["arbusto"] = 1.1 });
            Nuvens(T, 14, 77);
            Baloes(T);
            Borboletas(T, leve ? 3 : 5, 31);
        }

        // NUVENS BRANCAS (7.5): luz própria de céu, igual em todas as fases
        void Nuvens(string tema, int qtd, ulong semente)
        {
            var rnd = new Sorteio(semente);
            for (int i = 0; i < Q(qtd); i++)
            {
                double lado = i % 2 == 0 ? -1.0 : 1.0;
                var pos = new V3(lado * rnd.Faixa(4.0, 22.0), rnd.Faixa(6.0, 15.0), rnd.Faixa(-60.0, -8.0));
                double esc = rnd.Faixa(1.2, 2.6);
                pos.y = (float)AlturaNuvem(tema, pos, esc);
                var n = Obj(tema, "nuvem", pos, 0.0, esc);
                n.material = "nuvem";
                n.RotY((float)(rnd.Randf() * TAU));
                Animar(tema, new AnimCenario { tipo = "nuvem", no = n, vel = rnd.Faixa(0.15, 0.4) });
            }
        }

        /// <summary>A altura de uma NUVEM: 6 m acima do terreno em todo o caminho dela e acima
        /// das peças altas na mesma faixa de z.</summary>
        double AlturaNuvem(string tema, V3 pos, double esc)
        {
            double b = pos.y + 6.0;
            for (int k = 0; k < 31; k++) b = Math.Max(b, Chao(-30.0 + 2.0 * k, pos.z) + 6.0);
            double meiaZ = 0.95 * esc;
            foreach (var c in grupos[tema].filhos)
            {
                if (c.malha == null || c.material == "particulas") continue;
                var caixa = c.Transform.Xform(CaixaDe(c.malha));
                if (caixa.tam.y < 4.0f) continue;
                if (Math.Abs(caixa.pos.z + caixa.tam.z * 0.5 - pos.z) < caixa.tam.z * 0.5 + meiaZ)
                    b = Math.Max(b, caixa.Fim.y + 2.0);
            }
            return b - 0.26 * esc;
        }

        void Baloes(string tema)
        {
            var rnd = new Sorteio(91);
            var cores = new[] { Cor.Hex("#FF4B5C"), Cor.Hex("#FFD43B"), Cor.Hex("#4FC3F7"), Cor.Hex("#5ED16A"), Cor.Hex("#2E7BFF"), Cor.Hex("#FF9F2E") };
            for (int i = 0; i < (leve ? 8 : 16); i++)
            {
                var b = new No("balao");
                var mi = new No("balao")
                {
                    malha = "balao", malhaLeve = "balao", modelo = "balao", material = "atlas", temaAtlas = tema,
                    tinta = cores[i % cores.Length], borda = 0.6f,
                };
                b.Juntar(mi);
                var fio = new No("fio") { material = "fio", dados = new FioCenario { raio = 0.004f, altura = 0.9f, gomos = 4 } };
                fio.Translation = new V3(0f, -0.45f, 0f);
                b.Juntar(fio);
                double lado = i % 2 == 0 ? -1.0 : 1.0;
                var bp = new V3(lado * rnd.Faixa(1.8, 5.5), rnd.Faixa(2.2, 4.8), rnd.Faixa(-19.0, -4.0));
                if (i < 4) bp = new V3(lado * rnd.Faixa(2.0, 2.8), rnd.Faixa(2.6, 3.4), Z_FUNDO + rnd.Faixa(0.0, 1.5));
                b.Translation = bp;
                b.Scale = V3.Um * rnd.Faixa(0.8, 1.15);
                grupos[tema].Juntar(b);
                Animar(tema, new AnimCenario
                {
                    tipo = "boiar", no = b, basePos = bp, fase = (float)(rnd.Randf() * TAU), vel = rnd.Faixa(0.6, 1.1),
                    amp = new V3(0.12f, 0.18f, 0f), balanco = 0.12f, temBalanco = true,
                });
            }
        }

        // ================================================================ ESPAÇO
        void Espaco()
        {
            const string T = "espaco";
            var rnd = new Sorteio(404);
            // SATURNO atrás dos pinos (a versão de perto sempre), girando em volta do eixo do anel
            var sat = Obj(T, "saturno", new V3(0.3, 6.4, Z_FUNDO - 3.6), 0.0, 1.75, null, null, 0.0);
            sat.Rotation = new V3(0.33f, 0.0f, -0.2f);
            Animar(T, new AnimCenario { tipo = "girar_proprio", no = sat, vel = 0.1f, eixo = new V3(-0.218f, 0.891f, 0.398f).Normalized() });
            // dois foguetes em pé no chão, ao lado do gabinete
            foreach (double s in new[] { -1.0, 1.0 })
                Grande(T, "foguete", s * 3.35, -11.0, -s * (35.0 * PI / 180.0), 0.95, 0.03, false);
            // uma ESTRELA fofa em cima de cada pilar (7.6)
            NosPilares(T, "estrela_3d", 1.25, -0.25);
            // disco voador passeando por cima do fim da pista
            var ovni = Obj(T, "ovni", new V3(0.0, 5.0, -12.1), 0.0, 1.0);
            var vooOvni = new AnimCenario { tipo = "passeio", no = ovni, centro = new V3(0.0f, 5.0f, -12.1f), raio3 = new V3(3.1f, 0.25f, 0.65f), vel = 0.3f, girar = 1.6f, ang = 0f };
            Animar(T, vooOvni);
            Voz(T, ovni, "amb_ovni", 12f, 20f, 0.75f, "balancar", vooOvni);
            // um SATÉLITE dando a volta no céu
            var sat2 = Obj(T, "satelite", new V3(0.0, 9.0, -20.0), 0.0, 1.3);
            Animar(T, new AnimCenario { tipo = "orbita", no = sat2, centro = new V3(0.0f, 9.5f, -22.0f), raio2 = new V2(10.0f, 7.0f), sobe = 0.6f, girar = 0.25f, vel = 0.12f, fase = 1.0f });
            // planetinhas coloridos no céu
            for (int i = 0; i < Q(8); i++)
            {
                double lado = i % 2 == 0 ? -1.0 : 1.0;
                var pos = new V3(lado * rnd.Faixa(6.0, 22.0), rnd.Faixa(5.0, 15.0), rnd.Faixa(-58.0, -14.0));
                var pl = Obj(T, "planeta", pos, rnd.Randf() * TAU, rnd.Faixa(1.6, 4.2), CORES_BICHOS[i % CORES_BICHOS.Length]);
                Animar(T, new AnimCenario { tipo = "girar", no = pl, vel = rnd.Faixa(0.05, 0.2) });
            }
            // estrelas fofas flutuando ao lado da pista
            for (int i = 0; i < Q(6); i++)
            {
                double lado = i % 2 == 0 ? -1.0 : 1.0;
                var bp = new V3(lado * rnd.Faixa(2.1, 3.4), rnd.Faixa(2.2, 3.8), rnd.Faixa(-17.0, -3.0));
                var e = Obj(T, "estrela_3d", bp, 0.0, rnd.Faixa(0.6, 0.9));
                var boia = new AnimCenario
                {
                    tipo = "boiar", no = e, basePos = bp, fase = (float)(rnd.Randf() * TAU), vel = rnd.Faixa(0.7, 1.1),
                    amp = new V3(0f, 0.2f, 0f), olhar = (float)(-lado * 0.35), vira = 0.45f, temVira = true,
                };
                Animar(T, boia);
                if (i < 3) Voz(T, e, "amb_estrela", 14f, 24f, 0.55f, "piruetar", boia);
            }
            var d = Destaques(T, new[] { ("cristal", 0.9), ("cristal", 1.0), ("pedra_lua", 1.0), ("cristal", 0.8),
                ("pedra_lua", 1.1), ("cristal", 1.0), ("pedra_lua", 1.0), ("cristal", 1.1) });
            Vida(T, d, new[] { (0, "brilhar"), (1, "brilhar"), (3, "brilhar"), (5, "brilhar"), (7, "brilhar") });
            Espalhar(T, new[] { "cristal", "pedra_lua", "cristal", "pedra_lua", "pedra_lua", "foguete" }, Q(46), 77,
                new Dictionary<string, double> { ["cristal"] = 1.3, ["pedra_lua"] = 1.6, ["foguete"] = 0.8 });
            // estrelas cadentes de vez em quando
            var cad = Particulas(T, new V3(0f, 13.0f, -30.0f), "faisca", 3, 1.6f, 0.6f, true);
            cad.caixa = new V3(14f, 3f, 8f);
            cad.direcao = new V3(1f, -0.35f, 0f);
            cad.abertura = 5f;
            cad.gravidade = V3.Zero;
            cad.vMin = 14f; cad.vMax = 18f;
            cad.cor = new Cor(1.0f, 0.95f, 0.8f);
        }

        // ================================================================ FUNDO DO MAR
        void Mar()
        {
            const string T = "mar";
            var rnd = new Sorteio(505);
            // NAVIO PIRATA naufragado no alto do morro, a quilha inteira na areia
            double nx = -9.5, nz = -22.5;
            double giro = 90.0 * PI / 180.0;
            var navio = Obj(T, "navio", new V3(nx, 0.0, nz), giro, 1.05);
            navio.Rotation = new V3(4.0 * PI / 180.0, giro, 0.0);
            var tn = navio.Translation;
            tn.y = (float)(PousarQuilha(navio, -2.0, 1.5, 0.6) + 0.06);
            navio.Translation = tn;
            var proa = new V2(Math.Sin(giro), Math.Cos(giro)) * 1.05f;
            OcuparLinha(T, new V2(nx, nz) + proa * -3.0f, new V2(nx, nz) + proa * 4.1f, 1.3);
            // o BAÚ DO TESOURO pertinho da pista
            Grande(T, "bau", BAU_PERTO.x, BAU_PERTO.z, -0.5, 0.75, 0.02, false);
            // uma ESTRELA-DO-MAR de pé em cima de cada pilar (7.6)
            NosPilares(T, "estrela_mar", 2.0, 0.0, true);
            var d = Destaques(T, new[] { ("coral", 0.8), ("alga", 0.8), ("estrela_mar", 1.0), ("concha", 0.8),
                ("coral_roxo", 0.9), ("alga", 0.9), ("concha", 0.5), ("estrela_mar", 1.0) });
            // ANÊMONAS cor-de-rosa perto da pista
            foreach (var p in new[] { new[] { -3.4, -3.0 }, new[] { 3.5, -7.2 }, new[] { -3.7, 0.8 }, new[] { 3.6, 1.5 } })
                Talvez(T, "anemona", p[0], p[1], p[0], 1.2);
            Espalhar(T, new[] { "coral", "coral_roxo", "alga", "alga", "estrela_mar", "concha", "pedra", "coral", "anemona", "anemona" }, Q(60), 31,
                new Dictionary<string, double> { ["coral"] = 1.5, ["coral_roxo"] = 1.5, ["alga"] = 1.3, ["estrela_mar"] = 1.6, ["concha"] = 0.7, ["pedra"] = 1.2, ["anemona"] = 1.4 });
            Vida(T, d, new[] { (3, "pulsar"), (6, "pulsar") });
            if (detalhes) Animar(T, new AnimCenario { tipo = "navio", no = navio, baseRot = navio.Rotation });
            // algas balançando na correnteza (as de perto da pista balançam no shader: ficam no lote)
            foreach (var g in new List<No>(grupos[T].filhos))
            {
                if (g.malha != null && g.vento == null && g.malha.StartsWith("alga"))
                    Animar(T, new AnimCenario { tipo = "alga", no = g, fase = (float)(rnd.Randf() * TAU), baseRot = g.Rotation });
            }
            // CARDUMES de peixinhos, cada um de uma espécie
            var especies = new[] { "peixe", "peixe_azul", "peixe_amarelo", "peixe" };
            for (int c = 0; c < (leve ? 3 : 4); c++)
            {
                var centro = new V3((c % 2 == 0 ? -1.0 : 1.0) * rnd.Faixa(3.0, 7.0), rnd.Faixa(2.6, 5.5), rnd.Faixa(-20.0, -4.0));
                string especie = especies[c % especies.Length];
                float vel = rnd.Faixa(0.25, 0.45) * (c % 2 == 0 ? 1f : -1f);
                for (int k = 0; k < (leve ? 3 : 4); k++)
                {
                    var p = Obj(T, especie, centro, 0.0, rnd.Faixa(0.75, 1.05));
                    var cc = centro + new V3(rnd.Faixa(-0.4, 0.4), rnd.Faixa(-0.3, 0.3), 0f);
                    var rr = new V2(rnd.Faixa(2.2, 3.0), rnd.Faixa(1.4, 2.0));
                    Animar(T, new AnimCenario { tipo = "peixe", no = p, centro = cc, raio2 = rr, vel = vel, fase = k * 0.22f, nado = (float)(rnd.Randf() * TAU) });
                    if (k == 0) Voz(T, p, "amb_peixe", 12f, 22f, 0.6f);
                }
            }
            // águas-vivas subindo e descendo
            for (int i = 0; i < Q(7); i++)
            {
                double lado = i % 2 == 0 ? -1.0 : 1.0;
                var bp = new V3(lado * rnd.Faixa(2.2, 5.0), rnd.Faixa(2.4, 5.2), rnd.Faixa(-19.0, -2.0));
                var cor = new[] { Cor.Hex("#FFB36B"), Cor.Hex("#9FF0C8"), Cor.Hex("#8FE9FF") }[i % 3];
                var av = Obj(T, "agua_viva", bp, 0.0, rnd.Faixa(0.7, 1.0), cor);
                Animar(T, new AnimCenario { tipo = "agua_viva", no = av, basePos = bp, fase = (float)(rnd.Randf() * TAU), vel = rnd.Faixa(0.8, 1.2) });
            }
            // a BALEIA canta lá no alto
            Voz(T, Emissor(T, new V3(4.0f, 16.0f, -40.0f)), "amb_baleia", 17f, 27f, 0.75f, "baleia");
            // tartarugas atravessando o mar lá no alto
            for (int i = 0; i < Q(2); i++)
            {
                var tt = Obj(T, "tartaruga", new V3(0.0, 5.6 + i * 3.0, -17.0 - i * 13.0), PI / 2 * (i == 0 ? 1 : -1), 1.5 + i * 0.7);
                Animar(T, new AnimCenario { tipo = "atravessar", no = tt, vel = 1.1f * (i == 0 ? 1 : -1), limite = 28.0f });
            }
            // bolhas subindo
            for (int i = 0; i < (leve ? 3 : 8); i++)
            {
                double lado = i % 2 == 0 ? -1.0 : 1.0;
                var pos = new V3(lado * rnd.Faixa(1.8, 6.0), 0.1, rnd.Faixa(-18.0, 1.0));
                var b = Particulas(T, pos, "bolha", 10, 4.0f, 0.16f);
                if (i % 3 == 0) Voz(T, Emissor(T, pos + new V3(0f, 1.5f, 0f)), "amb_bolhas", 9f, 16f, 0.5f);
                b.direcao = V3.Cima;
                b.abertura = 12f;
                b.gravidade = new V3(0f, 0.25f, 0f);
                b.vMin = 0.5f; b.vMax = 0.9f;
                b.escMin = 0.4f; b.escMax = 1.4f;
                b.esfera = 0.25f;
                b.cor = new Cor(0.85f, 1.0f, 1.0f, 0.85f);
            }
        }

        // ================================================================ SELVA DOS DINOS
        void Dinos()
        {
            const string T = "dinos";
            var rnd = new Sorteio(606);
            // VULCÃO fumegando à esquerda do gabinete; o menor do outro lado
            var vulcao = Grande(T, "vulcao", -11.0, -36.0, 0.3, 2.1, 0.4, false, false);
            Grande(T, "vulcao", 17.0, -46.0, -0.5, 1.1, 0.3, false, false);
            var fum = Particulas(T, vulcao.Translation + new V3(0f, 10.6f, 0f), "fumaca", leve ? 8 : 14, 5.0f, 2.4f);
            var ronco = new AnimCenario { tipo = "vulcao", no = vulcao, basePos = vulcao.Translation, fumaca = fum };
            Animar(T, ronco);
            Voz(T, vulcao, "amb_vulcao", 18f, 28f, 0.8f, "tremer", ronco);
            fum.direcao = new V3(0.25f, 1f, 0f);
            fum.abertura = 18f;
            fum.gravidade = new V3(0.15f, 0.15f, 0f);
            fum.vMin = 0.8f; fum.vMax = 1.3f;
            fum.escMin = 0.8f; fum.escMax = 1.6f;
            fum.curva = new[] { 0f, 0.4f, 1f, 1.6f };
            fum.rampa = new[] { new Cor(0.95f, 0.9f, 0.95f, 0.85f), new Cor(0.85f, 0.85f, 0.95f, 0.0f) };
            // um OVO de dinossauro em cima de cada pilar (7.6)
            NosPilares(T, "ovo_dino", 1.7, 0.5);
            // os dinos, cada um no seu PATAMAR
            var dino = Grande(T, "dino", 5.4, -17.8, -0.85, 1.0, 0.03, false);
            var resp1 = new AnimCenario { tipo = "respirar", no = dino, fase = 0f, giro = dino.Rotation.y };
            Animar(T, resp1);
            var dino2 = Grande(T, "dino", -7.0, -27.6, 0.75, 1.45, 0.04, false);
            var resp2 = new AnimCenario { tipo = "respirar", no = dino2, fase = 1.7f, giro = dino2.Rotation.y };
            Animar(T, resp2);
            Voz(T, dino, "amb_dino", 13f, 20f, 0.85f, "rugir", resp1);
            Voz(T, dino2, "amb_dino", 20f, 30f, 0.7f, "rugir", resp2, 0.85f);
            Voz(T, Emissor(T, new V3(-3.6f, 2.5f, -9.0f)), "amb_passaros", 11f, 19f, 0.55f);
            Voz(T, Emissor(T, new V3(4.2f, 2.8f, -15.0f)), "amb_passaros", 13f, 21f, 0.5f);
            // pterossauros voando em círculos, batendo as asas
            for (int i = 0; i < Q(3); i++)
            {
                double escP = rnd.Faixa(0.9, 1.3);
                float tom = rnd.Faixa(0.9, 1.1);
                var centro = new V3(rnd.Faixa(-3.0, 3.0), rnd.Faixa(6.0, 8.5), rnd.Faixa(-22.0, -9.0));
                var p = Obj(T, "ptero", V3.Zero, 0.0, escP, null, null, AtePista(centro));
                p.material = "asas";
                Voz(T, p, "amb_ptero", 10f, 18f, 0.7f, "", null, tom);
                Animar(T, new AnimCenario
                {
                    tipo = "voo", no = p, centro = centro, raio = rnd.Faixa(4.0, 7.0),
                    vel = rnd.Faixa(0.25, 0.4) * (i % 2 == 0 ? 1f : -1f), fase = (float)(rnd.Randf() * TAU), asas = rnd.Faixa(5.5, 7.0),
                });
            }
            var d = Destaques(T, new[] { ("samambaia", 1.0), ("flor_gigante", 1.0), ("ovo_dino", 1.0), ("flor_gigante", 0.9),
                ("samambaia", 1.2), ("pedra", 1.0), ("ovo_dino", 1.0), ("samambaia", 1.1) });
            // um FILHOTE DE TRICERÁTOPS no patamar dele, olhando quem joga
            var tri = Grande(T, "tricera", -6.6, -6.8, 0.95, 1.0, 0.03, false);
            var resp3 = new AnimCenario { tipo = "respirar", no = tri, fase = 0.9f, giro = tri.Rotation.y };
            Animar(T, resp3);
            Voz(T, tri, "amb_dino", 15f, 24f, 0.7f, "rugir", resp3, 1.35f);
            foreach (var p in new[] { new[] { 3.5, -4.6 }, new[] { -3.5, 1.2 }, new[] { 3.8, 1.8 }, new[] { -3.4, -11.2 } })
                Talvez(T, "arbusto", p[0], p[1], p[0], 0.95);
            Espalhar(T, new[] { "palmeira", "samambaia", "pedra", "flor_gigante", "samambaia", "palmeira", "ovo_dino", "pedra", "arbusto", "arbusto" }, Q(60), 2027,
                new Dictionary<string, double> { ["palmeira"] = 1.0, ["samambaia"] = 1.4, ["pedra"] = 1.3, ["flor_gigante"] = 1.3, ["ovo_dino"] = 1.2, ["arbusto"] = 1.2 });
            // os ovos tremem de vez em quando (alguém quer nascer!)
            Vida(T, d, new[] { (2, "chocar"), (6, "chocar") });
            Borboletas(T, leve ? 3 : 5, 47);
            Nuvens(T, 10, 88);
        }

        // ================================================================ borboletas
        public static readonly Cor[][] CORES_BORBOLETA =
        {
            new[] { Cor.Hex("#FFE14D"), Cor.Hex("#FF7A1A") }, new[] { Cor.Hex("#FFB3E6"), Cor.Hex("#C22EFF") },
            new[] { Cor.Hex("#9BF0FF"), Cor.Hex("#1E7BFF") }, new[] { Cor.Hex("#C8FF8A"), Cor.Hex("#2DB84A") },
        };

        void Borboletas(string tema, int qtd, int semente)
        {
            if (!detalhes) return;
            var rnd = new Sorteio((ulong)semente);
            for (int i = 0; i < qtd; i++)
            {
                var mi = new No("borboleta") { material = "borboleta", variante = (i + semente) % CORES_BORBOLETA.Length };
                mi.Scale = V3.Um * rnd.Faixa(2.8, 3.4);
                grupos[tema].Juntar(mi);
                double lado = i % 2 == 0 ? -1.0 : 1.0;
                var centro = new V3(lado * rnd.Faixa(1.9, 2.7), rnd.Faixa(0.9, 1.6), rnd.Faixa(-6.5, -1.0));
                Animar(tema, new AnimCenario
                {
                    tipo = "borboleta", no = mi, centro = centro,
                    raio3 = new V3(rnd.Faixa(0.3, 0.6), 0.3f, rnd.Faixa(0.8, 1.5)),
                    vel = rnd.Faixa(0.35, 0.55), fase = (float)(rnd.Randf() * TAU),
                });
            }
        }

        /// <summary>A malha de uma borboleta (asas em leque e o corpinho), no Godot:
        /// pontos e cores, 3 por triângulo. Olha para +Z; as asas no plano XZ.</summary>
        public static void MalhaBorboleta(Cor meio, Cor beirada, List<V3> pontos, List<Cor> cores)
        {
            var cima = new[] { new V2(0.0f, 0.012f), new V2(0.03f, 0.062f), new V2(0.068f, 0.082f), new V2(0.094f, 0.062f),
                new V2(0.09f, 0.022f), new V2(0.05f, -0.004f), new V2(0.004f, -0.006f) };
            var baixo = new[] { new V2(0.004f, -0.006f), new V2(0.05f, -0.012f), new V2(0.07f, -0.04f), new V2(0.056f, -0.07f),
                new V2(0.026f, -0.064f), new V2(0.004f, -0.03f) };
            foreach (float lado in new[] { 1f, -1f })
            {
                foreach (var asa in new[] { (cima, new V2(0.04f, 0.03f)), (baixo, new V2(0.03f, -0.03f)) })
                {
                    var pts = asa.Item1;
                    var c = asa.Item2;
                    for (int k = 0; k < pts.Length; k++)
                    {
                        var a = pts[k];
                        var b = pts[(k + 1) % pts.Length];
                        var tri = lado > 0f ? new[] { (c, meio), (a, beirada), (b, beirada) } : new[] { (c, meio), (b, beirada), (a, beirada) };
                        foreach (var par in tri)
                        {
                            pontos.Add(new V3(par.Item1.x * lado, 0f, par.Item1.y));
                            cores.Add(par.Item2);
                        }
                    }
                }
            }
            var corpo = Cor.Hex("#4A2E2A");
            var tris = new[]
            {
                new[] { new V3(0f, 0.003f, 0.04f), new V3(0.007f, 0.003f, 0.0f), new V3(-0.007f, 0.003f, 0.0f) },
                new[] { new V3(-0.007f, 0.003f, 0.0f), new V3(0.007f, 0.003f, 0.0f), new V3(0f, 0.003f, -0.055f) },
            };
            foreach (var t in tris)
                foreach (var v in t)
                {
                    pontos.Add(v);
                    cores.Add(corpo);
                }
        }
    }
}
