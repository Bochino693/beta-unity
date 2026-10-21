using System.Collections.Generic;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// O PLANEJADOR DA JOGADA — ele só escolhe a LINHA da bola. Quem derruba os
    /// pinos é a física de verdade: a bola bate, os pinos voam, batem uns nos
    /// outros, nas paredes, e caem. Nenhum pino é empurrado "de mentira".
    ///
    /// A mira é sempre o STRIKE (o "bolso" entre os pinos 1 e 3, ou 1 e 2). O
    /// sensor diz POR ONDE a bola passou (5 posições): no centro é mais fácil
    /// acertar o bolso; nas pontas a bola chega torta. A dificuldade escolhida
    /// pelo operador define o quanto a bola "erra" o bolso.
    ///
    /// As linhas vêm de uma tabela de jogadas simuladas com a MESMA física
    /// (Editor/Calibrar.cs -&gt; Resources/Dados/tabela_jogadas.json): o jogo sorteia
    /// quão boa foi a jogada e pega uma linha que, na simulação, rendeu aquilo.
    /// Na hora, a física pode dar um pino a mais ou a menos — como no boliche.
    ///
    ///   Z = esquerda   X = meio-esquerda   C = CENTRO   V = meio-direita   B = direita
    /// (de quem joga; na Unity a esquerda de quem olha para os pinos é o X positivo)
    /// </summary>
    public class Jogada
    {
        public static readonly Dictionary<string, float> LINHA = new Dictionary<string, float>
        {
            ["Z"] = 0.43f, ["X"] = 0.21f, ["C"] = 0f, ["V"] = -0.21f, ["B"] = -0.43f,
        };
        public const string ARQUIVO = "Dados/tabela_jogadas";

        /// <summary>Chance (%) de cada resultado na 1ª bola, por posição e dificuldade. Chave = pinos derrubados.</summary>
        static readonly Dictionary<string, Dictionary<string, int[,]>> TABELA = new Dictionary<string, Dictionary<string, int[,]>>
        {
            ["facil"] = new Dictionary<string, int[,]>
            {
                ["C"] = new[,] { { 10, 60 }, { 9, 14 }, { 8, 10 }, { 7, 8 }, { 6, 5 }, { 5, 3 } },
                ["X"] = new[,] { { 10, 34 }, { 9, 17 }, { 8, 15 }, { 7, 13 }, { 6, 10 }, { 5, 7 }, { 4, 4 } },
                ["Z"] = new[,] { { 10, 14 }, { 9, 12 }, { 8, 14 }, { 7, 15 }, { 6, 14 }, { 5, 12 }, { 4, 10 }, { 3, 6 }, { 2, 3 } },
            },
            ["normal"] = new Dictionary<string, int[,]>
            {
                ["C"] = new[,] { { 10, 42 }, { 9, 18 }, { 8, 14 }, { 7, 11 }, { 6, 8 }, { 5, 5 }, { 4, 2 } },
                ["X"] = new[,] { { 10, 22 }, { 9, 16 }, { 8, 16 }, { 7, 15 }, { 6, 12 }, { 5, 10 }, { 4, 6 }, { 3, 3 } },
                ["Z"] = new[,] { { 10, 7 }, { 9, 9 }, { 8, 12 }, { 7, 14 }, { 6, 15 }, { 5, 14 }, { 4, 12 }, { 3, 9 }, { 2, 6 }, { 1, 2 } },
            },
            ["dificil"] = new Dictionary<string, int[,]>
            {
                ["C"] = new[,] { { 10, 28 }, { 9, 18 }, { 8, 16 }, { 7, 14 }, { 6, 10 }, { 5, 8 }, { 4, 6 } },
                ["X"] = new[,] { { 10, 12 }, { 9, 13 }, { 8, 15 }, { 7, 16 }, { 6, 15 }, { 5, 12 }, { 4, 9 }, { 3, 8 } },
                ["Z"] = new[,] { { 10, 4 }, { 9, 6 }, { 8, 9 }, { 7, 12 }, { 6, 14 }, { 5, 15 }, { 4, 14 }, { 3, 12 }, { 2, 9 }, { 1, 5 } },
            },
        };

        /// <summary>Chance (%) da bola acertar em cheio o que sobrou (spare), conforme a linha do
        /// sensor passa perto do grupo de pinos.</summary>
        static readonly Dictionary<string, int[]> SPARE = new Dictionary<string, int[]>
        {
            ["facil"] = new[] { 72, 45, 20 }, ["normal"] = new[] { 58, 32, 12 }, ["dificil"] = new[] { 42, 22, 7 },
        };

        /// <summary>Sem para-choques: chance (%) de canaleta na 1ª bola, por posição.</summary>
        static readonly Dictionary<string, int> CANALETA = new Dictionary<string, int> { ["C"] = 0, ["X"] = 4, ["Z"] = 12 };

        public string zona = "C";
        /// <summary>por onde a bola passa na linha de falta (z = 0,35)</summary>
        public float origemX;
        /// <summary>para onde ela vai (na altura do pino 1)</summary>
        public float alvoX;
        public float velocidade = 7.9f;
        /// <summary>gancho (força lateral)</summary>
        public float efeito;
        /// <summary>quantos pinos a simulação derrubou com essa linha (-1 = não se sabe)</summary>
        public int esperado = -1;
        public bool canaleta;

        public bool StrikeProvavel => esperado == 10;

        /// <summary>Só para testes e para a abertura: resultados desejados em sequência
        /// (quantos pinos caem). Vazia = sorteio normal.</summary>
        public static readonly List<int> Forcar = new List<int>();

        class Linha
        {
            public string zona;
            public float xt, v, e, entrada;
            public List<int> resultados = new List<int>();
            public List<int> amostra;
        }

        static Dictionary<string, List<Linha>> _grupos;

        public static Jogada Planejar(string z, List<int> emPe, bool primeiraBola, string dificuldade, bool protecao)
        {
            var j = new Jogada { zona = z };
            float linha = LINHA.TryGetValue(z, out var l) ? l : 0f;
            string grupo = z == "C" ? "C" : (z == "X" || z == "V" ? "X" : "Z");
            string dif = TABELA.ContainsKey(dificuldade) ? dificuldade : "facil";
            int desejado = -1;
            if (Forcar.Count > 0)
            {
                desejado = Mathf.Clamp(Forcar[0], 0, emPe.Count);
                Forcar.RemoveAt(0);
            }
            if (primeiraBola && emPe.Count == 10)
            {
                if (desejado < 0)
                {
                    desejado = Sortear(TABELA[dif][grupo]);
                    if (!protecao && Random.Range(0, 100) < CANALETA[grupo]) desejado = 0;
                }
                if (desejado == 0 && !protecao)
                {
                    Canaleta(j, linha);
                    return j;
                }
                if (DaTabela(j, z, desejado, protecao)) return j;
            }
            Resto(j, linha, emPe, dif, desejado, protecao);
            return j;
        }

        static int Sortear(int[,] t)
        {
            int total = 0;
            for (int i = 0; i < t.GetLength(0); i++) total += t[i, 1];
            int r = Random.Range(0, Mathf.Max(total, 1));
            for (int i = 0; i < t.GetLength(0); i++)
            {
                r -= t[i, 1];
                if (r < 0) return t[i, 0];
            }
            return 0;
        }

        static void Canaleta(Jogada j, float linha)
        {
            float lado = linha != 0f ? Mathf.Sign(linha) : (Random.value < 0.5f ? 1f : -1f);
            j.canaleta = true;
            j.esperado = 0;
            j.origemX = Mathf.Clamp(linha + lado * 0.08f, -0.5f, 0.5f);
            j.alvoX = lado * 0.95f;
            j.velocidade = Random.Range(6.8f, 7.4f);
        }

        // ------------------------------------------------------------ 1ª bola
        /// <summary>Esquece a tabela (a calibração acabou de gravar outra).</summary>
        public static void Recarregar() => _grupos = null;

        static void Carregar()
        {
            if (_grupos != null) return;
            _grupos = new Dictionary<string, List<Linha>>();
            var ta = Resources.Load<TextAsset>(ARQUIVO);
            if (ta == null)
            {
                Debug.LogWarning("Jogada: sem tabela de calibração (" + ARQUIVO + ")");
                return;
            }
            var dados = Json.Dic(Json.Ler(ta.text));
            Resources.UnloadAsset(ta);
            if (dados == null) return;
            var idx = new Dictionary<string, Linha>();
            foreach (var o in Json.Lst(Json.Pegar(dados, "jogadas")) ?? new List<object>())
            {
                var r = Json.Lst(o);
                if (r == null || r.Count < 6) continue;
                // [zona, x0, xt, v, efeito, n, caidos, x_entrada]
                string zona = Json.Str(r[0]);
                float xt = (float)Json.Num(r[2]), v = (float)Json.Num(r[3]), e = (float)Json.Num(r[4]);
                string chave = Chave(zona, xt, v, e);
                if (!idx.TryGetValue(chave, out var g))
                {
                    g = new Linha { zona = zona, xt = xt, v = v, e = e, entrada = r.Count > 7 ? (float)Json.Num(r[7]) : xt };
                    idx[chave] = g;
                    if (!_grupos.ContainsKey(zona)) _grupos[zona] = new List<Linha>();
                    _grupos[zona].Add(g);
                }
                g.resultados.Add(Json.Int(r[5]));
            }
            // a física é caótica: um tiquinho de diferença muda o resultado. Por isso cada
            // linha olha também as vizinhas (2 cm para cada lado): só conta como "linha de
            // strike" a que derruba tudo mesmo errando um pouquinho.
            foreach (var g in idx.Values)
            {
                g.amostra = new List<int>(g.resultados);
                foreach (float dx in new[] { -0.02f, 0.02f })
                    if (idx.TryGetValue(Chave(g.zona, g.xt + dx, g.v, g.e), out var viz)) g.amostra.AddRange(viz.resultados);
            }
        }

        static string Chave(string z, float xt, float v, float e) =>
            z + "|" + (Mathf.Round(xt * 100f) / 100f).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "|" +
            v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "|" +
            e.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Pega na tabela uma linha da zona que costuma render `n` pinos (a mais
        /// "garantida", contando as vizinhas).</summary>
        static bool DaTabela(Jogada j, string z, int n, bool protecao)
        {
            Carregar();
            if (!_grupos.TryGetValue(z, out var lista) || lista.Count == 0) return false;
            float melhor = -1f;
            var opcoes = new List<Linha>();
            foreach (var g in lista)
            {
                // sem para-choques: essa linha encostava neles
                if (!protecao && (Mathf.Abs(g.entrada) > 0.5f || Mathf.Abs(g.xt) > 0.5f)) continue;
                float nota = 0f;
                foreach (int r in g.amostra)
                {
                    int d = Mathf.Abs(r - n);
                    nota += d == 0 ? 1f : (d == 1 ? 0.35f : 0f);
                }
                nota /= g.amostra.Count;
                if (nota > melhor + 0.001f)
                {
                    melhor = nota;
                    opcoes.Clear();
                    opcoes.Add(g);
                }
                else if (Mathf.Abs(nota - melhor) <= 0.001f) opcoes.Add(g);
            }
            if (opcoes.Count == 0) return false;
            var esc = opcoes[Random.Range(0, opcoes.Count)];
            j.origemX = LINHA[z];
            j.alvoX = esc.xt;
            j.velocidade = esc.v;
            j.efeito = esc.e;
            j.esperado = n;
            return true;
        }

        // ------------------------------------------------------------ bolas seguintes
        /// <summary>Mira no que sobrou: o pino da frente do grupo, batendo do lado que joga
        /// ele em cima dos outros. Errou a mira? A bola passa raspando ou de lado.</summary>
        static void Resto(Jogada j, float linha, List<int> emPe, string dif, int desejado, bool protecao)
        {
            j.origemX = Mathf.Clamp(linha, -0.45f, 0.45f);
            j.velocidade = Random.Range(8.0f, 8.8f);
            j.efeito = 0f;
            if (emPe.Count == 0)
            {
                j.alvoX = 0f;
                return;
            }
            // centro do grupo e o pino "chave" (o mais da frente; empate: o mais central)
            float cx = 0f, xmin = 9f, xmax = -9f;
            int chave = emPe[0];
            foreach (int n in emPe)
            {
                var p = Pinos.PosicaoDoPino(n);
                cx += p.x;
                xmin = Mathf.Min(xmin, p.x);
                xmax = Mathf.Max(xmax, p.x);
                var pc = Pinos.PosicaoDoPino(chave);
                if (p.z > pc.z + 0.01f || (Mathf.Abs(p.z - pc.z) < 0.01f && Mathf.Abs(p.x) < Mathf.Abs(pc.x))) chave = n;
            }
            cx /= emPe.Count;
            var pk = Pinos.PosicaoDoPino(chave);
            float lado = Mathf.Sign(cx - pk.x);
            float mira = Mathf.Abs(cx - pk.x) > 0.03f ? pk.x - lado * 0.07f : pk.x + Random.Range(-0.03f, 0.03f);
            float dist = 0f;
            if (linha < xmin) dist = xmin - linha;
            else if (linha > xmax) dist = linha - xmax;
            var chances = SPARE[dif];
            int chance = dist < 0.12f ? chances[0] : (dist < 0.32f ? chances[1] : chances[2]);
            bool acertou = Random.Range(0, 100) < chance;
            if (desejado >= 0) acertou = desejado >= emPe.Count;
            if (acertou)
            {
                j.alvoX = mira + Random.Range(-0.015f, 0.015f);
                j.esperado = emPe.Count;
            }
            else
            {
                // erra para o lado de fora do grupo (raspando ou passando direto)
                float fora = mira != 0f ? Mathf.Sign(mira) : (Random.value < 0.5f ? 1f : -1f);
                float erro = Random.Range(0.1f, 0.32f);
                if (desejado == 0) erro = 0.45f;
                j.alvoX = mira + fora * erro;
                j.esperado = -1;
            }
            float lim = protecao ? 0.58f : 0.62f;
            j.alvoX = Mathf.Clamp(j.alvoX, -lim, lim);
        }

        // ------------------------------------------------------------ a linha no mundo
        /// <summary>A bola segue a linha planejada (a mesma da calibração): passa por origemX na
        /// linha de falta (z = 0,35) e vai até alvoX nos pinos. z0 = de onde ela sai.</summary>
        public void Lancamento(float z0, out Vector3 origem, out Vector3 vel)
        {
            Lancamento(origemX, alvoX, velocidade, z0, out origem, out vel);
        }

        public static void Lancamento(float x0, float xt, float v, float z0, out Vector3 origem, out Vector3 vel)
        {
            float u = (0.35f - z0) / (0.35f - Medidas.Z_PINO1);
            origem = new Vector3(Mathf.Lerp(x0, xt, u), Bola.RAIO + 0.04f, z0);
            var alvo = new Vector3(xt, Bola.RAIO, Medidas.Z_PINO1);
            var dir = alvo - origem;
            dir.y = 0f;
            vel = dir.normalized * v;
        }
    }
}
