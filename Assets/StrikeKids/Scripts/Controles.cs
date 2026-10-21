using System.Collections.Generic;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>As ações do jogo.</summary>
    public enum Acao { Start, Z, X, C, V, B, Credito, Config }

    /// <summary>Teclas do controle remoto da TV Box (e as equivalentes no PC), para os menus.</summary>
    public enum Remoto { Cima, Baixo, Esquerda, Direita, Ok, Voltar, Menu }

    /// <summary>
    /// OS COMANDOS DO JOGO VÊM DO ARDUINO DA MÁQUINA: os 5 SENSORES da pista
    /// (S1 = esquerda ... S5 = direita = jogadas Z X C V B), o START, a FICHA
    /// (crédito) e o botão de CONFIGURAÇÃO (ArduinoBridge chama Injetar).
    ///
    /// Para testar sem a máquina, um teclado também vale (no PC ou ligado na
    /// TV Box): A S D F G = sensores, 1 ou Espaço = START, 5 = crédito,
    /// 9 = configuração. O CONTROLE REMOTO da TV Box (setas, OK, voltar,
    /// menu) navega no menu do operador.
    ///
    /// Na TV Box as teclas chegam pela StrikeAtividade (Java), com o código
    /// cru do Android; se ela não estiver rodando, vale o Input da Unity.
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

        /// <summary>O teclado de teste (PC e TV Box).</summary>
        static readonly Dictionary<Acao, string[]> TECLADO = new Dictionary<Acao, string[]>
        {
            [Acao.Start] = new[] { "tecla:Alpha1", "tecla:Keypad1", "tecla:Space", "android:" + AK_1, "android:" + AK_NUMPAD_1, "android:" + AK_SPACE },
            [Acao.Z] = new[] { "tecla:A", "android:" + AK_A },
            [Acao.X] = new[] { "tecla:S", "android:" + AK_S },
            [Acao.C] = new[] { "tecla:D", "android:" + AK_D },
            [Acao.V] = new[] { "tecla:F", "android:" + AK_F },
            [Acao.B] = new[] { "tecla:G", "android:" + AK_G },
            [Acao.Credito] = new[] { "tecla:Alpha5", "tecla:Keypad5", "android:" + AK_5, "android:" + AK_NUMPAD_5 },
            [Acao.Config] = new[] { "tecla:Alpha9", "tecla:Keypad9", "android:" + AK_9, "android:" + AK_NUMPAD_9 },
        };

        static readonly HashSet<string> _abaixados = new HashSet<string>();
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
            _cliques.Clear();
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

        static readonly KeyCode[] TECLAS = MontarTeclas();

        static KeyCode[] MontarTeclas()
        {
            var l = new List<KeyCode>();
            foreach (var p in TECLADO)
                foreach (var c in p.Value)
                    if (c.StartsWith("tecla:")) l.Add((KeyCode)System.Enum.Parse(typeof(KeyCode), c.Substring(6)));
            return l.ToArray();
        }

        static void LerUnity()
        {
            foreach (var k in TECLAS)
            {
                if (Input.GetKeyDown(k)) Aperto("tecla:" + k, true);
                if (Input.GetKeyUp(k)) Aperto("tecla:" + k, false);
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
            var lista = TECLADO[a];
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
