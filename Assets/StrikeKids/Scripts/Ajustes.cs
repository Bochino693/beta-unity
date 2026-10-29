using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// AJUSTES DO OPERADOR, RECORDE E CONTADORES — gravados na TV Box
    /// (ajustes.json em Application.persistentDataPath). Mudados na tela do
    /// operador (botão de configuração &gt; AJUSTES DO JOGO).
    ///
    /// GRAVAR SEM PARAR O JOGO: as gravações do mesmo quadro viram uma só e o
    /// arquivo é escrito numa linha de trabalho separada (sempre na ordem
    /// certa); ao fechar o jogo, grava na hora o que estiver pendente.
    /// </summary>
    public static class Ajustes
    {
        public static readonly string[] DIFICULDADES = { "facil", "normal", "dificil" };
        public static readonly string[] QUALIDADES = { "auto", "alta", "media", "baixa" };

        /// <summary>Valores de fábrica.</summary>
        public static readonly Dictionary<string, Dictionary<string, object>> PADRAO = new Dictionary<string, Dictionary<string, object>>
        {
            ["jogo"] = new Dictionary<string, object>
            {
                ["rodadas"] = 3,              // rodadas por jogador
                ["tentativas"] = 3,           // bolas por rodada (como no Dragon Bowling)
                ["dificuldade"] = "facil",
                ["velocidade"] = 130,         // ritmo do jogo (%): 130 = tudo 30% mais rápido (a física das jogadas não muda)
                ["protecao_canaleta"] = true, // para-choques nas canaletas (bumpers)
                ["replay_strike"] = true,
                ["modo_livre"] = true,        // sem crédito: START sempre começa
                ["inicio_bola"] = true,       // modo livre: rolar a bola começa (1 bola = 1 jogador, 2 = 2; o lado escolhe Bob/Mia)
                ["creditos_por_partida"] = 1,
                ["tempo_inatividade"] = 45,   // segundos sem jogar até o aviso
                ["demo"] = true,
                ["foto"] = true,              // foto das crianças com a câmera da máquina
                ["camera_giro"] = 0,          // 0, 90, 180, 270 (câmera montada de lado)
                ["camera_espelho"] = true,    // espelha a imagem (a criança se vê como no espelho)
            },
            ["som"] = new Dictionary<string, object>
            {
                ["musica"] = 0.8,
                ["efeitos"] = 1.0,
                ["som_na_demo"] = true,
            },
            ["arduino"] = new Dictionary<string, object>
            {
                ["porta_invertida"] = false,     // a trava abre quando devia fechar? troca aqui
                ["sensores_invertidos"] = false, // sensores ligados da direita para a esquerda
                // PONTE H da porta (o jogo manda ao Arduino, que guarda na EEPROM):
                ["porta_tempo"] = 1500,          // ms que o motor anda para abrir/fechar tudo (sem fim de curso)
                ["porta_fim_de_curso"] = false,  // tem os micro-switches (A3 = aberta, A4 = fechada): anda até eles
            },
            ["video"] = new Dictionary<string, object>
            {
                ["qualidade"] = "auto",          // auto, alta, media, baixa
                ["cenario"] = "auto",            // fundo: auto (= leve, como a edição Godot da TV Box), leve, completo
                ["cenario3d"] = "auto",          // nível do cenário 3D: auto (leve nas Mali-400/450), alto, leve
                ["cenario3d_medido"] = "",       // o que o automático viu ("leve" = não segurou o alto)
                ["detalhes"] = true,             // vento nas plantas, enfeites que se mexem, borboletas, sons de ambiente
                ["nitidez"] = "auto",            // resolução do 3D: auto ou 100%/90%/80%/70%
                ["fator"] = 1.0,                 // o último fator automático bom (começa dele ao ligar)
                ["medidor"] = false,             // painel de desempenho na tela (F3 no PC)
                ["qualidade_auto"] = "",         // o que o TESTE DE DESEMPENHO recomendou
                ["testar"] = false,              // pedido do menu: rodar o teste ao voltar
            },
        };

        public static event Action Mudaram;

        static Dictionary<string, object> _dados;
        static int _creditos;
        static bool _pendente;
        static int _versao;
        static int _gravada;
        static readonly object _trava = new object();
        static string _arquivo;

        public static string Pasta
        {
            get { return Application.persistentDataPath; }
        }

        static void Garantir()
        {
            if (_dados != null) return;
            _arquivo = Path.Combine(Pasta, "ajustes.json");
            _dados = null;
            try
            {
                if (File.Exists(_arquivo)) _dados = Json.Dic(Json.Ler(File.ReadAllText(_arquivo)));
            }
            catch (Exception e)
            {
                Debug.LogWarning("ajustes: não li " + _arquivo + ": " + e.Message);
            }
            if (_dados == null) _dados = new Dictionary<string, object>();
        }

        static Dictionary<string, object> Secao(string secao, bool criar)
        {
            Garantir();
            if (_dados.TryGetValue(secao, out object s) && s is Dictionary<string, object> d) return d;
            if (!criar) return null;
            d = new Dictionary<string, object>();
            _dados[secao] = d;
            return d;
        }

        public static object Valor(string secao, string chave)
        {
            var s = Secao(secao, false);
            if (s != null && s.TryGetValue(chave, out object v)) return v;
            if (PADRAO.TryGetValue(secao, out var p) && p.TryGetValue(chave, out object vp)) return vp;
            return null;
        }

        public static int Int(string secao, string chave) => Json.Int(Valor(secao, chave));
        public static float Float(string secao, string chave) => (float)Json.Num(Valor(secao, chave));
        public static bool Bool(string secao, string chave) => Json.Bool(Valor(secao, chave));
        public static string Texto(string secao, string chave) => Json.Str(Valor(secao, chave));

        public static void Definir(string secao, string chave, object v)
        {
            Secao(secao, true)[chave] = v;
            // escolher o nível do cenário à mão esquece o que o automático mediu
            if (secao == "video" && chave == "cenario3d") Secao(secao, true)["cenario3d_medido"] = "";
            Salvar();
            Mudaram?.Invoke();
        }

        public static void RestaurarPadrao()
        {
            foreach (var s in PADRAO)
                foreach (var k in s.Value)
                    Secao(s.Key, true)[k.Key] = k.Value;
            Salvar();
            Mudaram?.Invoke();
        }

        // ------------------------------------------------------------ gravação
        static void Salvar()
        {
            if (_pendente) return;
            _pendente = true;
            Tarefas.NoFimDoQuadro(SalvarAgora);
        }

        static void SalvarAgora()
        {
            if (!_pendente) return;
            _pendente = false;
            _versao++;
            int v = _versao;
            // a cópia é tirada aqui (no quadro do jogo); a linha de trabalho só grava o texto
            string texto = Json.Escrever(_dados, true);
            string arq = _arquivo;
            ThreadPool.QueueUserWorkItem(_ => Escrever(arq, texto, v));
        }

        static void Escrever(string arq, string texto, int v)
        {
            lock (_trava)
            {
                if (v <= _gravada) return;
                try
                {
                    string tmp = arq + ".tmp";
                    File.WriteAllText(tmp, texto);
                    if (File.Exists(arq)) File.Delete(arq);
                    File.Move(tmp, arq);
                    _gravada = v;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("ajustes: não gravei: " + e.Message);
                }
            }
        }

        /// <summary>Fechando o jogo: grava na hora o que estiver pendente.</summary>
        public static void GravarJa()
        {
            if (_dados == null) return;
            if (_pendente)
            {
                _pendente = false;
                _versao++;
            }
            Escrever(_arquivo, Json.Escrever(_dados, true), _versao);
        }

        // ------------------------------------------------------------ atalhos
        public static int Rodadas() => Mathf.Clamp(Int("jogo", "rodadas"), 1, 10);

        /// <summary>Ritmo do jogo: 1,0 = tempo real; 1,3 = tudo 30% mais rápido.</summary>
        public static float Velocidade() => Mathf.Clamp(Float("jogo", "velocidade") / 100f, 0.5f, 2f);

        public static int Tentativas() => Mathf.Clamp(Int("jogo", "tentativas"), 1, 3);

        public static string Dificuldade()
        {
            string d = Texto("jogo", "dificuldade");
            return Array.IndexOf(DIFICULDADES, d) >= 0 ? d : "facil";
        }

        public static bool ProtecaoCanaleta() => Bool("jogo", "protecao_canaleta");
        public static bool ModoLivre() => Bool("jogo", "modo_livre");

        /// <summary>Cenário leve (65% das peças espalhadas, menos partículas)? É o padrão da
        /// edição Godot da TV Box (strike/video/cenario = leve): "auto" = leve.</summary>
        public static bool CenarioLeve() => Texto("video", "cenario") != "completo";

        /// <summary>Uma placa de vídeo bem fraca (Mali-400/450 e parecidas)?</summary>
        public static bool PlacaFraca()
        {
            string g = (SystemInfo.graphicsDeviceName ?? "").ToLowerInvariant();
            return g.Contains("mali-4") || g.Contains("mali-3") || g.Contains("mali-2") || g.Contains("sgx") || g.Contains("videocore")
                || g.Contains("adreno (tm) 3") || g.Contains("adreno (tm) 2");
        }

        /// <summary>O cenário 3D no nível LEVE (um passo mais leve em cada peça)? "auto": nas placas
        /// bem fracas (a Mali-450), como na edição Godot; "Detalhes extras" desligado também.</summary>
        public static bool Cenario3dLeve()
        {
            if (!Detalhes()) return true;
            string c = Texto("video", "cenario3d");
            if (c == "leve") return true;
            if (c == "alto") return false;
            // no automático: o que o governador do quadro já mediu nesta máquina (antes de
            // medir, a Mali-400/450 começa no leve; as outras, no alto)
            string medido = Texto("video", "cenario3d_medido");
            return medido == "leve" || (medido == "" && PlacaFraca());
        }

        /// <summary>Detalhes extras: vento nas plantas, enfeites perto da pista que se mexem,
        /// borboletas e os sons de ambiente das fases.</summary>
        public static bool Detalhes() => Bool("video", "detalhes");

        // ------------------------------------------------------------ créditos
        public static int Creditos => _creditos;

        public static void AdicionarCredito()
        {
            _creditos++;
            Contar("moedas");
        }

        public static bool PodeComecar() => ModoLivre() || _creditos >= Int("jogo", "creditos_por_partida");

        public static void GastarCreditos()
        {
            if (!ModoLivre()) _creditos = Mathf.Max(0, _creditos - Int("jogo", "creditos_por_partida"));
        }

        // ------------------------------------------------------------ recorde
        /// <summary>{pontos, quem, foto} do recorde de 1 ou 2 jogadores.</summary>
        public static Dictionary<string, object> Recorde(int jogadores)
        {
            var s = Secao("recorde", false);
            if (s != null && s.TryGetValue("j" + jogadores, out object v) && v is Dictionary<string, object> d) return d;
            return new Dictionary<string, object> { ["pontos"] = 0, ["quem"] = "" };
        }

        /// <summary>Grava se for recorde. Retorna true se bateu.</summary>
        public static bool RegistrarPontos(int jogadores, int pontos, string quem)
        {
            var r = Recorde(jogadores);
            if (pontos > Json.Int(Json.Pegar(r, "pontos"), 0))
            {
                Secao("recorde", true)["j" + jogadores] = new Dictionary<string, object> { ["pontos"] = pontos, ["quem"] = quem };
                Salvar();
                return true;
            }
            return false;
        }

        public static void DefinirFotoRecorde(int jogadores, string caminho)
        {
            var r = new Dictionary<string, object>(Recorde(jogadores));
            r["foto"] = caminho;
            Secao("recorde", true)["j" + jogadores] = r;
            Salvar();
        }

        public static void ZerarRecordes()
        {
            Garantir();
            _dados.Remove("recorde");
            Salvar();
            Classificacao.Zerar();
            Fotos.ApagarRecordes();
        }

        // ------------------------------------------------------------ contadores
        public static void Contar(string chave, int quanto = 1)
        {
            var s = Secao("contadores", true);
            s[chave] = Json.Int(Json.Pegar(s, chave), 0) + quanto;
            Salvar();
        }

        public static int Contador(string chave) => Json.Int(Json.Pegar(Secao("contadores", false), chave), 0);
    }
}
