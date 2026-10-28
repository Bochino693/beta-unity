using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace StrikeKids
{
    /// <summary>
    /// O ARDUINO DA MÁQUINA (firmware em strike-kids-gles2/arduino/strike_kids),
    /// pelo cabo USB da TV Box (StrikeSerial.java, por UsbSerial.cs). A máquina
    /// NÃO tem placa Zero Delay: TODO comando físico vem daqui —
    ///   * os 5 SENSORES da pista (S1 = esquerda ... S5 = direita) viram as
    ///     jogadas Z X C V B (Controles.Injetar) e o evento Sensor;
    ///   * START, FICHA (crédito) e o botão de CONFIGURAÇÃO (menu do operador);
    ///   * a PORTA das bolas (ponte H, servo ou relé): fechada na espera, ABRE
    ///     depois do START, FECHA no fim da partida (e sozinha, no Arduino, se o
    ///     jogo sumir);
    ///   * a fita de LED (MENU, STRIKE, SPARE...).
    ///
    /// Protocolo (linhas de texto, 9600):
    ///   Arduino -> jogo: OLA:STRIKE_KIDS:2, PONG:STRIKE_KIDS:2, S:1..S:5, CFG,
    ///     START, FICHA, SENS:00100 (ao vivo, no teste), PORTA:ABERTA/FECHADA
    ///     (com a ponte H, quando a porta chega), CAL:OK:HHHHH, INFO:...,
    ///     AVISO:SEM_JOGO, AVISO:PORTA_TRAVOU (o fim de curso não chegou)
    ///   (o firmware 1, sem a ponte H, também conecta: o protocolo é o mesmo)
    ///   jogo -> Arduino: PING, PORTA:ABRE, PORTA:FECHA, PORTA?, SENS?, TESTE:1,
    ///     TESTE:0, CALIBRA, INFO?, MENU, IDLE, STRIKE...
    /// O firmware antigo só de LEDs (DRAGON_BOWLING) também conecta (só LEDs).
    ///
    /// No PC não tem Arduino: o teclado faz tudo (A S D F G = sensores, 1 =
    /// START, 5 = ficha, 9 = configuração — Controles.cs).
    ///
    /// PERMISSÃO USB SÓ NA PRIMEIRA VEZ: a janela do Android ("Permitir que o
    /// Strike Kids acesse o USB?") é pedida UMA vez por Arduino (guardado em
    /// permissoes.json), na tela de espera, com o aviso na tela (JanelaAndroid)
    /// para marcar "Sempre abrir" (Usar por padrão). Marcado, o Android dá a
    /// permissão sozinho a cada boot e nada mais aparece. Depois disso o jogo
    /// nunca mais pede sozinho: só o operador, nos testes do Arduino no menu
    /// (PedirPermissaoAgora).
    ///
    /// Todo o estado é estático (vale chamar antes do Criar: a porta e o LED
    /// pedidos seguem quando o Arduino conectar); o objeto só anda o Update.
    /// </summary>
    public class ArduinoBridge : MonoBehaviour
    {
        public static ArduinoBridge I { get; private set; }

        public static event Action Mudou;
        /// <summary>Sensor 1..5 acionado (os sensores também viram as ações Z X C V B em Controles).</summary>
        public static event Action<int> Sensor;
        /// <summary>Aviso na tela antes da janela de permissão do Android ("" = some).</summary>
        public static event Action<string> JanelaAndroid;

        const string STATUS_PC = "Arduino: no PC o teclado faz tudo (A S D F G = sensores, 1 = START, 9 = configuração)";

#if UNITY_ANDROID && !UNITY_EDITOR
        public static string Status = "Iniciando USB...";
#else
        public static string Status = STATUS_PC;
#endif
        public static bool Conectado;
        /// <summary>Resposta do CALIBRA ("CAL:OK:HHHHH"; "" = esperando).</summary>
        public static string Calibracao = "";
        /// <summary>Estado ao vivo dos sensores (só no teste): "00100", 1 = bola no sensor.</summary>
        public static string Sensores = "";
        /// <summary>O que o Arduino confirmou (já no jeito do jogo, com a "porta invertida").</summary>
        public static bool PortaAberta;
        /// <summary>O que o jogo pediu.</summary>
        public static bool PortaQuer;
        /// <summary>O último AVISO: do Arduino (PORTA_TRAVOU, SEM_JOGO); a porta chegou = "".</summary>
        public static string Aviso = "";

        /// <summary>STRIKE_KIDS:1 (ou DRAGON_BOWLING:1, o antigo só de LEDs); "" = desconectado.</summary>
        public static string Firmware { get; private set; } = "";
        /// <summary>Chip USB do Arduino: CH340, USB original, FTDI, CP2102.</summary>
        public static string Chip { get; private set; } = "";
        /// <summary>Resposta do INFO? (pinos, LEDs, repouso dos sensores).</summary>
        public static string Info { get; private set; } = "";
        /// <summary>Quantas vezes cada sensor (S1..S5) acionou desde o ModoTeste(true).</summary>
        public static readonly int[] Contagem = new int[5];

        const int BAUD = 9600;
        const long PING_MS = 2000;
        const long SEM_RESPOSTA_MS = 6500;
        const long APRESENTAR_MS = 6000;      // abriu: espera o OLA/PONG (o Uno reinicia ~2 s)
        const long ABRIR_MAX_MS = 8000;
        const long PROCURAR_MS = 1500;
        const long FALHOU_MS = 15000;         // aparelho que falhou: não tenta de novo logo
        const long JANELA_MAX_MS = 25000;
        const long ESPERA_JANELA_MS = 600;    // o aviso aparece antes da janela (que congela a imagem)
        const string DICA_USB = "NA JANELA DO ANDROID: MARQUE \"SEMPRE ABRIR\" (USAR POR PADRÃO) E TOQUE EM OK\nassim ela nunca mais aparece";
        const string ARQUIVO_PERMISSOES = "permissoes.json";
        static readonly string[] LED_COMMANDS = { "MENU", "BLUE", "RED", "IDLE", "NORMAL", "STRIKE", "SPARE", "MISS", "OFF" };
        /// <summary>S1..S5, da esquerda para a direita.</summary>
        static readonly Acao[] ACOES_SENSOR = { Acao.Z, Acao.X, Acao.C, Acao.V, Acao.B };
        static readonly Dictionary<string, string> NOMES_CHIP = new Dictionary<string, string>
        {
            ["ch34x"] = "CH340", ["cdc"] = "USB original", ["ftdi"] = "FTDI", ["cp210x"] = "CP2102",
        };

        enum Fase { Procurando, Abrindo, Apresentando, Conectado }

        static Fase _fase = Fase.Procurando;
        static string _device = "";
        static readonly Dictionary<string, long> _falhou = new Dictionary<string, long>();
        static long _desde;
        static long _ultimoRx;
        static long _ultimoPing;
        static long _ultimaProcura = -PROCURAR_MS;
        static bool _janelasOk;               // na espera ou no menu: pode abrir janela do Android
        static bool _pedirAgora;              // o operador pediu a janela (testes do Arduino no menu)
        static long _janelaAte;               // a janela do Android está aberta (até este ms)
        static long _abrirJanelaEm = -1;      // a janela abre neste ms (depois do aviso)
        static string _janelaDe = "";
        static bool _teste;
        static string _ledState = "MENU";
        static string _lastLedSent = "";
        static Dictionary<string, object> _pedidosSalvos;
        static long _ultimoErro = -100000;
        static readonly Stopwatch _relogio = Stopwatch.StartNew();
#if UNITY_ANDROID && !UNITY_EDITOR
        static UsbSerial _serial;
        static bool _tentouPlugin;
#endif

        public static void Criar()
        {
            if (I != null) return;
            var go = new GameObject("ArduinoBridge");
            DontDestroyOnLoad(go);
            I = go.AddComponent<ArduinoBridge>();
        }

        void Awake()
        {
            if (I != null && I != this)
            {
                Destroy(gameObject);
                return;
            }
            I = this;
            try
            {
                IniciarPlugin();
            }
            catch (Exception e)
            {
                Falhou(e);
            }
        }

        static void IniciarPlugin()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_tentouPlugin) return;
            _tentouPlugin = true;
            _serial = UsbSerial.Criar();
            if (_serial == null)
            {
                DefinirStatus("Arduino: este APK não tem a ponte USB (StrikeSerial.java em Assets/Plugins/Android)");
                return;
            }
            _serial.Iniciar();
            _ultimaProcura = Agora() - PROCURAR_MS;
#else
            DefinirStatus(STATUS_PC);
#endif
        }

        static long Agora() => _relogio.ElapsedMilliseconds;

        public static bool TemPlugin()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return _serial != null;
#else
            return false;
#endif
        }

        /// <summary>Firmware novo (sensores, botão e porta) conectado?</summary>
        public static bool Completo() => Conectado && Firmware.StartsWith("STRIKE_KIDS", StringComparison.Ordinal);

        // ================================================================ quadro a quadro
        void Update()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_serial == null) return;
            try
            {
                Passo(Agora());
            }
            catch (Exception e)
            {
                Falhou(e);
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static void Passo(long agora)
        {
            if (_janelaAte > 0 && agora >= _janelaAte) FechouJanela();
            if (_abrirJanelaEm >= 0 && agora >= _abrirJanelaEm)
            {
                _abrirJanelaEm = -1;
                if (_serial != null && _janelaDe != "") _serial.PedirPermissao(_janelaDe);
            }
            switch (_fase)
            {
                case Fase.Procurando:
                    if (agora - _ultimaProcura >= PROCURAR_MS)
                    {
                        _ultimaProcura = agora;
                        Procurar(agora);
                    }
                    break;
                case Fase.Abrindo:
                    {
                        int e = _serial.Estado();
                        if (e == UsbSerial.ABERTA)
                        {
                            _fase = Fase.Apresentando;
                            _desde = agora;
                            _ultimoPing = agora - 1000;
                            DefinirStatus("Arduino: conversando com " + Chip + "...");
                        }
                        else if (e == UsbSerial.ERRO || e == UsbSerial.FECHADA || agora - _desde > ABRIR_MAX_MS)
                        {
                            Falha(e == UsbSerial.ERRO ? _serial.Erro() : "não abriu");
                        }
                        break;
                    }
                case Fase.Apresentando:
                case Fase.Conectado:
                    {
                        if (_serial.Estado() != UsbSerial.ABERTA)
                        {
                            Perdeu(_serial.Erro());
                            return;
                        }
                        if (_serial.TemLinhas()) ReceberTexto(_serial.Ler(), agora);
                        if (_fase == Fase.Apresentando)
                        {
                            if (agora - _ultimoPing >= 500)
                            {
                                _ultimoPing = agora;
                                Enviar("PING");
                            }
                            if (agora - _desde > APRESENTAR_MS) Falha("o aparelho não respondeu como Arduino do Strike Kids");
                        }
                        else if (_fase == Fase.Conectado)
                        {
                            if (agora - _ultimoRx > SEM_RESPOSTA_MS) Perdeu("o Arduino parou de responder");
                            else if (agora - _ultimoPing >= PING_MS)
                            {
                                _ultimoPing = agora;
                                Enviar("PING");
                            }
                        }
                        break;
                    }
            }
        }

        /// <summary>Uma linha do USB por aparelho serial: "nome|VID:PID|tipo|permissão|produto".</summary>
        static void Procurar(long agora)
        {
            // (a lista vem pronta do Java e é refeita em segundo plano: o jogo nunca espera o USB)
            bool pronta = _serial.Listada();
            string lista = _serial.Listar();
            if (!pronta)
            {
                // a primeira lista ainda está sendo feita: pergunta de novo logo
                _ultimaProcura = agora - PROCURAR_MS + 250;
                return;
            }
            string[] comPermissao = null, semPermissao = null;
            string chaveSem = "", tipoCom = "";
            bool algum = false;
            foreach (string bruta in lista.Split('\n'))
            {
                string linha = bruta.Trim();
                if (linha.Length == 0) continue;
                algum = true;
                string[] p = linha.Split('|');
                if (p.Length < 4) continue;
                if (_falhou.TryGetValue(p[0], out long quando) && agora - quando < FALHOU_MS) continue;
                if (p[3] == "1")
                {
                    if (comPermissao == null)
                    {
                        comPermissao = p;
                        tipoCom = NOMES_CHIP.TryGetValue(p[2], out string nome) ? nome : p[2];
                    }
                }
                else if (semPermissao == null)
                {
                    semPermissao = p;
                    chaveSem = p[1];
                }
            }
            if (comPermissao != null)
            {
                _device = comPermissao[0];
                Chip = tipoCom;
                _fase = Fase.Abrindo;
                _desde = agora;
                DefinirStatus("Arduino: abrindo o USB (" + Chip + ")...");
                _serial.Abrir(_device, BAUD);
                return;
            }
            if (semPermissao != null)
            {
                if (JaPediu(chaveSem) && !_pedirAgora && !JanelaAberta())
                    DefinirStatus("Arduino no USB sem permissão: no menu, TESTE DOS SENSORES pede de novo (marque \"Sempre abrir\")");
                else
                    DefinirStatus("Arduino no USB: aceite a janela do Android (marque \"Sempre abrir\" / \"Usar por padrão\")");
                PedirPermissao(semPermissao[0], chaveSem, agora);
                return;
            }
            if (!algum)
            {
                DefinirStatus("Arduino não encontrado: confira o cabo USB");
                // nada no USB: procura com menos pressa
                _ultimaProcura = agora + 1500;
            }
        }

        /// <summary>
        /// A janela de permissão USB do Android: UMA vez por Arduino, sozinha, na
        /// tela de espera; depois, só quando o operador pede (menu).
        /// </summary>
        static void PedirPermissao(string nome, string chave, long agora)
        {
            if (_serial.PermissaoPendente() || JanelaAberta() || Fotos.JanelaAberta()) return;   // uma janela do Android por vez
            if (_pedirAgora) _pedirAgora = false;
            else if (!_janelasOk || JaPediu(chave)) return;
            MarcarPedido(chave);
            _janelaAte = agora + JANELA_MAX_MS;
            // a janela do Android congela a imagem do jogo: o aviso entra antes
            Avisar(DICA_USB);
            _janelaDe = nome;
            _abrirJanelaEm = agora + ESPERA_JANELA_MS;
        }

        static void ReceberTexto(string texto, long agora)
        {
            if (string.IsNullOrEmpty(texto)) return;
            foreach (string linha in texto.Split('\n'))
            {
                Receber(linha.Trim(), agora);
                if (_fase != Fase.Apresentando && _fase != Fase.Conectado) return;   // (caiu no meio)
            }
        }
#endif

        // ================================================================ janela do Android
        /// <summary>O operador pediu (testes do Arduino no menu): a janela pode abrir de novo.</summary>
        public static void PedirPermissaoAgora()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_serial == null || Conectado) return;
            _pedirAgora = true;
            _ultimaProcura = Agora() - PROCURAR_MS;
#endif
        }

        public static bool JanelaAberta() => _janelaAte > 0 && Agora() < _janelaAte;

        static void FechouJanela()
        {
            if (_janelaAte > 0)
            {
                _janelaAte = 0;
                Avisar("");
            }
        }

        /// <summary>A tela de espera (atração) e o menu do operador chamam isto: ali pode
        /// aparecer a janela do Android (só a primeira, que nunca foi pedida).</summary>
        public static void NaEspera(bool sim = true)
        {
            _janelasOk = sim;
        }

        void OnApplicationFocus(bool foco)
        {
            // o jogo voltou para a frente: a janela do Android fechou
            if (foco) FechouJanela();
        }

        void OnApplicationPause(bool pausa)
        {
            if (pausa) return;
            FechouJanela();
            // (pausado o jogo não conversa: a janela do Android não derruba o Arduino)
            long agora = Agora();
            if (_fase == Fase.Conectado)
            {
                _ultimoRx = agora;
                _ultimoPing = agora - PING_MS;
            }
            else if (_fase == Fase.Abrindo || _fase == Fase.Apresentando)
            {
                _desde = agora;
            }
        }

        void OnApplicationQuit()
        {
            FecharPorta();
        }

        void OnDestroy()
        {
            if (I != this) return;
            I = null;
            FecharPorta();
        }

        static void FecharPorta()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (_serial != null) _serial.Fechar();
            }
            catch (Exception e)
            {
                Falhou(e);
            }
#endif
        }

        // ---------------------------------------------------------------- permissões já pedidas
        static string ArquivoPermissoes => Path.Combine(Ajustes.Pasta, ARQUIVO_PERMISSOES);

        static Dictionary<string, object> UsbPedidos()
        {
            if (_pedidosSalvos == null)
            {
                try
                {
                    if (File.Exists(ArquivoPermissoes)) _pedidosSalvos = Json.Dic(Json.Ler(File.ReadAllText(ArquivoPermissoes)));
                }
                catch (Exception e)
                {
                    Debug.LogWarning("arduino: não li " + ARQUIVO_PERMISSOES + ": " + e.Message);
                }
                if (_pedidosSalvos == null) _pedidosSalvos = new Dictionary<string, object>();
            }
            var usb = Json.Dic(Json.Pegar(_pedidosSalvos, "usb"));
            if (usb == null)
            {
                usb = new Dictionary<string, object>();
                _pedidosSalvos["usb"] = usb;
            }
            return usb;
        }

        /// <summary>vendor:produto do aparelho (a permissão é pedida uma vez por Arduino).</summary>
        static bool JaPediu(string chave) => Json.Bool(Json.Pegar(UsbPedidos(), chave));

        static void MarcarPedido(string chave)
        {
            UsbPedidos()[chave] = true;
            try
            {
                // (pequeno e raro: grava na hora, antes da janela aparecer)
                File.WriteAllText(ArquivoPermissoes, Json.Escrever(_pedidosSalvos, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("arduino: não gravei " + ARQUIVO_PERMISSOES + ": " + e.Message);
            }
        }

        // ================================================================ conexão
        static void Falha(string motivo)
        {
            if (_device != "") _falhou[_device] = Agora();
            FecharPorta();
            DefinirStatus("Arduino: " + motivo);
            Desconectar();
        }

        static void Perdeu(string motivo)
        {
            FecharPorta();
            DefinirStatus("Arduino desconectado (" + (string.IsNullOrEmpty(motivo) ? "sem resposta" : motivo) + "): procurando de novo");
            Desconectar();
        }

        static void Desconectar()
        {
            bool estava = Conectado;
            Conectado = false;
            Firmware = "";
            Sensores = "";
            Aviso = "";
            _lastLedSent = "";
            _fase = Fase.Procurando;
            _ultimaProcura = Agora();
            if (estava) Mudar();
        }

        static void Receber(string linha, long agora)
        {
            if (linha.Length == 0) return;
            _ultimoRx = agora;
            if (linha.StartsWith("OLA:", StringComparison.Ordinal) || linha.StartsWith("PONG:", StringComparison.Ordinal) ||
                linha.StartsWith("READY:", StringComparison.Ordinal))
            {
                string fw = linha.Substring(linha.IndexOf(':') + 1);
                if (!Conectado || fw != Firmware) Conectou(fw);
                return;
            }
            if (!Conectado) return;
            if (linha.StartsWith("S:", StringComparison.Ordinal))
            {
                if (int.TryParse(linha.Substring(2), out int n) && n >= 1 && n <= 5)
                {
                    if (Ajustes.Bool("arduino", "sensores_invertidos")) n = 6 - n;
                    Contagem[n - 1]++;
                    Controles.Injetar(ACOES_SENSOR[n - 1]);
                    Acionou(n);
                }
                return;
            }
            switch (linha)
            {
                case "CFG":
                    Controles.Injetar(Acao.Config);
                    return;
                case "START":
                    Controles.Injetar(Acao.Start);
                    return;
                case "FICHA":
                    Controles.Injetar(Acao.Credito);
                    return;
                case "PORTA:ABERTA":
                case "PORTA:FECHADA":
                    {
                        bool aberta = linha == "PORTA:ABERTA";
                        PortaAberta = aberta != Ajustes.Bool("arduino", "porta_invertida");
                        Aviso = "";
                        Mudar();
                        return;
                    }
            }
            if (linha.StartsWith("SENS:", StringComparison.Ordinal))
            {
                string s = linha.Substring(5);
                if (Ajustes.Bool("arduino", "sensores_invertidos")) s = AoContrario(s);
                Sensores = s;
                Mudar();
            }
            else if (linha.StartsWith("CAL:", StringComparison.Ordinal))
            {
                Calibracao = linha;
                Mudar();
            }
            else if (linha.StartsWith("INFO:", StringComparison.Ordinal))
            {
                Info = linha.Substring(5);
                Mudar();
            }
            else if (linha.StartsWith("AVISO:", StringComparison.Ordinal))
            {
                Aviso = linha.Substring(6);
                Mudar();
            }
        }

        static string AoContrario(string s)
        {
            var c = s.ToCharArray();
            Array.Reverse(c);
            return new string(c);
        }

        static void Conectou(string fw)
        {
            FechouJanela();
            Conectado = true;
            Firmware = fw;
            _fase = Fase.Conectado;
            _falhou.Clear();
            if (fw.StartsWith("STRIKE_KIDS", StringComparison.Ordinal))
            {
                DefinirStatus("Arduino conectado (" + Chip + "): sensores, botão e porta OK");
                EnviarPorta();
                Enviar(_teste ? "TESTE:1" : "TESTE:0");
                Enviar("INFO?");
            }
            else
            {
                DefinirStatus("Arduino com o firmware ANTIGO (só LEDs): grave o arduino/strike_kids");
            }
            Enviar(_ledState);
            _lastLedSent = _ledState;
            Mudar();
        }

        // ================================================================ porta
        /// <summary>
        /// A trava da porta das bolas: abre depois do START, fecha no fim (espera,
        /// menu). Vale também se o Arduino conectar depois: ele recebe ao conectar.
        /// </summary>
        public static void Porta(bool abrir)
        {
            if (abrir == PortaQuer && Conectado) return;
            PortaQuer = abrir;
            EnviarPorta();
        }

        /// <summary>O operador trocou "porta invertida": manda de novo, do jeito certo.</summary>
        public static void ReenviarPorta() => EnviarPorta();

        static void EnviarPorta()
        {
            if (!Completo()) return;
            bool abre = PortaQuer != Ajustes.Bool("arduino", "porta_invertida");
            Enviar(abre ? "PORTA:ABRE" : "PORTA:FECHA");
        }

        /// <summary>Teste dos sensores (menu): o Arduino manda o estado ao vivo (Sensores).</summary>
        public static void ModoTeste(bool sim)
        {
            _teste = sim;
            if (sim) Array.Clear(Contagem, 0, Contagem.Length);
            if (Completo()) Enviar(sim ? "TESTE:1" : "TESTE:0");
        }

        /// <summary>
        /// Calibrar (com a pista VAZIA): o nível de agora de cada sensor vira o
        /// "sem bola" — serve para sensor normalmente fechado ou aberto. A
        /// resposta chega em Calibracao ("CAL:OK:HHHHH") com o evento Mudou.
        /// </summary>
        public static bool Calibrar()
        {
            if (!Completo()) return false;
            Calibracao = "";
            Enviar("CALIBRA");
            return true;
        }

        /// <summary>Status, firmware, chip e o que o Android enxerga no USB (menu do operador).</summary>
        public static string Relatorio()
        {
            var sb = new StringBuilder();
            sb.Append("Arduino: ").Append(Status).Append('\n');
            sb.Append("firmware: ").Append(Firmware != "" ? Firmware : "-").Append("   chip: ").Append(Chip != "" ? Chip : "-").Append('\n');
            if (Info != "") sb.Append("info: ").Append(Info).Append('\n');
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (_serial != null) sb.Append(_serial.Relatorio());
            }
            catch (Exception e)
            {
                Falhou(e);
            }
#endif
            return sb.ToString();
        }

        static void DefinirStatus(string mensagem)
        {
            if (mensagem == Status) return;
            Status = mensagem;
            Mudar();
        }

        static void Enviar(string linha)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_serial != null && (_fase == Fase.Apresentando || _fase == Fase.Conectado)) _serial.Escrever(linha);
#endif
        }

        /// <summary>MENU, BLUE, RED, IDLE, NORMAL, STRIKE, SPARE, MISS, OFF.</summary>
        public static void SendLed(string comando)
        {
            if (Array.IndexOf(LED_COMMANDS, comando) < 0) return;
            _ledState = comando;
            if (Conectado && _lastLedSent != comando)
            {
                Enviar(comando);
                _lastLedSent = comando;
            }
        }

        // ---------------------------------------------------------------- eventos (quem ouve nunca derruba a ponte)
        static void Avisar(string t)
        {
            try { JanelaAndroid?.Invoke(t); } catch (Exception e) { Debug.LogException(e); }
        }

        static void Mudar()
        {
            try { Mudou?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
        }

        static void Acionou(int n)
        {
            try { Sensor?.Invoke(n); } catch (Exception e) { Debug.LogException(e); }
        }

        static void Falhou(Exception e)
        {
            long agora = Agora();
            if (agora - _ultimoErro < 10000) return;
            _ultimoErro = agora;
            Debug.LogWarning("ArduinoBridge: " + e);
        }
    }
}
