using System.Collections.Generic;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>As ações do jogo.</summary>
    public enum Acao { Start, Z, X, C, V, B, Credito, Config }

    /// <summary>Teclas do controle remoto da TV Box (e as equivalentes no PC), para os menus.</summary>
    public enum Remoto { Cima, Baixo, Esquerda, Direita, Ok, Voltar, Menu }

    /// <summary>
    /// OS COMANDOS DO JOGO VÊM DA PLACA ZERO DELAY E/OU DO ARDUINO DA MÁQUINA (como
    /// na edição Godot, ArcadeControls.gd): os 5 SENSORES da pista (S1 = esquerda
    /// ... S5 = direita = jogadas Z X C V B), o START, a FICHA (crédito) e o botão
    /// de CONFIGURAÇÃO. Os ligados no Arduino chegam prontos (ArduinoBridge chama
    /// Injetar).
    ///
    /// A ZERO DELAY vale do jeito que ela se apresentar ao Android: como joystick
    /// (botão) ou, no modo teclado, como tecla. Sem mapeamento gravado valem os
    /// de fábrica (os mesmos da edição Godot na mesma máquina):
    ///   teclado: 1/Espaço = START, A S D F G = jogadas, 5 = crédito, 9 = configuração;
    ///   joystick (códigos do Android): X = meio esquerdo, bolinha = centro,
    ///   quadrado = esquerda, triângulo = meio direito, R1 = direita, START,
    ///   SELECT = crédito, R2 (ou L3) = configuração.
    /// MAPEAMENTO GRAVADO NA TV BOX: na tela CONFIGURAR BOTÕES DA PLACA (menu do
    /// operador, ou segurar qualquer botão por 10 s na tela de espera) o jogo pede
    /// um botão de cada vez, na sequência, e grava (Ajustes, seção "placa"). O
    /// botão gravado SUBSTITUI os de fábrica daquela ação; o R2 continua abrindo a
    /// configuração (se não virou outra ação).
    /// O CONTROLE REMOTO da TV Box (setas, OK, voltar, menu) navega no menu do
    /// operador.
    ///
    /// Na TV Box as teclas e botões chegam pela StrikeAtividade (Java), com o
    /// código cru do Android; se ela não estiver rodando (e no PC), vale o Input
    /// da Unity (teclas e botões de joystick).
    ///
    /// CADA APERTO É UM CLIQUE: uma tecla só vale de novo depois de ser solta,
    /// e um repique (menos de 120 ms) não conta.
    /// </summary>
    public static class Controles
    {
        const int INTERVALO_MINIMO_MS = 120;

        // códigos de tecla do Android (KeyEvent.KEYCODE_*)
        public const int AK_BACK = 4, AK_1 = 8, AK_5 = 12, AK_9 = 16, AK_DPAD_UP = 19, AK_DPAD_DOWN = 20, AK_DPAD_LEFT = 21,
            AK_DPAD_RIGHT = 22, AK_DPAD_CENTER = 23, AK_A = 29, AK_D = 32, AK_F = 34, AK_G = 35, AK_S = 47, AK_SPACE = 62,
            AK_ENTER = 66, AK_MENU = 82, AK_ESCAPE = 111, AK_NUMPAD_ENTER = 160, AK_NUMPAD_1 = 145, AK_NUMPAD_5 = 149,
            AK_NUMPAD_9 = 153;
        // botões de joystick do Android (KeyEvent.KEYCODE_BUTTON_*)
        public const int AB_A = 96, AB_B = 97, AB_X = 99, AB_Y = 100, AB_R1 = 103, AB_R2 = 105, AB_L3 = 106,
            AB_START = 108, AB_SELECT = 109;

        /// <summary>Os de fábrica (sem mapeamento gravado): o teclado (a Zero Delay no modo teclado,
        /// e o teclado de teste no PC e na TV Box) e a Zero Delay como joystick.</summary>
        static readonly Dictionary<Acao, string[]> FABRICA = new Dictionary<Acao, string[]>
        {
            [Acao.Start] = new[] { "tecla:Alpha1", "tecla:Keypad1", "tecla:Space", "android:" + AK_1, "android:" + AK_NUMPAD_1, "android:" + AK_SPACE, "android:" + AB_START },
            [Acao.Z] = new[] { "tecla:A", "android:" + AK_A, "android:" + AB_X },
            [Acao.X] = new[] { "tecla:S", "android:" + AK_S, "android:" + AB_A },
            [Acao.C] = new[] { "tecla:D", "android:" + AK_D, "android:" + AB_B },
            [Acao.V] = new[] { "tecla:F", "android:" + AK_F, "android:" + AB_Y },
            [Acao.B] = new[] { "tecla:G", "android:" + AK_G, "android:" + AB_R1 },
            [Acao.Credito] = new[] { "tecla:Alpha5", "tecla:Keypad5", "android:" + AK_5, "android:" + AK_NUMPAD_5, "android:" + AB_SELECT },
            [Acao.Config] = new[] { "tecla:Alpha9", "tecla:Keypad9", "android:" + AK_9, "android:" + AK_NUMPAD_9, "android:" + AB_R2, "android:" + AB_L3 },
        };

        /// <summary>Um passo do assistente CONFIGURAR BOTÕES DA PLACA.</summary>
        public struct Passo
        {
            public Acao acao;
            public string chave, nome;
            public Passo(Acao a, string c, string n)
            {
                acao = a;
                chave = c;
                nome = n;
            }
        }

        /// <summary>A sequência do assistente: a ação, a chave gravada e o nome na tela.</summary>
        public static readonly Passo[] SEQUENCIA =
        {
            new Passo(Acao.Start, "input_start", "STR  ·  START"),
            new Passo(Acao.Z, "input_z", "QUADRADO  ·  JOGADA ESQUERDA"),
            new Passo(Acao.X, "input_x", "X  ·  MEIO ESQUERDO"),
            new Passo(Acao.C, "input_c", "BOLINHA  ·  CENTRO"),
            new Passo(Acao.V, "input_v", "TRIÂNGULO  ·  MEIO DIREITO"),
            new Passo(Acao.B, "input_b", "R1  ·  JOGADA DIREITA"),
            new Passo(Acao.Credito, "input_credit", "SELECT  ·  CRÉDITO"),
            new Passo(Acao.Config, "input_teste", "R2  ·  CONFIGURAÇÃO"),
        };
        /// <summary>Os primeiros são obrigatórios; SELECT e R2 podem ser pulados (a máquina pode não
        /// ter esses botões ligados).</summary>
        public const int OBRIGATORIOS = 6;
        /// <summary>Segurar qualquer botão por este tempo na tela de espera abre a configuração — a
        /// saída de emergência se o mapeamento estiver errado.</summary>
        public const float SEGURAR_PARA_CONFIGURAR = 10f;

        /// <summary>O que aciona cada ação agora (os de fábrica ou o mapeamento gravado).</summary>
        static readonly Dictionary<Acao, string[]> _mapa = new Dictionary<Acao, string[]>();
        static KeyCode[] _teclas = new KeyCode[0];
        public static bool MapeamentoGravado { get; private set; }

        static readonly HashSet<string> _abaixados = new HashSet<string>();
        static readonly Dictionary<string, double> _seguradoDesde = new Dictionary<string, double>();
        // (assistente da placa) TODO aperto e soltura deste quadro, com o código cru
        static readonly List<KeyValuePair<string, bool>> _brutos = new List<KeyValuePair<string, bool>>();
        static bool _capturando;
        static KeyCode[] _todas;
        static readonly Dictionary<string, double> _ultimoClique = new Dictionary<string, double>();
        // o que aconteceu NESTE quadro
        static readonly List<string> _cliques = new List<string>();
        static readonly List<Acao> _injetadas = new List<Acao>();
        static readonly List<Acao> _injetadasProximo = new List<Acao>();
        static readonly List<Remoto> _remoto = new List<Remoto>();
        static readonly Dictionary<Remoto, double> _repete = new Dictionary<Remoto, double>();
        static int _quadro = -1;

        static bool PelaAtividade
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return AndroidPonte.Ativa;
#else
                return false;
#endif
            }
        }

        // ------------------------------------------------------------ leitura (uma vez por quadro)
        /// <summary>Lê os apertos e solturas deste quadro (lê uma vez só por quadro, quem chamar primeiro).</summary>
        public static void Atualizar()
        {
            if (_quadro == Time.frameCount) return;
            _quadro = Time.frameCount;
            if (_mapa.Count == 0) CarregarMapa();
            _cliques.Clear();
            _brutos.Clear();
            _injetadas.Clear();
            _injetadas.AddRange(_injetadasProximo);
            _injetadasProximo.Clear();
            _remoto.Clear();
            if (PelaAtividade) LerAndroid();
            else LerUnity();
            RepetirSetas();
        }

        static void Aperto(string codigo, bool apertou)
        {
            double agora = Time.realtimeSinceStartupAsDouble;   // (em double: a máquina fica ligada dias seguidos)
            if (_capturando) _brutos.Add(new KeyValuePair<string, bool>(codigo, apertou));
            if (apertou && !_abaixados.Contains(codigo)) _seguradoDesde[codigo] = agora;
            else if (!apertou) _seguradoDesde.Remove(codigo);
            if (apertou)
            {
                bool valido = !_abaixados.Contains(codigo);   // ainda segurado: não é clique novo
                if (valido && _ultimoClique.TryGetValue(codigo, out double u) && agora - u < INTERVALO_MINIMO_MS / 1000.0)
                    valido = false;                             // repique
                _abaixados.Add(codigo);
                if (valido)
                {
                    _ultimoClique[codigo] = agora;
                    _cliques.Add(codigo);
                }
            }
            else _abaixados.Remove(codigo);
        }

        static void LerAndroid()
        {
            string texto = AndroidPonte.LerTeclas();
            if (string.IsNullOrEmpty(texto)) return;
            foreach (var ev in texto.Split(';'))
            {
                int dp = ev.IndexOf(':');
                if (dp <= 0 || !int.TryParse(ev.Substring(0, dp), out int k)) continue;
                bool apertou = ev.Substring(dp + 1) == "0";
                Aperto("android:" + k, apertou);
                if (!apertou) continue;
                switch (k)
                {
                    case AK_DPAD_UP: _remoto.Add(Remoto.Cima); break;
                    case AK_DPAD_DOWN: _remoto.Add(Remoto.Baixo); break;
                    case AK_DPAD_LEFT: _remoto.Add(Remoto.Esquerda); break;
                    case AK_DPAD_RIGHT: _remoto.Add(Remoto.Direita); break;
                    case AK_DPAD_CENTER: case AK_ENTER: case AK_NUMPAD_ENTER: _remoto.Add(Remoto.Ok); break;
                    case AK_BACK: case AK_ESCAPE: _remoto.Add(Remoto.Voltar); break;
                    case AK_MENU: _remoto.Add(Remoto.Menu); break;
                }
            }
        }

        /// <summary>As teclas e botões de joystick que o Input da Unity precisa olhar (os do mapa).</summary>
        static KeyCode[] MontarTeclas()
        {
            var l = new List<KeyCode>();
            foreach (var p in _mapa)
                foreach (var c in p.Value)
                {
                    var k = KeyCodeDe(c);
                    if (k != KeyCode.None && !l.Contains(k)) l.Add(k);
                }
            return l.ToArray();
        }

        /// <summary>"tecla:A" -> KeyCode.A; "joy:3" -> KeyCode.JoystickButton3; o resto -> None.</summary>
        static KeyCode KeyCodeDe(string c)
        {
            if (c.StartsWith("tecla:") && System.Enum.TryParse(c.Substring(6), out KeyCode k)) return k;
            if (c.StartsWith("joy:") && int.TryParse(c.Substring(4), out int n) && n >= 0 && n < 20) return KeyCode.JoystickButton0 + n;
            return KeyCode.None;
        }

        /// <summary>O código gravável de uma tecla/botão da Unity.</summary>
        static string CodigoDe(KeyCode k)
        {
            if (k >= KeyCode.JoystickButton0 && k <= KeyCode.JoystickButton19) return "joy:" + (k - KeyCode.JoystickButton0);
            return "tecla:" + k;
        }

        static void LerUnity()
        {
            if (_capturando)
            {
                // o assistente da placa: qualquer tecla ou botão de joystick (do 1º joystick em diante)
                if (_todas == null)
                {
                    var l = new List<KeyCode>();
                    foreach (KeyCode k in System.Enum.GetValues(typeof(KeyCode)))
                        if (k != KeyCode.None && (k < KeyCode.Mouse0 || (k >= KeyCode.JoystickButton0 && k <= KeyCode.JoystickButton19)) && !l.Contains(k))
                            l.Add(k);
                    _todas = l.ToArray();
                }
                foreach (var k in _todas)
                {
                    if (Input.GetKeyDown(k)) Aperto(CodigoDe(k), true);
                    if (Input.GetKeyUp(k)) Aperto(CodigoDe(k), false);
                }
            }
            else
            {
                foreach (var k in _teclas)
                {
                    if (Input.GetKeyDown(k)) Aperto(CodigoDe(k), true);
                    if (Input.GetKeyUp(k)) Aperto(CodigoDe(k), false);
                }
            }
            if (Input.GetKeyDown(KeyCode.UpArrow)) _remoto.Add(Remoto.Cima);
            if (Input.GetKeyDown(KeyCode.DownArrow)) _remoto.Add(Remoto.Baixo);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) _remoto.Add(Remoto.Esquerda);
            if (Input.GetKeyDown(KeyCode.RightArrow)) _remoto.Add(Remoto.Direita);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) _remoto.Add(Remoto.Ok);
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)) _remoto.Add(Remoto.Voltar);
            if (Input.GetKeyDown(KeyCode.Menu) || Input.GetKeyDown(KeyCode.F1)) _remoto.Add(Remoto.Menu);
        }

        /// <summary>Setas seguradas repetem (menus do operador).</summary>
        static void RepetirSetas()
        {
            foreach (Remoto r in new[] { Remoto.Cima, Remoto.Baixo, Remoto.Esquerda, Remoto.Direita })
            {
                if (_remoto.Contains(r))
                {
                    _repete[r] = Time.realtimeSinceStartupAsDouble + 0.45;
                    continue;
                }
                if (!_repete.TryGetValue(r, out double quando)) continue;
                if (!SetaSegurada(r)) { _repete.Remove(r); continue; }
                if (Time.realtimeSinceStartupAsDouble >= quando)
                {
                    _remoto.Add(r);
                    _repete[r] = Time.realtimeSinceStartupAsDouble + 0.11;
                }
            }
        }

        static bool SetaSegurada(Remoto r)
        {
            if (PelaAtividade)
            {
                int ak = r == Remoto.Cima ? AK_DPAD_UP : r == Remoto.Baixo ? AK_DPAD_DOWN : r == Remoto.Esquerda ? AK_DPAD_LEFT : AK_DPAD_RIGHT;
                return _abaixados.Contains("android:" + ak);
            }
            KeyCode kc = r == Remoto.Cima ? KeyCode.UpArrow : r == Remoto.Baixo ? KeyCode.DownArrow : r == Remoto.Esquerda ? KeyCode.LeftArrow : KeyCode.RightArrow;
            return Input.GetKey(kc);
        }

        // ------------------------------------------------------------ perguntas
        public static bool Apertou(Acao a)
        {
            Atualizar();
            if (_injetadas.Contains(a)) return true;
            var lista = _mapa[a];
            foreach (var c in _cliques)
                if (System.Array.IndexOf(lista, c) >= 0) return true;
            return false;
        }

        public static bool Apertou(Remoto r)
        {
            Atualizar();
            return _remoto.Contains(r);
        }

        /// <summary>Jogada deste quadro ("Z".."B") ou "".</summary>
        public static string TeclaJogada()
        {
            if (Apertou(Acao.Z)) return "Z";
            if (Apertou(Acao.X)) return "X";
            if (Apertou(Acao.C)) return "C";
            if (Apertou(Acao.V)) return "V";
            if (Apertou(Acao.B)) return "B";
            return "";
        }

        /// <summary>Qualquer aperto (Arduino ou teclado) conta como "alguém está jogando".</summary>
        public static bool Atividade()
        {
            Atualizar();
            return _cliques.Count > 0 || _injetadas.Count > 0;
        }

        /// <summary>Do Arduino (sensores, START, ficha, configuração): já filtrado lá — vale no próximo quadro.</summary>
        public static void Injetar(Acao a) => _injetadasProximo.Add(a);

        // ------------------------------------------------------------ placa Zero Delay
        /// <summary>Os de fábrica e, por cima, o mapeamento gravado (cada ação gravada fica só com o
        /// botão dela; o R2 continua abrindo a configuração, se não virou outra ação).</summary>
        static void CarregarMapa()
        {
            _mapa.Clear();
            foreach (var p in FABRICA) _mapa[p.Key] = p.Value;
            var gravados = new Dictionary<Acao, string>();
            foreach (var passo in SEQUENCIA)
            {
                string c = Json.Str(Ajustes.Valor("placa", passo.chave));
                if (!string.IsNullOrEmpty(c)) gravados[passo.acao] = c;
            }
            MapeamentoGravado = gravados.Count > 0;
            string r2 = "android:" + AB_R2;
            bool r2Usado = false;
            foreach (var g in gravados)
            {
                _mapa[g.Key] = new[] { g.Value };
                if (g.Key != Acao.Config && g.Value == r2) r2Usado = true;
            }
            // um botão gravado para uma ação sai dos de fábrica das outras (o R2 gravado como
            // crédito não abre também a configuração)
            if (gravados.Count > 0)
                foreach (var a in FABRICA.Keys)
                {
                    if (gravados.ContainsKey(a)) continue;
                    var l = new List<string>();
                    foreach (var c in FABRICA[a])
                        if (!gravados.ContainsValue(c)) l.Add(c);
                    _mapa[a] = l.ToArray();
                }
            if (gravados.ContainsKey(Acao.Config) && !r2Usado && gravados[Acao.Config] != r2)
                _mapa[Acao.Config] = new[] { gravados[Acao.Config], r2 };
            _teclas = MontarTeclas();
        }

        /// <summary>Grava o mapeamento do assistente (ação -> código) e passa a valer na hora.</summary>
        public static void Gravar(Dictionary<Acao, string> botoes)
        {
            foreach (var passo in SEQUENCIA)
                Ajustes.Definir("placa", passo.chave, botoes.TryGetValue(passo.acao, out var c) ? c : "");
            CarregarMapa();
        }

        /// <summary>(assistente) Ligado: todo aperto e soltura cru do quadro fica em Brutos.</summary>
        public static bool Capturando
        {
            get => _capturando;
            set => _capturando = value;
        }

        /// <summary>(assistente) Os apertos (true) e solturas (false) crus deste quadro.</summary>
        public static List<KeyValuePair<string, bool>> Brutos
        {
            get
            {
                Atualizar();
                return _brutos;
            }
        }

        /// <summary>Há quanto tempo o botão segurado mais antigo está apertado (0 = nenhum).</summary>
        public static float SeguradoHa()
        {
            Atualizar();
            double agora = Time.realtimeSinceStartupAsDouble, mais = 0;
            foreach (var t in _seguradoDesde.Values) mais = System.Math.Max(mais, agora - t);
            return (float)mais;
        }

        /// <summary>Esquece os botões segurados (abriu a configuração: o aperto não conta de novo).</summary>
        public static void EsquecerSegurados() => _seguradoDesde.Clear();

        /// <summary>VOLTAR do controle remoto, ou Esc (sai do assistente sem mudar nada).</summary>
        public static bool EhVoltar(string codigo) =>
            codigo == "android:" + AK_BACK || codigo == "android:" + AK_ESCAPE || codigo == "tecla:Escape" || codigo == "tecla:Backspace";

        /// <summary>"BOTÃO START", "BOTÃO 3", "TECLA A"...: o código para as telas.</summary>
        public static string TextoDoCodigo(string codigo)
        {
            if (codigo.StartsWith("tecla:")) return "TECLA " + codigo.Substring(6).Replace("Alpha", "").ToUpperInvariant();
            if (codigo.StartsWith("joy:")) return "BOTÃO " + codigo.Substring(4);
            if (!codigo.StartsWith("android:") || !int.TryParse(codigo.Substring(8), out int k)) return codigo;
            if (k >= 188 && k <= 203) return "BOTÃO " + (k - 187);
            switch (k)
            {
                case 96: return "BOTÃO A";
                case 97: return "BOTÃO B";
                case 98: return "BOTÃO C";
                case 99: return "BOTÃO X";
                case 100: return "BOTÃO Y";
                case 101: return "BOTÃO Z";
                case 102: return "BOTÃO L1";
                case 103: return "BOTÃO R1";
                case 104: return "BOTÃO L2";
                case 105: return "BOTÃO R2";
                case 106: return "BOTÃO L3";
                case 107: return "BOTÃO R3";
                case 108: return "BOTÃO START";
                case 109: return "BOTÃO SELECT";
                case 110: return "BOTÃO MODE";
                case AK_DPAD_UP: return "SETA CIMA";
                case AK_DPAD_DOWN: return "SETA BAIXO";
                case AK_DPAD_LEFT: return "SETA ESQUERDA";
                case AK_DPAD_RIGHT: return "SETA DIREITA";
                case AK_DPAD_CENTER: case AK_ENTER: return "TECLA OK";
                case AK_SPACE: return "TECLA ESPAÇO";
            }
            if (k >= 7 && k <= 16) return "TECLA " + (k - 7);
            if (k >= 29 && k <= 54) return "TECLA " + (char)('A' + k - 29);
            if (k >= 144 && k <= 153) return "TECLA " + (k - 144) + " (numérico)";
            return "CÓDIGO " + k;
        }

        /// <summary>O código é um botão de joystick (e não uma tecla)?</summary>
        public static bool EhBotao(string codigo)
        {
            if (codigo.StartsWith("joy:")) return true;
            return codigo.StartsWith("android:") && int.TryParse(codigo.Substring(8), out int k) && ((k >= 96 && k <= 110) || (k >= 188 && k <= 203));
        }

        /// <summary>O que aciona a ação agora ("BOTÃO R1 / TECLA G"), só os desta plataforma.</summary>
        public static string TextoAtual(Acao a)
        {
            Atualizar();
            var partes = new List<string>();
            foreach (var c in _mapa[a])
            {
                bool daqui = PelaAtividade ? c.StartsWith("android:") : !c.StartsWith("android:");
                if (!daqui) continue;
                string t = TextoDoCodigo(c);
                if (!partes.Contains(t)) partes.Add(t);
            }
            return partes.Count > 0 ? string.Join(" / ", partes) : "-";
        }

        /// <summary>Texto para as telas de teste: o que aciona cada ação.</summary>
        public static string TextoDe(Acao a)
        {
            switch (a)
            {
                case Acao.Start: return "START (Arduino) · tecla 1";
                case Acao.Credito: return "FICHA (Arduino) · tecla 5";
                case Acao.Config: return "CONFIGURAÇÃO (Arduino) · tecla 9";
                default: return "SENSOR S" + ((int)a) + " (Arduino) · tecla " + "ASDFG"[(int)a - 1];
            }
        }
    }
}
