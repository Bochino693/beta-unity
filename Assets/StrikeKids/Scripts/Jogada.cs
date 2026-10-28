using System.Collections.Generic;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// O PLANEJADOR DA JOGADA — ele só escolhe a LINHA da bola. Quem derruba os
    /// pinos é a física de verdade: a bola bate, os pinos voam, batem uns nos
    /// outros, nas paredes, e caem. Nenhum pino é empurrado "de mentira".
    ///
    /// A BOLA VAI PARA ONDE A CRIANÇA JOGOU (6.5, como na edição Godot): o sensor
    /// diz por onde a bola passou e ela segue por ali até os pinos —
    ///   Z (A) = canto esquerdo: pega o 4, o 7 e o 8 (2 ou 3 pinos)
    ///   X (S) = lado esquerdo: o 2, o 4, o 5, o 7, o 8... (uns 6 ou 7)
    ///   C (D) = CENTRO: o "bolso" entre o 1 e o 3 (ou o 1 e o 2) — o strike
    ///   V (F) e B (G) = o mesmo do lado direito.
    /// A dificuldade escolhida pelo operador define o quanto a bola se espalha em
    /// volta da linha (e, sem para-choques, a chance de ir para a canaleta pelos
    /// cantos). No centro as linhas vêm de uma tabela de jogadas simuladas com a
    /// MESMA física (Editor/Calibrar.cs -&gt; Resources/Dados/tabela_jogadas.json): o
    /// jogo sorteia quão boa foi a jogada e pega uma linha do bolso que, na
    /// simulação, rendeu aquilo.
    ///
    /// (de quem joga; na Unity a esquerda de quem olha para os pinos é o X positivo:
    /// as contas de mira são feitas nas coordenadas do Godot, com o X trocado)
    /// </summary>
    public class Jogada
    {
        public static readonly Dictionary<string, float> LINHA = new Dictionary<string, float>
        {
            ["Z"] = 0.43f, ["X"] = 0.21f, ["C"] = 0f, ["V"] = -0.21f, ["B"] = -0.43f,
        };
        /// <summary>por onde ela passa na fileira do pino 1 (fora do centro), no Godot: o canto pega
        /// 4-7-8 (com para-choques ela não passa de 0,52 — encosta neles); o lado, 2-4-5-7-8</summary>
        static readonly Dictionary<string, float> MIRA_G = new Dictionary<string, float>
        {
            ["Z"] = -0.5f, ["X"] = -0.28f, ["V"] = 0.28f, ["B"] = 0.5f,
        };
        /// <summary>quanto a linha se espalha (metros, para cada lado) por dificuldade</summary>
        static readonly Dictionary<string, float> ESPALHA = new Dictionary<string, float>
        {
            ["facil"] = 0.03f, ["normal"] = 0.05f, ["dificil"] = 0.08f,
        };
        public const string ARQUIVO = "Dados/tabela_jogadas";

        /// <summary>Chance (%) de cada resultado na 1ª bola NO CENTRO, por dificuldade. Chave = pinos derrubados.</summary>
        static readonly Dictionary<string, int[,]> TABELA = new Dictionary<string, int[,]>
        {
            ["facil"] = new[,] { { 10, 60 }, { 9, 14 }, { 8, 10 }, { 7, 8 }, { 6, 5 }, { 5, 3 } },
            ["normal"] = new[,] { { 10, 42 }, { 9, 18 }, { 8, 14 }, { 7, 11 }, { 6, 8 }, { 5, 5 }, { 4, 2 } },
            ["dificil"] = new[,] { { 10, 28 }, { 9, 18 }, { 8, 16 }, { 7, 14 }, { 6, 10 }, { 5, 8 }, { 4, 6 } },
        };

        /// <summary>Sem para-choques: chance (%) de canaleta, por dificuldade e posição (cantos e
        /// lados; no centro, nunca).</summary>
        static readonly Dictionary<string, (int canto, int lado)> CANALETA = new Dictionary<string, (int, int)>
        {
            ["facil"] = (10, 0), ["normal"] = (20, 3), ["dificil"] = (30, 6),
        };

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
            string dif = TABELA.ContainsKey(dificuldade) ? dificuldade : "facil";
            int desejado = -1;
            if (Forcar.Count > 0)
            {
                desejado = Mathf.Clamp(Forcar[0], 0, emPe.Count);
                Forcar.RemoveAt(0);
            }
            if (primeiraBola && emPe.Count == 10)
            {
                // resultado pedido (testes, abertura): a linha da tabela
                if (desejado >= 0)
                {
                    if (desejado == 0 && !protecao)
                    {
                        Canaleta(j, linha);
                        return j;
                    }
                    if (DaTabela(j, z, desejado, protecao)) return j;
                }
                else if (z == "C")
                {
                    if (DaTabela(j, z, Sortear(TABELA[dif]), protecao)) return j;
                }
                else
                {
                    var c = CANALETA[dif];
                    int chance = z == "Z" || z == "B" ? c.canto : c.lado;
                    if (!protecao && Random.Range(0, 100) < chance)
                    {
                        Canaleta(j, linha);
                        return j;
                    }
                    NaLinha(j, z, dif, protecao);
                    return j;
                }
            }
            Resto(j, z, emPe, dif, desejado, protecao);
            return j;
        }

        /// <summary>A bola segue a linha da posição do sensor (espalhando um pouco).</summary>
        static void NaLinha(Jogada j, string z, string dif, bool protecao)
        {
            float e = ESPALHA[dif];
            j.origemX = LINHA[z];
            float miraG = MIRA_G.TryGetValue(z, out var m) ? m : 0f;
            j.alvoX = -Limite(miraG + Random.Range(-e, e), protecao);
            j.velocidade = Random.Range(7.4f, 8.6f);
            j.efeito = 0f;
            j.esperado = -1;
        }

        static float Limite(float x, bool protecao) => Mathf.Clamp(x, protecao ? -0.58f : -0.62f, protecao ? 0.58f : 0.62f);

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
        /// <summary>A bola (raio 0,14) pega o pino que passa a menos de 0,22 m do centro dela. Com
        /// 2 cm de folga, mirando no MEIO, ela pega de uma vez todos os pinos que cabem numa faixa
        /// de 2 x ALCANCE (um pino só, 3-6, 1-3-6, 3-6-10, 2-4-7...).</summary>
        const float ALCANCE = 0.2f;
        /// <summary>As bolas seguintes também vão para onde a criança jogou. Uma ajudinha: se tem
        /// pino em pé perto da linha (até AJUDA), a bola passa pelo MEIO da faixa que pega mais
        /// deles — senão ela segue a linha e passa direto (tem que mirar).</summary>
        const float AJUDA = 0.15f;

        static void Resto(Jogada j, string z, List<int> emPe, string dif, int desejado, bool protecao)
        {
            // (as contas no Godot: x ao contrário; a faixa cresce para o +x de lá)
            float linhaG = -(LINHA.TryGetValue(z, out var l) ? l : 0f);
            float naMira = MIRA_G.TryGetValue(z, out var m) ? m : 0f;
            float origemG = Mathf.Clamp(linhaG, -0.45f, 0.45f);
            j.origemX = -origemG;
            j.velocidade = Random.Range(8.0f, 8.8f);
            j.efeito = 0f;
            if (emPe.Count == 0)
            {
                j.alvoX = -naMira;
                return;
            }
            // o pino "chave" (o mais da frente; empate: o mais central)
            int chave = emPe[0];
            foreach (int n in emPe)
            {
                var p = Pinos.PosicaoDoPino(n);
                var pc = Pinos.PosicaoDoPino(chave);
                if (p.z > pc.z + 0.01f || (Mathf.Abs(p.z - pc.z) < 0.01f && Mathf.Abs(p.x) < Mathf.Abs(pc.x))) chave = n;
            }
            // cada pino na conta da mira: a bola vem em linha reta da origem (z = 0,35) até a
            // mira (no pino 1) e segue torta assim até as fileiras de trás
            float vai = 0.35f - Medidas.Z_PINO1;
            var ax = new Dictionary<int, float>();
            foreach (int n in emPe)
            {
                var p = Pinos.PosicaoDoPino(n);
                float d = Medidas.Z_PINO1 - p.z;
                ax[n] = (-p.x + origemG * d / vai) / (1f + d / vai);
            }
            // a faixa que pega mais pinos — só as que passam perto da linha (ou qualquer uma,
            // quando o resultado foi pedido: testes)
            float mira = naMira, notaMelhor = -1f, largura = 0f;
            int cabem = 0;
            foreach (int a in emPe)
            {
                float x0 = ax[a];
                float lo = 9f, hi = -9f, nota = 0f;
                int dentro = 0;
                foreach (int n in emPe)
                {
                    float x = ax[n];
                    if (x >= x0 - 0.001f && x <= x0 + 2f * ALCANCE)
                    {
                        dentro++;
                        lo = Mathf.Min(lo, x);
                        hi = Mathf.Max(hi, x);
                        nota += n == chave ? 1.5f : 1f;
                    }
                }
                float meio = (lo + hi) * 0.5f;
                if (desejado < 0 && Mathf.Abs(meio - naMira) > AJUDA + (hi - lo) * 0.5f) continue;
                if (nota > notaMelhor)
                {
                    notaMelhor = nota;
                    mira = meio;
                    cabem = dentro;
                    largura = hi - lo;
                }
            }
            // um pino só (ou um atrás do outro): em cheio, um pouquinho de cada lado
            if (cabem > 0 && largura < 0.02f) mira += Random.Range(-0.03f, 0.03f);
            float e = ESPALHA[dif];
            if (desejado >= 0)
            {
                if (desejado >= emPe.Count)
                {
                    j.alvoX = -Limite(mira + Random.Range(-0.015f, 0.015f), protecao);
                    j.esperado = cabem;
                }
                else
                {
                    // erra para o lado de fora do grupo (raspando ou passando direto)
                    float fora = mira != 0f ? Mathf.Sign(mira) : (Random.value < 0.5f ? 1f : -1f);
                    j.alvoX = -Limite(mira + fora * (desejado == 0 ? 0.45f : Random.Range(0.1f, 0.32f)), protecao);
                    j.esperado = -1;
                }
                return;
            }
            j.alvoX = -Limite(mira + Random.Range(-e, e), protecao);
            j.esperado = cabem > 0 ? cabem : -1;
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
