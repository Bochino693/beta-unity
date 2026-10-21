using System.Collections.Generic;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// ANIMAÇÕES DOS PERSONAGENS (poses-chave).
    ///
    /// Cada animação: duração, se repete, as poses-chave no tempo e eventos.
    /// Os números são OS MESMOS das edições Godot (ângulos em graus, no espaço
    /// do personagem, que olha para +Z) e viram coordenadas da Unity uma vez só,
    /// ao preparar o clipe (X espelhado: giro em Y e Z troca de sinal).
    ///   braco_*.x  negativo = levanta para a FRENTE, positivo = para trás
    ///   braco_e.z  positivo = abre para fora;  braco_d.z negativo = abre para fora
    ///   antebraco  x negativo = dobra o cotovelo (mão vem para a frente)
    ///   coxa.x     negativo = perna para a frente (sempre em relação ao chão)
    ///   canela.x   positivo = dobra o joelho
    ///   corpo.x    positivo = inclina para a frente
    ///   cabeca.x   positivo = olha para baixo; cabeca.y positivo = vira p/ esquerda
    /// A cabeça é grande e os braços curtinhos: mão levantada vai sempre pelo
    /// LADO da cabeça (braco.z bem aberto), nunca por cima ou por trás dela.
    /// Extras: "altura" (sobe/desce o quadril, m — os joelhos dobram sozinhos
    /// pela IK), "quadril" (x para o lado, z para a frente), "frente" (anda
    /// para a frente, m), "giro" (gira o corpo inteiro em Y, graus), "voo"
    /// (sobe o personagem todo, m). No clipe: pulos = [início, fim, altura]
    /// (arco de gravidade); ik = "trava" (pés presos, padrão), "chao" (só não
    /// deixa o pé afundar) ou "livre".
    /// </summary>
    public static class Animacoes
    {
        public class Pose : Dictionary<string, Vector3>
        {
            public Pose() { }
            public Pose(IDictionary<string, Vector3> d) : base(d) { }
        }

        public struct Evento
        {
            public float t;
            public string tipo, valor;
            public Evento(float t, string tipo, string valor)
            {
                this.t = t;
                this.tipo = tipo;
                this.valor = valor;
            }
        }

        public class Clipe
        {
            public string nome;
            public float dur = 1f;
            public bool loop;
            public string ik = "trava";
            public string depois;
            public Vector3[] pulos = new Vector3[0];     // (início, fim, altura)
            public Evento[] eventos = new Evento[0];
            public List<(float t, Pose p)> chaves = new List<(float, Pose)>();
            // preparado (coordenadas da Unity)
            public float[] tempos;
            public string[] canais;
            public Vector3[][] valores;
            public Vector3[][] tangentes;
        }

        public static readonly HashSet<string> ESCALARES = new HashSet<string> { "altura", "frente", "giro", "voo" };

        static Dictionary<string, Clipe> _clipes;

        public static IEnumerable<string> Todos
        {
            get
            {
                Montar();
                return _clipes.Keys;
            }
        }

        public static Clipe Pegar(string nome)
        {
            Montar();
            return _clipes.TryGetValue(nome, out var c) ? c : _clipes["parado"];
        }

        // ------------------------------------------------------------ ajudantes de escrita
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Vector3 S(float v) => new Vector3(v, 0, 0);

        static Pose P(Pose b, Pose mudancas)
        {
            var d = new Pose(b);
            foreach (var kv in mudancas) d[kv.Key] = kv.Value;
            return d;
        }

        static Pose P(Pose b) => new Pose(b);

        static void Novo(string nome, float dur, bool loop, (float, Pose)[] chaves, string ik = "trava", string depois = null,
            Vector3[] pulos = null, Evento[] eventos = null)
        {
            var c = new Clipe { nome = nome, dur = dur, loop = loop, ik = ik, depois = depois };
            c.chaves.AddRange(chaves);
            if (pulos != null) c.pulos = pulos;
            if (eventos != null) c.eventos = eventos;
            Preparar(c);
            _clipes[nome] = c;
        }

        static Evento E(float t, string tipo, string valor = null) => new Evento(t, tipo, valor);

        // ------------------------------------------------------------ as poses
        static readonly Pose PARADO = new Pose
        {
            ["braco_e"] = V(0, 0, 8), ["braco_d"] = V(0, 0, -8),
            ["antebraco_e"] = V(-12, 0, 0), ["antebraco_d"] = V(-12, 0, 0),
        };

        static readonly Pose PRONTO = new Pose
        {
            ["braco_d"] = V(-48, 0, -4), ["antebraco_d"] = V(-78, 0, 6),
            ["braco_e"] = V(-42, 0, 14), ["antebraco_e"] = V(-72, 0, -18),
            ["corpo"] = V(6, 0, 0), ["cabeca"] = V(4, 0, 0),
            ["coxa_e"] = V(-5, 0, 0), ["canela_e"] = V(10, 0, 0),
            ["coxa_d"] = V(-5, 0, 0), ["canela_d"] = V(10, 0, 0), ["altura"] = S(-0.012f),
        };

        static void Montar()
        {
            if (_clipes != null) return;
            _clipes = new Dictionary<string, Clipe>();
            // parado: o peso passa devagar de um pé para o outro, cabeça acompanha
            Novo("parado", 4f, true, new[]
            {
                (0f, P(PARADO, new Pose { ["quadril"] = V(0.012f, 0, 0), ["corpo"] = V(1, 0, -2), ["cabeca"] = V(0, 0, 3), ["altura"] = S(-0.006f) })),
                (2f, P(PARADO, new Pose { ["quadril"] = V(-0.012f, 0, 0), ["corpo"] = V(2, 0, 2), ["cabeca"] = V(3, 0, -3),
                    ["braco_e"] = V(4, 0, 11), ["braco_d"] = V(4, 0, -11), ["altura"] = S(-0.012f) })),
                (4f, P(PARADO, new Pose { ["quadril"] = V(0.012f, 0, 0), ["corpo"] = V(1, 0, -2), ["cabeca"] = V(0, 0, 3), ["altura"] = S(-0.006f) })),
            });
            Novo("pronto", 3.2f, true, new[]
            {
                (0f, P(PRONTO)),
                (1.6f, P(PRONTO, new Pose { ["altura"] = S(-0.008f), ["corpo"] = V(7, 0, 0) })),
                (3.2f, P(PRONTO)),
            });
            // ---------------------------------------------------- o ARREMESSO
            // passada de 3 passos: empurra a bola, desce, vai lá atrás, desliza e solta.
            var p1 = new Pose
            {
                ["braco_d"] = V(-40, 0, -6), ["antebraco_d"] = V(-25, 0, 0),
                ["braco_e"] = V(-30, 0, 20), ["antebraco_e"] = V(-30, 0, 0),
                ["coxa_d"] = V(-28, 0, 0), ["canela_d"] = V(10, 0, 0),
                ["coxa_e"] = V(14, 0, 0), ["canela_e"] = V(28, 0, 0),
                ["corpo"] = V(8, 0, 0), ["altura"] = S(-0.02f), ["frente"] = S(0.45f),
            };
            var p2 = new Pose
            {
                ["braco_d"] = V(12, 0, -4), ["antebraco_d"] = V(-5, 0, 0),
                ["braco_e"] = V(-15, 0, 40), ["antebraco_e"] = V(-20, 0, 0),
                ["coxa_e"] = V(-30, 0, 0), ["canela_e"] = V(12, 0, 0),
                ["coxa_d"] = V(16, 0, 0), ["canela_d"] = V(32, 0, 0),
                ["corpo"] = V(12, 0, 0), ["altura"] = S(-0.03f), ["frente"] = S(1.05f),
            };
            var p3 = new Pose
            {
                ["braco_d"] = V(78, 0, -4), ["antebraco_d"] = V(0, 0, 0),
                ["braco_e"] = V(-25, 0, 60), ["antebraco_e"] = V(-15, 0, 0),
                ["coxa_d"] = V(-32, 0, 0), ["canela_d"] = V(18, 0, 0),
                ["coxa_e"] = V(22, 0, 0), ["canela_e"] = V(40, 0, 0),
                ["corpo"] = V(24, 0, -4), ["cabeca"] = V(-16, 0, 0), ["altura"] = S(-0.06f), ["frente"] = S(1.7f),
            };
            var p4 = new Pose
            {
                ["braco_d"] = V(-18, 0, -4), ["antebraco_d"] = V(-8, 0, 0),
                ["braco_e"] = V(-10, 0, 80), ["antebraco_e"] = V(-10, 0, 0),
                ["coxa_e"] = V(-58, 0, 0), ["canela_e"] = V(72, 0, 0),
                ["coxa_d"] = V(42, 0, 0), ["canela_d"] = V(12, 0, 0),
                ["corpo"] = V(32, 0, -6), ["cabeca"] = V(-30, 0, 0), ["altura"] = S(-0.19f), ["frente"] = S(2.35f),
            };
            var p5 = P(p4, new Pose
            {
                ["braco_d"] = V(-128, 0, -8), ["antebraco_d"] = V(-25, 0, 0),
                ["braco_e"] = V(-5, 0, 82), ["corpo"] = V(26, 0, -4), ["cabeca"] = V(-24, 0, 0),
                ["altura"] = S(-0.17f), ["frente"] = S(2.5f),
            });
            var p6 = P(p5, new Pose { ["braco_d"] = V(-112, 0, -10), ["antebraco_d"] = V(-35, 0, 0), ["frente"] = S(2.55f), ["cabeca"] = V(-20, 0, 0) });
            Novo("arremesso", 2f, false, new[] { (0f, P(PRONTO)), (0.28f, p1), (0.55f, p2), (0.8f, p3), (1.02f, p4), (1.3f, p5), (2f, p6) },
                ik: "chao", eventos: new[]
                {
                    E(0.28f, "som", "passo"), E(0.55f, "som", "passo"), E(0.8f, "som", "passo"), E(1f, "soltar"),
                    E(0f, "expressao", "concentrado"), E(1f, "expressao", "surpreso"),
                });
            // depois de soltar: fica olhando a bola, na pose final
            Novo("assistindo", 1.6f, true, new[]
            {
                (0f, P(p6, new Pose { ["frente"] = S(0) })),
                (0.8f, P(p6, new Pose { ["frente"] = S(0), ["altura"] = S(-0.15f), ["braco_d"] = V(-100, 0, -12) })),
                (1.6f, P(p6, new Pose { ["frente"] = S(0) })),
            }, ik: "chao");
            // ---------------------------------------------------- STRIKE: pulão com pirueta e festa!
            var agacha = P(PARADO, new Pose
            {
                ["altura"] = S(-0.1f), ["corpo"] = V(16, 0, 0), ["cabeca"] = V(-6, 0, 0),
                ["braco_e"] = V(28, 0, 22), ["braco_d"] = V(28, 0, -22), ["antebraco_e"] = V(-20, 0, 0), ["antebraco_d"] = V(-20, 0, 0),
            });
            var voando = new Pose
            {
                ["braco_e"] = V(-162, 0, 26), ["braco_d"] = V(-162, 0, -26),
                ["antebraco_e"] = V(-10, 0, 0), ["antebraco_d"] = V(-10, 0, 0),
                ["coxa_e"] = V(-34, 0, 4), ["canela_e"] = V(70, 0, 0), ["coxa_d"] = V(-24, 0, -4), ["canela_d"] = V(78, 0, 0),
                ["pe_e"] = V(20, 0, 0), ["pe_d"] = V(24, 0, 0),
                ["corpo"] = V(-8, 0, 0), ["cabeca"] = V(-14, 0, 0),
            };
            var esticado = P(voando, new Pose
            {
                ["coxa_e"] = V(-6, 0, 0), ["canela_e"] = V(8, 0, 0), ["coxa_d"] = V(-6, 0, 0),
                ["canela_d"] = V(8, 0, 0), ["pe_e"] = V(30, 0, 0), ["pe_d"] = V(30, 0, 0), ["corpo"] = V(-4, 0, 0),
            });
            var aterrissa = P(agacha, new Pose { ["altura"] = S(-0.085f), ["braco_e"] = V(-120, 0, 52), ["braco_d"] = V(-120, 0, -52) });
            // soquinhos no ar: o punho sobe pelo LADO da cabeça
            var vibra1 = P(PARADO, new Pose
            {
                ["braco_d"] = V(-125, 0, -35), ["antebraco_d"] = V(-15, 0, 0), ["braco_e"] = V(-20, 0, 30),
                ["corpo"] = V(-4, 0, -6), ["altura"] = S(-0.03f), ["quadril"] = V(0.015f, 0, 0),
            });
            var vibra2 = P(vibra1, new Pose { ["braco_d"] = V(-155, 0, -35), ["antebraco_d"] = V(-5, 0, 0), ["altura"] = S(-0.005f), ["quadril"] = V(-0.01f, 0, 0) });
            // impulso: os braços vêm de trás para cima passando pela frente (sem chicotada)
            var impulso = P(agacha, new Pose
            {
                ["altura"] = S(-0.05f), ["corpo"] = V(8, 0, 0), ["braco_e"] = V(-70, 0, 20), ["braco_d"] = V(-70, 0, -20),
                ["antebraco_e"] = V(-25, 0, 0), ["antebraco_d"] = V(-25, 0, 0),
            });
            Pose G(Pose p, float giro) => P(p, new Pose { ["giro"] = S(giro) });
            Novo("comemora_strike", 3.1f, false, new[]
            {
                (0f, P(PARADO)), (0.2f, agacha), (0.3f, impulso),
                (0.42f, G(esticado, 25f)), (0.56f, G(voando, 125f)), (0.72f, G(voando, 245f)), (0.92f, G(esticado, 360f)),
                (1.06f, G(aterrissa, 360f)),
                (1.3f, G(vibra1, 360f)), (1.5f, G(vibra2, 360f)), (1.7f, G(vibra1, 360f)), (1.9f, G(vibra2, 360f)), (2.1f, G(vibra1, 360f)),
                (2.36f, P(agacha, new Pose
                {
                    ["giro"] = S(360f), ["altura"] = S(-0.06f), ["braco_e"] = V(-110, 0, 30), ["braco_d"] = V(-110, 0, -30),
                    ["antebraco_e"] = V(-20, 0, 0), ["antebraco_d"] = V(-20, 0, 0),
                })),
                (2.5f, G(esticado, 360f)),
                (2.78f, P(aterrissa, new Pose { ["giro"] = S(360f), ["altura"] = S(-0.05f) })),
                (3.1f, G(PARADO, 360f)),
            }, pulos: new[] { V(0.3f, 0.98f, 0.52f), V(2.42f, 2.74f, 0.12f) }, eventos: new[]
            {
                E(0f, "expressao", "empolgado"), E(0.3f, "jatos", "1"), E(0.3f, "som", "jato"), E(2.7f, "jatos", "0"),
                E(1.1f, "expressao", "risada"), E(1f, "som", "passo"), E(2.74f, "som", "passo"),
            });
            // ---------------------------------------------------- feliz (spare, boa jogada): pulinho + palmas
            var palmaJunta = P(PARADO, new Pose
            {
                ["braco_e"] = V(-72, 0, -12), ["antebraco_e"] = V(-48, -20, -25),
                ["braco_d"] = V(-72, 0, 12), ["antebraco_d"] = V(-48, 20, 25), ["corpo"] = V(4, 0, 0),
            });
            var palmaAbre = P(palmaJunta, new Pose { ["braco_e"] = V(-70, 0, 14), ["braco_d"] = V(-70, 0, -14) });
            Novo("feliz", 1.9f, false, new[]
            {
                (0f, P(PARADO)),
                (0.18f, P(agacha, new Pose { ["altura"] = S(-0.07f) })),
                (0.39f, P(PARADO, new Pose
                {
                    ["braco_e"] = V(-130, 0, 45), ["braco_d"] = V(-130, 0, -45), ["coxa_e"] = V(-20, 0, 0),
                    ["canela_e"] = V(45, 0, 0), ["coxa_d"] = V(-20, 0, 0), ["canela_d"] = V(45, 0, 0),
                })),
                (0.62f, P(PARADO, new Pose { ["altura"] = S(-0.05f) })),
                (0.75f, palmaJunta), (0.9f, palmaAbre), (1.05f, palmaJunta), (1.2f, palmaAbre), (1.35f, palmaJunta),
                (1.9f, P(PARADO)),
            }, depois: "parado", pulos: new[] { V(0.22f, 0.56f, 0.2f) }, eventos: new[]
            {
                E(0f, "expressao", "feliz"), E(0.75f, "som", "palma"), E(1.05f, "som", "palma"), E(1.35f, "som", "palma"),
            });
            // ---------------------------------------------------- triste (errou): cabeça baixa, balança
            var baixa = P(PARADO, new Pose
            {
                ["cabeca"] = V(26, 0, 0), ["corpo"] = V(10, 0, 0), ["braco_e"] = V(6, 0, 3), ["braco_d"] = V(6, 0, -3), ["altura"] = S(-0.03f),
            });
            Novo("triste", 2f, false, new[]
            {
                (0f, P(PARADO)), (0.3f, baixa), (0.6f, P(baixa, new Pose { ["cabeca"] = V(24, -22, 0) })),
                (0.9f, P(baixa, new Pose { ["cabeca"] = V(24, 22, 0) })),
                (1.2f, P(baixa, new Pose { ["cabeca"] = V(20, 0, 0) })), (1.5f, baixa),
                (2f, P(PARADO, new Pose
                {
                    ["braco_e"] = V(-20, 0, 40), ["braco_d"] = V(-20, 0, -40), ["antebraco_e"] = V(-80, 0, 0), ["antebraco_d"] = V(-80, 0, 0),
                })),
            }, depois: "parado", eventos: new[] { E(0f, "expressao", "triste"), E(1.6f, "expressao", "concentrado") });
            // ---------------------------------------------------- torcida (o outro jogando)
            var t1 = P(PARADO, new Pose { ["braco_e"] = V(-155, 0, 28), ["antebraco_e"] = V(-30, 0, 0), ["braco_d"] = V(-110, 0, -40), ["antebraco_d"] = V(-70, 0, 0) });
            var t2 = P(PARADO, new Pose { ["braco_d"] = V(-155, 0, -28), ["antebraco_d"] = V(-30, 0, 0), ["braco_e"] = V(-110, 0, 40), ["antebraco_e"] = V(-70, 0, 0) });
            Novo("torcida", 2.4f, true, new[]
            {
                (0f, P(t1, new Pose { ["quadril"] = V(0.01f, 0, 0) })),
                (0.6f, P(t1, new Pose { ["quadril"] = V(0.012f, 0, 0), ["altura"] = S(-0.006f) })),
                (1.2f, P(t2, new Pose { ["quadril"] = V(-0.01f, 0, 0) })),
                (1.8f, P(t2, new Pose { ["quadril"] = V(-0.012f, 0, 0), ["altura"] = S(-0.006f) })),
                (2.4f, P(t1, new Pose { ["quadril"] = V(0.01f, 0, 0) })),
            });
            // festa (fim de jogo): braços para cima balançando, sem pular
            var f1 = P(PARADO, new Pose
            {
                ["braco_e"] = V(-150, 0, 38), ["antebraco_e"] = V(-15, 0, 0), ["braco_d"] = V(-150, 0, -38),
                ["antebraco_d"] = V(-15, 0, 0), ["corpo"] = V(-3, 0, -5), ["cabeca"] = V(-6, 0, 6), ["quadril"] = V(0.015f, 0, 0),
            });
            var f2 = P(f1, new Pose { ["corpo"] = V(-3, 0, 5), ["cabeca"] = V(-6, 0, -6), ["quadril"] = V(-0.015f, 0, 0) });
            Novo("festa", 2f, true, new[] { (0f, f1), (1f, f2), (2f, P(f1)) });
            // ---------------------------------------------------- dança (atração / fim)
            var d1 = P(PARADO, new Pose
            {
                ["braco_e"] = V(-110, 0, 30), ["antebraco_e"] = V(-35, 0, 0), ["braco_d"] = V(-8, 0, -30),
                ["antebraco_d"] = V(-90, 0, 0), ["corpo"] = V(2, 10, 7), ["cabeca"] = V(0, -10, -8), ["quadril"] = V(0.035f, 0, 0),
            });
            var d2 = P(PARADO, new Pose
            {
                ["braco_d"] = V(-110, 0, -30), ["antebraco_d"] = V(-35, 0, 0), ["braco_e"] = V(-8, 0, 30),
                ["antebraco_e"] = V(-90, 0, 0), ["corpo"] = V(2, -10, -7), ["cabeca"] = V(0, 10, 8), ["quadril"] = V(-0.035f, 0, 0),
            });
            var danca = new[]
            {
                (0f, d1), (0.4f, P(d1, new Pose { ["altura"] = S(-0.018f), ["quadril"] = V(0.025f, 0, 0) })), (0.7f, P(d1)),
                (0.95f, P(d1, new Pose { ["coxa_d"] = V(-22, 0, 0), ["canela_d"] = V(40, 0, 0), ["pe_d"] = V(12, 0, 0) })), (1.2f, P(d1)),
                (1.4f, d2), (1.8f, P(d2, new Pose { ["altura"] = S(-0.018f), ["quadril"] = V(-0.025f, 0, 0) })), (2.1f, P(d2)),
                (2.35f, P(d2, new Pose { ["coxa_e"] = V(-22, 0, 0), ["canela_e"] = V(40, 0, 0), ["pe_e"] = V(12, 0, 0) })), (2.6f, P(d2)),
                (2.8f, P(d1)),
            };
            Novo("danca", 2.8f, true, danca);
            // um pedacinho de dança e volta a ficar parado (atração)
            var dc = new List<(float, Pose)>(danca);
            for (int i = 1; i < danca.Length; i++) dc.Add((danca[i].Item1 + 2.8f, danca[i].Item2));
            Novo("danca_curta", 5.6f, false, dc.ToArray(), depois: "parado");
            // ---------------------------------------------------- acenar (oi!) pelo LADO da cabeça
            var a1 = P(PARADO, new Pose
            {
                ["braco_d"] = V(-150, 0, -38), ["antebraco_d"] = V(-10, 0, -30), ["cabeca"] = V(0, 0, 6),
                ["quadril"] = V(0.015f, 0, 0), ["corpo"] = V(0, 0, -3),
            });
            var a2 = P(a1, new Pose { ["antebraco_d"] = V(-10, 0, 28), ["cabeca"] = V(0, 0, -4), ["quadril"] = V(0.008f, 0, 0) });
            Novo("acenar", 1.2f, true, new[] { (0f, a1), (0.3f, a2), (0.6f, P(a1)), (0.9f, P(a2)), (1.2f, P(a1)) });
            Novo("acenar_uma", 2.6f, false, new[]
            {
                (0f, P(PARADO)), (0.35f, a1), (0.65f, a2), (0.95f, P(a1)), (1.25f, P(a2)), (1.55f, P(a1)), (1.85f, P(a2)), (2.6f, P(PARADO)),
            }, depois: "parado", eventos: new[] { E(0f, "expressao", "feliz") });
            // olha para os lados, curioso (atração)
            Novo("olhar_volta", 3.2f, false, new[]
            {
                (0f, P(PARADO)), (0.6f, P(PARADO, new Pose { ["cabeca"] = V(4, 35, 4), ["corpo"] = V(0, 8, 0) })),
                (1.5f, P(PARADO, new Pose { ["cabeca"] = V(4, 35, 4), ["corpo"] = V(0, 8, 0) })),
                (2.1f, P(PARADO, new Pose { ["cabeca"] = V(2, -30, -4), ["corpo"] = V(0, -6, 0) })),
                (2.7f, P(PARADO, new Pose { ["cabeca"] = V(2, -30, -4), ["corpo"] = V(0, -6, 0) })), (3.2f, P(PARADO)),
            }, depois: "parado");
            // ---------------------------------------------------- andar
            var w1 = P(PARADO, new Pose
            {
                ["coxa_e"] = V(-26, 0, 0), ["canela_e"] = V(8, 0, 0), ["coxa_d"] = V(22, 0, 0), ["canela_d"] = V(30, 0, 0),
                ["braco_e"] = V(25, 0, 10), ["braco_d"] = V(-25, 0, -10), ["altura"] = S(-0.015f),
            });
            var w2 = P(PARADO, new Pose
            {
                ["coxa_d"] = V(-26, 0, 0), ["canela_d"] = V(8, 0, 0), ["coxa_e"] = V(22, 0, 0), ["canela_e"] = V(30, 0, 0),
                ["braco_d"] = V(25, 0, -10), ["braco_e"] = V(-25, 0, 10), ["altura"] = S(-0.015f),
            });
            Novo("andar", 0.7f, true, new[]
            {
                (0f, w1), (0.175f, P(PARADO, new Pose { ["altura"] = S(0.02f) })), (0.35f, w2),
                (0.525f, P(PARADO, new Pose { ["altura"] = S(0.02f) })), (0.7f, P(w1)),
            }, ik: "chao");
            // ---------------------------------------------------- vitória: pulinhos de alegria, braços para cima
            var vBaixo = P(agacha, new Pose { ["altura"] = S(-0.08f), ["braco_e"] = V(-150, 0, 40), ["braco_d"] = V(-150, 0, -40) });
            var vAlto = P(voando);
            Novo("vitoria", 1.4f, false, new[]
            {
                (0f, vBaixo), (0.12f, P(esticado)), (0.35f, vAlto), (0.58f, P(esticado)), (0.7f, P(vBaixo)),
                (0.82f, P(esticado, new Pose { ["corpo"] = V(-6, 0, 8) })), (1.03f, P(vAlto, new Pose { ["corpo"] = V(-6, 0, 10) })),
                (1.24f, P(esticado)), (1.4f, P(vBaixo)),
            }, depois: "festa", pulos: new[] { V(0.12f, 0.58f, 0.3f), V(0.82f, 1.24f, 0.24f) });
            // ---------------------------------------------------- escolhido (seleção de personagem)
            Novo("escolhido", 1.4f, false, new[]
            {
                (0f, P(PARADO)), (0.17f, P(agacha, new Pose { ["altura"] = S(-0.07f) })),
                (0.41f, P(PARADO, new Pose
                {
                    ["braco_d"] = V(-165, 0, -25), ["antebraco_d"] = V(-10, 0, 0), ["braco_e"] = V(10, 0, 30),
                    ["coxa_e"] = V(-30, 0, 0), ["canela_e"] = V(55, 0, 0), ["coxa_d"] = V(-10, 0, 0), ["canela_d"] = V(25, 0, 0),
                })),
                (0.75f, P(PARADO, new Pose { ["altura"] = S(-0.03f), ["braco_d"] = V(-160, 0, -28) })),
                (1.4f, P(PARADO, new Pose { ["braco_d"] = V(-150, 0, -30), ["antebraco_d"] = V(-40, 0, 0) })),
            }, pulos: new[] { V(0.2f, 0.62f, 0.26f) }, eventos: new[] { E(0f, "expressao", "empolgado"), E(1.2f, "expressao", "piscadinha") });
            // ---------------------------------------------------- quase (ficou 1 pino): mãos nas bochechas
            Novo("quase", 1.7f, false, new[]
            {
                (0f, P(PARADO)),
                (0.3f, P(PARADO, new Pose
                {
                    ["braco_e"] = V(-110, 0, 20), ["antebraco_e"] = V(-30, 0, 0), ["braco_d"] = V(-110, 0, -20), ["antebraco_d"] = V(-30, 0, 0),
                    ["corpo"] = V(-6, 0, 0), ["cabeca"] = V(-10, 0, 0),
                })),
                (1.2f, P(PARADO, new Pose
                {
                    ["braco_e"] = V(-115, 0, 18), ["antebraco_e"] = V(-35, 0, 0), ["braco_d"] = V(-115, 0, -18), ["antebraco_d"] = V(-35, 0, 0),
                    ["corpo"] = V(-4, 0, 0), ["cabeca"] = V(-6, 0, 4),
                })),
                (1.7f, P(PARADO)),
            }, depois: "parado", eventos: new[] { E(0f, "expressao", "surpreso") });
        }

        // ------------------------------------------------------------ preparo (curvas)
        /// <summary>Um valor das edições Godot nas coordenadas da Unity.</summary>
        static Vector3 Unity(string canal, Vector3 v)
        {
            if (canal == "quadril") return new Vector3(-v.x, v.y, v.z);
            if (canal == "giro") return new Vector3(-v.x, 0, 0);
            if (ESCALARES.Contains(canal)) return v;
            return new Vector3(v.x, -v.y, -v.z);   // ângulos de junta
        }

        /// <summary>Prepara o clipe uma vez: tempos, valores de cada canal em cada chave e as
        /// tangentes da curva (PCHIP, no tempo de verdade de cada chave).</summary>
        static void Preparar(Clipe c)
        {
            int n = c.chaves.Count;
            c.tempos = new float[n];
            var canais = new List<string>();
            for (int i = 0; i < n; i++)
            {
                c.tempos[i] = c.chaves[i].t;
                foreach (var k in c.chaves[i].p.Keys)
                    if (!canais.Contains(k)) canais.Add(k);
            }
            c.canais = canais.ToArray();
            c.valores = new Vector3[canais.Count][];
            c.tangentes = new Vector3[canais.Count][];
            for (int k = 0; k < canais.Count; k++)
            {
                var vals = new Vector3[n];
                for (int i = 0; i < n; i++)
                    vals[i] = c.chaves[i].p.TryGetValue(canais[k], out var v) ? Unity(canais[k], v) : Vector3.zero;
                c.valores[k] = vals;
                c.tangentes[k] = Tangentes(vals, c.tempos, c.loop, c.dur);
            }
        }

        static Vector3[] Tangentes(Vector3[] vals, float[] tempos, bool loop, float dur)
        {
            int n = vals.Length;
            var res = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                bool temAnt = false, temProx = false;
                Vector3 ant = default, prox = default;
                float h0 = 0f, h1 = 0f;
                if (i > 0)
                {
                    ant = vals[i - 1];
                    h0 = tempos[i] - tempos[i - 1];
                    temAnt = true;
                }
                else if (loop && n > 2)
                {
                    ant = vals[n - 2];
                    h0 = tempos[i] - (tempos[n - 2] - dur);
                    temAnt = true;
                }
                if (i < n - 1)
                {
                    prox = vals[i + 1];
                    h1 = tempos[i + 1] - tempos[i];
                    temProx = true;
                }
                else if (loop && n > 2)
                {
                    prox = vals[1];
                    h1 = (tempos[1] + dur) - tempos[i];
                    temProx = true;
                }
                // começo/fim do clipe: chega e sai devagar
                if (!temAnt || !temProx) res[i] = Vector3.zero;
                else res[i] = new Vector3(Pchip(ant.x, vals[i].x, prox.x, h0, h1), Pchip(ant.y, vals[i].y, prox.y, h0, h1),
                    Pchip(ant.z, vals[i].z, prox.z, h0, h1));
            }
            return res;
        }

        /// <summary>Tangente monotônica (Fritsch-Carlson): zero nos picos e vales (não passa do
        /// ponto) e média ponderada pelo tempo nas subidas/descidas.</summary>
        static float Pchip(float a, float b, float c, float h0, float h1)
        {
            h0 = Mathf.Max(h0, 0.0001f);
            h1 = Mathf.Max(h1, 0.0001f);
            float d0 = (b - a) / h0, d1 = (c - b) / h1;
            if (d0 * d1 <= 0f) return 0f;
            float w1 = 2f * h1 + h0, w2 = h1 + 2f * h0;
            return (w1 + w2) / (w1 / d0 + w2 / d1);
        }

        /// <summary>A pose do clipe no instante t (Hermite cúbica com as tangentes PCHIP), em
        /// coordenadas da Unity. Os pulos (arco de gravidade) entram no "voo".</summary>
        public static void Amostra(Clipe c, float t, Dictionary<string, Vector3> res)
        {
            res.Clear();
            int n = c.tempos.Length;
            if (n == 1)
            {
                for (int k = 0; k < c.canais.Length; k++) res[c.canais[k]] = c.valores[k][0];
            }
            else
            {
                int i = 0;
                while (i < n - 2 && t >= c.tempos[i + 1]) i++;
                float h = Mathf.Max(c.tempos[i + 1] - c.tempos[i], 0.0001f);
                float u = Mathf.Clamp01((t - c.tempos[i]) / h);
                float u2 = u * u, u3 = u2 * u;
                float h00 = 2f * u3 - 3f * u2 + 1f, h10 = (u3 - 2f * u2 + u) * h, h01 = -2f * u3 + 3f * u2, h11 = (u3 - u2) * h;
                for (int k = 0; k < c.canais.Length; k++)
                {
                    var v = c.valores[k];
                    var m = c.tangentes[k];
                    res[c.canais[k]] = v[i] * h00 + m[i] * h10 + v[i + 1] * h01 + m[i + 1] * h11;
                }
            }
            float pulo = Pulo(c, t);
            if (pulo > 0f)
            {
                res.TryGetValue("voo", out var voo);
                res["voo"] = new Vector3(voo.x + pulo, 0, 0);
            }
        }

        /// <summary>Altura extra dos pulos do clipe: arco de gravidade h = 4H·u·(1-u).</summary>
        static float Pulo(Clipe c, float t)
        {
            float h = 0f;
            foreach (var p in c.pulos)
                if (t > p.x && t < p.y)
                {
                    float u = (t - p.x) / (p.y - p.x);
                    h += 4f * p.z * u * (1f - u);
                }
            return h;
        }
    }
}
