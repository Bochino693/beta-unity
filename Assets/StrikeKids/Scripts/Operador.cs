using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace StrikeKids
{
    /// <summary>
    /// MENU DO OPERADOR: o botão de CONFIGURAÇÃO da máquina (no Arduino, ou a
    /// tecla 9 / MENU do controle remoto) entra aqui, e apertado de novo volta ao
    /// jogo. Aqui se testa tudo: os sensores (ao vivo), a porta (trava), a câmera.
    ///
    /// Navegação com os próprios sensores/botões da máquina (Arduino):
    ///   X (meio-esquerda) = sobe        V (meio-direita) = desce
    ///   Z (esquerda)      = diminui     B (direita)      = aumenta
    ///   C (centro) ou START = escolher  CONFIGURAÇÃO     = voltar ao jogo
    /// Pelo CONTROLE REMOTO da TV Box: setas (cima/baixo andam, esquerda/direita
    /// mudam o valor), OK escolhe, VOLTAR (ou MENU) sai. No PC: setas, Enter e Esc.
    ///
    /// O menu abre POR CIMA do jogo pausado (Principal.AbrirMenu): nada do jogo é
    /// desmontado, e sair dele é na hora.
    /// </summary>
    public class Operador : MonoBehaviour
    {
        struct Item
        {
            public string rotulo, secao, chave, tipo;
            public object[] opcoes;
            public Item(string r, string s, string c, string t, params object[] o)
            {
                rotulo = r;
                secao = s;
                chave = c;
                tipo = t;
                opcoes = o;
            }
        }

        static readonly Item[] ITENS =
        {
            new Item("Rodadas por jogador", "jogo", "rodadas", "int", 1, 10),
            new Item("Bolas por rodada", "jogo", "tentativas", "int", 1, 3),
            new Item("Dificuldade", "jogo", "dificuldade", "lista", "facil", "normal", "dificil"),
            new Item("Velocidade do jogo", "jogo", "velocidade", "lista", 100, 115, 130, 145, 160),
            new Item("Para-choques nas canaletas", "jogo", "protecao_canaleta", "bool"),
            new Item("Replay do strike", "jogo", "replay_strike", "bool"),
            new Item("Demonstração na tela inicial", "jogo", "demo", "bool"),
            new Item("Modo livre (sem ficha e sem trava)", "jogo", "modo_livre", "bool"),
            new Item("Foto das crianças (câmera)", "jogo", "foto", "bool"),
            new Item("Giro da câmera", "jogo", "camera_giro", "lista", 0, 90, 180, 270),
            new Item("Espelhar a câmera", "jogo", "camera_espelho", "bool"),
            new Item("Fichas por jogador", "jogo", "creditos_por_partida", "int", 1, 5),
            new Item("Tempo sem jogar até o aviso (s)", "jogo", "tempo_inatividade", "int", 20, 180),
            new Item("Volume da música", "som", "musica", "vol"),
            new Item("Volume dos efeitos", "som", "efeitos", "vol"),
            new Item("Som na demonstração", "som", "som_na_demo", "bool"),
            new Item("Qualidade gráfica", "video", "qualidade", "lista", "auto", "alta", "media", "baixa"),
            new Item("Cenário (fundo)", "video", "cenario", "lista", "auto", "leve", "completo"),
            new Item("Detalhes extras (vento, enfeites, borboletas)", "video", "detalhes", "bool"),
            new Item("Cenário 3D (auto = alivia sozinho)", "video", "cenario3d", "lista", "auto", "alto", "leve"),
            new Item("Resolução da imagem (TV Box)", "video", "resolucao", "lista", "auto", "720p", "900p", "1080p", "tela"),
            new Item("Quadros por segundo", "video", "qps", "lista", "auto", "60", "30"),
            new Item("Desenho da tela", "video", "desenho", "lista", "auto", "direto", "imagem"),
            new Item("Antisserrilhado da tela (ao reabrir)", "video", "antisserrilhado", "bool"),
            new Item("Nitidez do 3D (só no desenho imagem)", "video", "nitidez", "lista", "auto", "100%", "90%", "80%", "70%"),
            new Item("Mostrar desempenho na tela (QPS)", "video", "medidor", "bool"),
            new Item("> TESTE DE DESEMPENHO (18 s)", "", "desempenho", "acao"),
            new Item("Giro da tela", "tela", "giro", "lista", 1, -1, 0),
            new Item("> TESTE DOS SENSORES", "", "teste", "acao"),
            new Item("> CALIBRAR SENSORES (pista vazia)", "", "calibrar", "acao"),
            new Item("Sensores invertidos (direita/esquerda)", "arduino", "sensores_invertidos", "bool"),
            new Item("> TESTE DA PORTA (trava das bolas)", "", "porta", "acao"),
            new Item("Porta invertida", "arduino", "porta_invertida", "bool"),
            new Item("Porta: tempo do motor (ponte H sem fim de curso)", "arduino", "porta_tempo", "lista",
                500, 800, 1000, 1200, 1500, 2000, 2500, 3000, 4000, 5000, 6000, 8000, 10000),
            new Item("Porta: fim de curso (micro-switch A3/A4)", "arduino", "porta_fim_de_curso", "bool"),
            new Item("> TESTE DA CÂMERA", "", "camera", "acao"),
            new Item("> CONFIGURAR BOTÕES DA PLACA", "", "botoes", "acao"),
            new Item("> ABRIR O JOGO AO LIGAR A TV", "", "inicio", "acao"),
            new Item("> SAIR PARA A TELA DA TV (ajustes, outros apps)", "", "sair_tv", "acao"),
            new Item("> ZERAR RECORDES", "", "recordes", "acao"),
            new Item("> RESTAURAR PADRÃO DE FÁBRICA", "", "padrao", "acao"),
            new Item("> VOLTAR AO JOGO", "", "voltar", "acao"),
        };

        static readonly Dictionary<string, string> NOMES = new Dictionary<string, string>
        {
            ["facil"] = "FÁCIL", ["normal"] = "NORMAL", ["dificil"] = "DIFÍCIL", ["auto"] = "AUTOMÁTICA", ["alta"] = "ALTA",
            ["media"] = "MÉDIA", ["baixa"] = "BAIXA", ["leve"] = "LEVE (TV Box fraca)", ["completo"] = "COMPLETO",
            ["direto"] = "DIRETO NA TELA (rápido)", ["imagem"] = "IMAGEM 3D (compatível)", ["720p"] = "720p (mais leve)",
            ["900p"] = "900p", ["1080p"] = "1080p (Full HD)", ["tela"] = "A DA PRÓPRIA TELA", ["60"] = "60 (o mais liso)",
            ["30"] = "30 TRAVADOS (uniforme)",
            ["giro:1"] = "1 (topo à esquerda)", ["giro:-1"] = "-1 (topo à direita)", ["giro:0"] = "0 (sem giro)",
            ["cam:0"] = "0°", ["cam:90"] = "90°", ["cam:180"] = "180°", ["cam:270"] = "270°",
            ["vel:100"] = "100% (tempo real)", ["vel:115"] = "115%", ["vel:130"] = "130% (padrão)", ["vel:145"] = "145%", ["vel:160"] = "160%",
        };

        Principal _p;
        int _sel, _topo, _visiveis;
        float _esp, u = 1f;
        readonly List<Rotulo> _linhas = new List<Rotulo>();
        Rotulo _info, _testeRotulo, _titulo;
        Figura _cameraTela;
        bool _teste, _camera, _porta, _calibrando;
        bool _inicio;                    // abrir o jogo ao ligar a TV
        string _inicioEstado = "";       // "jogo", "escolher", "outra|Nome" ("" = fora da TV Box)
        string _inicioPasso = "-";       // o que a última escolha abriu ("-" = nenhuma ainda)
        // CONFIGURAR BOTÕES DA PLACA (a Zero Delay): um botão de cada vez, na sequência
        const float ESPERA_MAXIMA = 20f, PULAR_OPCIONAL = 4f;
        bool _botoes, _bTerminado;
        int _bPasso;
        readonly Dictionary<Acao, string> _bGravar = new Dictionary<Acao, string>();
        string _bSegurando = "";
        float _bUltimoToque, _bVoltarEm;
        Rotulo _bPedido, _bDica, _bSinal, _bRodape;
        readonly List<Rotulo> _bLinhas = new List<Rotulo>();
        readonly int[] _testeContagem = new int[5];
        float _cameraMs, _cameraT;
        Vector2 _T;
        int _quadroAbriu;

        public static Operador Abrir(Transform pai, Principal p)
        {
            var rt = Ui.Camada("operador", pai);
            var c = rt.GetComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = 50;
            var o = rt.gameObject.AddComponent<Operador>();
            o._p = p;
            o._quadroAbriu = Time.frameCount;
            o.Montar();
            Fotos.Mudou += o.FotosMudou;
            ArduinoBridge.Mudou += o.ArduinoMudou;
            // no menu a porta das bolas fica fechada; aqui pode aparecer a janela de permissão USB
            ArduinoBridge.Porta(false);
            ArduinoBridge.NaEspera(true);
            o._inicioEstado = AndroidPonte.TelaInicial();
            o.Atualizar();
            return o;
        }

        /// <summary>De volta ao jogo (depois da escolha da tela inicial ou da tela da TV):
        /// confere de novo se a TV já abre o jogo sozinha.</summary>
        void OnApplicationFocus(bool foco)
        {
            if (!foco || _linhas.Count == 0 || _botoes) return;
            _inicioEstado = AndroidPonte.TelaInicial();
            if (_inicio) MostrarInicio();
            else Atualizar();
        }

        void OnDestroy()
        {
            Controles.Capturando = false;
            Fotos.Mudou -= FotosMudou;
            ArduinoBridge.Mudou -= ArduinoMudou;
            Tela.Mudou -= Remontar;
        }

        void Montar()
        {
            _T = Tela.Tamanho;
            var T = _T;
            u = Mathf.Min(T.x, T.y) / 1080f;
            Ui.Ret((RectTransform)transform, 0, 0, T.x, T.y);
            var fundo = Ui.Retangulo(transform, new Color(0.03f, 0.09f, 0.22f));
            Ui.Ret(fundo, 0, 0, T.x, T.y);
            _titulo = Ui.Rotulo(transform, "STRIKE KIDS · OPERADOR", 58 * u, Materiais.Hex("#FFD43B"), 58 * u * 0.18f * 0.5f);
            Ui.Ret(_titulo, 0, 40 * u, T.x, 58 * u * 1.5f);
            // a lista inteira se couber; senão, uma janela que rola com a seleção
            float area = T.y - 420 * u;
            _esp = Mathf.Clamp(area / ITENS.Length, 44f * u, 68f * u);
            _visiveis = Mathf.Min(ITENS.Length, (int)(area / _esp));
            float tam = Mathf.Min(34 * u, _esp * 0.55f);
            for (int i = 0; i < ITENS.Length; i++)
            {
                var l = Ui.Rotulo(transform, "", tam, Color.white, tam * 0.09f);
                l.Alinhar(-1);
                Ui.Ret(l, 60 * u, 150 * u, T.x - 120 * u, tam * 1.5f);
                _linhas.Add(l);
            }
            _info = Ui.Rotulo(transform, "", 26 * u, new Color(1, 1, 1, 0.75f), 26 * u * 0.09f);
            _info.Quebrar = true;
            Ui.Ret(_info, 30 * u, T.y - 250 * u, T.x - 60 * u, 220 * u);
            _testeRotulo = Ui.Rotulo(transform, "", 54 * u, Color.white, 54 * u * 0.09f);
            _testeRotulo.Quebrar = true;
            Ui.Ret(_testeRotulo, 0, T.y * 0.35f, T.x, 500 * u);
            Ui.Ver(_testeRotulo, false);
            float lado = Mathf.Min(T.x * 0.8f, T.y * 0.5f);
            _cameraTela = Ui.Figura(transform, null);
            Ui.Ret(_cameraTela, (T.x - lado) / 2, T.y * 0.3f, lado, lado);
            Ui.Ver(_cameraTela, false);
            Tela.Mudou -= Remontar;
            Tela.Mudou += Remontar;
        }

        /// <summary>A tela girou (giro da tela mudou): monta o menu de novo no tamanho novo.</summary>
        void Remontar()
        {
            for (int k = transform.childCount - 1; k >= 0; k--) Destroy(transform.GetChild(k).gameObject);
            _linhas.Clear();
            _topo = 0;
            Montar();
            Atualizar();
        }

        // (o ValorTexto é estático: o estado da tela inicial passa por aqui)
        static string _inicioEstadoMenu = "";

        static object ValorAtual(Item it)
        {
            if (it.secao == "tela") return Tela.Giro;
            return Ajustes.Valor(it.secao, it.chave);
        }

        static string ValorTexto(Item it)
        {
            if (it.tipo == "acao")
            {
                if (it.chave == "inicio" && _inicioEstadoMenu != "") return _inicioEstadoMenu == "jogo" ? "SIM" : "NÃO";
                return "";
            }
            var v = ValorAtual(it);
            // só a ponte H usa; o Arduino do relé (arduino/strike_kids) ignora
            if (it.chave == "porta_tempo" || it.chave == "porta_fim_de_curso")
                return (it.chave == "porta_tempo" ? Segundos(Json.Int(v)) : Json.Bool(v) ? "SIM" : "NÃO") +
                    (ArduinoBridge.SemPonte() ? " · relé: não usa" : "");
            switch (it.tipo)
            {
                case "bool": return Json.Bool(v) ? "SIM" : "NÃO";
                case "vol": return Mathf.RoundToInt((float)Json.Num(v) * 10f).ToString();
                case "lista":
                {
                    string k = Json.Str(v);
                    if (it.chave == "giro") k = "giro:" + Json.Int(v);
                    else if (it.chave == "camera_giro") k = "cam:" + Json.Int(v);
                    else if (it.chave == "velocidade") k = "vel:" + Json.Int(v);
                    return NOMES.TryGetValue(k, out var n) ? n : Json.Str(v);
                }
            }
            return Json.Str(v);
        }

        /// <summary>1500 -> "1,5 s"</summary>
        static string Segundos(int ms)
        {
            string t = (ms / 1000).ToString();
            if (ms % 1000 != 0) t += "," + (ms % 1000 / 100);
            return t + " s";
        }

        void Atualizar()
        {
            if (_sel < _topo) _topo = _sel;
            else if (_sel >= _topo + _visiveis) _topo = _sel - _visiveis + 1;
            _inicioEstadoMenu = _inicioEstado;
            bool emTeste = _teste || _camera || _porta || _calibrando || _inicio || _botoes;
            for (int i = 0; i < ITENS.Length; i++)
            {
                var it = ITENS[i];
                var l = _linhas[i];
                string v = ValorTexto(it);
                int j = i - _topo;
                Ui.Ver(l, !emTeste && j >= 0 && j < _visiveis);
                Ui.Pos(l, 60 * u, 150 * u + j * _esp);
                l.text = (i == _sel ? "» " : "   ") + it.rotulo + (v == "" ? "" : ":   " + v);
                l.color = i == _sel ? Materiais.Hex("#FFD43B") : new Color(1, 1, 1, 0.85f);
            }
            string rolar = _visiveis < ITENS.Length ? "   (" + (_sel + 1) + " de " + ITENS.Length + ")" : "";
            _info.text = "X sobe · V desce · Z diminui · B aumenta · C/START escolhe · CONFIGURAÇÃO volta" + rolar + "\n" +
                "Controle da TV Box: setas andam e mudam · OK escolhe · VOLTAR sai\n" +
                "Partidas: " + Ajustes.Contador("partidas") + "   ·   Strikes: " + Ajustes.Contador("strikes") + "   ·   Moedas: " +
                Ajustes.Contador("moedas") + "\n" + ArduinoBridge.Status + "\nCâmera: " + Fotos.TextoStatus() + "\n" + TextoAparelho();
        }

        /// <summary>Qual é o aparelho (para saber qual TV Box é): modelo, sistema, placa de vídeo, núcleos e memória.</summary>
        public static string TextoAparelho()
        {
            return "Aparelho: " + SystemInfo.deviceModel + " · " + SystemInfo.operatingSystem + " · " + SystemInfo.graphicsDeviceName +
                " · " + SystemInfo.graphicsDeviceVersion + " · " + SystemInfo.processorCount + " núcleos · " +
                (SystemInfo.systemMemorySize / 1024f).ToString("0.0") + " GB";
        }

        /// <summary>A câmera mudou: o menu mostra o estado novo (na configuração dos botões, nada:
        /// as linhas do menu ficam escondidas).</summary>
        void FotosMudou()
        {
            if (!_botoes) Atualizar();
        }

        void ArduinoMudou()
        {
            if (_botoes) return;
            if (_teste) MostrarTeste("");
            else if (_porta) MostrarPorta();
            else if (_calibrando) MostrarCalibracao();
            else Atualizar();
        }

        void Update()
        {
            Controles.Atualizar();
            // o mesmo aperto que abriu o menu não pode fechá-lo
            if (Time.frameCount == _quadroAbriu) return;
            // na configuração dos botões todo aperto é um botão da placa (VOLTAR sai)
            if (_botoes)
            {
                PassoBotoes();
                return;
            }
            // comandos: botões da máquina (Arduino), teclado do PC e controle remoto da TV Box
            string cmd = "";
            if (Controles.Apertou(Acao.Config)) cmd = "sair";
            else if (Controles.Apertou(Acao.Start)) cmd = "ok";
            else
            {
                string z = Controles.TeclaJogada();
                if (z != "")
                {
                    if (_teste)
                    {
                        int i = System.Array.IndexOf(new[] { "Z", "X", "C", "V", "B" }, z);
                        if (i >= 0) _testeContagem[i]++;
                        MostrarTeste(z);
                        return;
                    }
                    switch (z)
                    {
                        case "X": cmd = "sobe"; break;
                        case "V": cmd = "desce"; break;
                        case "Z": cmd = "menos"; break;
                        case "B": cmd = "mais"; break;
                        case "C": cmd = "ok"; break;
                    }
                }
            }
            if (cmd == "")
            {
                if (Controles.Apertou(Remoto.Cima)) cmd = "sobe";
                else if (Controles.Apertou(Remoto.Baixo)) cmd = "desce";
                else if (Controles.Apertou(Remoto.Esquerda)) cmd = "menos";
                else if (Controles.Apertou(Remoto.Direita)) cmd = "mais";
                else if (Controles.Apertou(Remoto.Ok)) cmd = "ok";
                else if (Controles.Apertou(Remoto.Voltar) || Controles.Apertou(Remoto.Menu)) cmd = "sair";
            }
            if (cmd != "") Comando(cmd);
            if (_camera)
            {
                _cameraT += Time.unscaledDeltaTime;
                if (_cameraT >= 1f)
                {
                    _cameraT = 0f;
                    if (Fotos.TemImagem())
                    {
                        if (!Ui.Visivel(_cameraTela)) CameraAoVivo();
                    }
                    else if (Time.realtimeSinceStartup - _cameraMs > 6f && Fotos.Pronta())
                        CameraRelatorio(" - SEM IMAGEM AINDA");
                }
            }
        }

        /// <summary>Um comando do menu (dos botões da máquina, do teclado ou do controle remoto).</summary>
        public void Comando(string cmd)
        {
            if (_camera && (cmd == "menos" || cmd == "mais"))
            {
                // no teste da câmera, Z / B giram a imagem (câmera montada de lado)
                int g = (Ajustes.Int("jogo", "camera_giro") + (cmd == "mais" ? 90 : 270)) % 360;
                Ajustes.Definir("jogo", "camera_giro", g);
                Sons.Tocar("selecao");
                if (Ui.Visivel(_cameraTela)) CameraAoVivo();
                return;
            }
            if (_porta && cmd == "ok")
            {
                // teste da porta: C ou START abre/fecha; CONFIGURAÇÃO sai
                ArduinoBridge.Porta(!ArduinoBridge.PortaQuer);
                Sons.Tocar("selecao");
                MostrarPorta();
                return;
            }
            if (_inicio && cmd == "ok" && _inicioEstado != "" && _inicioEstado != "jogo")
            {
                // OK: abre a escolha da tela inicial do Android
                _inicioPasso = AndroidPonte.EscolherTelaInicial();
                Sons.Tocar("selecao");
                MostrarInicio();
                return;
            }
            if (_teste || _camera || _porta || _calibrando || _inicio)
            {
                if (cmd == "sair" || cmd == "ok") SairDoTeste();
                return;
            }
            switch (cmd)
            {
                case "sobe": _sel = (_sel - 1 + ITENS.Length) % ITENS.Length; break;
                case "desce": _sel = (_sel + 1) % ITENS.Length; break;
                case "menos": Mudar(-1); break;
                case "mais": Mudar(1); break;
                case "ok": Acao_(); break;
                case "sair":
                    _p.FecharMenu();
                    return;
            }
            if (this != null) Atualizar();
        }

        void Mudar(int d)
        {
            var it = ITENS[_sel];
            object v = ValorAtual(it);
            switch (it.tipo)
            {
                case "int":
                    v = Mathf.Clamp(Json.Int(v) + d, (int)it.opcoes[0], (int)it.opcoes[1]);
                    break;
                case "bool":
                    v = !Json.Bool(v);
                    break;
                case "vol":
                    v = Mathf.Clamp01(Mathf.Round(((float)Json.Num(v) + d * 0.1f) * 10f) / 10f);
                    break;
                case "lista":
                {
                    var op = it.opcoes;
                    int i = 0;
                    for (int k = 0; k < op.Length; k++)
                        if (Json.Str(op[k]) == Json.Str(v) || (op[k] is int && v != null && Json.Int(v, int.MinValue) == (int)op[k])) i = k;
                    v = op[(i + d + op.Length) % op.Length];
                    break;
                }
                default:
                    return;
            }
            if (it.secao == "tela")
            {
                Tela.DefinirGiro(Json.Int(v));
                Sons.Tocar("selecao");
                return;   // a tela girou: o menu se monta de novo (Remontar)
            }
            Ajustes.Definir(it.secao, it.chave, v);
            if (it.chave == "foto")
            {
                Fotos.Iniciar();
                if (Json.Bool(v)) Fotos.PedirPermissaoAgora();
            }
            if (it.chave == "porta_invertida") ArduinoBridge.ReenviarPorta();
            if (it.chave == "porta_tempo" || it.chave == "porta_fim_de_curso") ArduinoBridge.EnviarPonte();
            Sons.AtualizarVolume();
            Sons.Tocar("selecao");
        }

        void Acao_()
        {
            var it = ITENS[_sel];
            if (it.tipo != "acao")
            {
                Mudar(1);
                return;
            }
            switch (it.chave)
            {
                case "teste":
                    ArduinoBridge.PedirPermissaoAgora();
                    _teste = true;
                    for (int i = 0; i < 5; i++) _testeContagem[i] = 0;
                    EntrarNoTeste();
                    ArduinoBridge.ModoTeste(true);
                    MostrarTeste("");
                    break;
                case "calibrar":
                    ArduinoBridge.PedirPermissaoAgora();
                    _calibrando = true;
                    EntrarNoTeste();
                    ArduinoBridge.Calibrar();
                    MostrarCalibracao();
                    break;
                case "porta":
                    ArduinoBridge.PedirPermissaoAgora();
                    _porta = true;
                    EntrarNoTeste();
                    ArduinoBridge.Porta(false);
                    MostrarPorta();
                    break;
                case "camera":
                    _camera = true;
                    EntrarNoTeste();
                    // confere a câmera e pede de novo a permissão que faltar
                    Fotos.PedirPermissaoAgora();
                    Fotos.NaEspera();
                    _cameraMs = Time.realtimeSinceStartup;
                    if (Fotos.Ligar()) CameraAoVivo();
                    else CameraRelatorio("");
                    break;
                case "botoes":
                    ConfigurarBotoes();
                    break;
                case "inicio":
                    _inicio = true;
                    _inicioPasso = "-";
                    _inicioEstado = AndroidPonte.TelaInicial();
                    EntrarNoTeste();
                    MostrarInicio();
                    break;
                case "sair_tv":
                    if (Application.platform == RuntimePlatform.Android) AndroidPonte.SairParaTv();
                    else
                    {
                        _inicio = true;
                        EntrarNoTeste();
                        MostrarInicio();
                    }
                    break;
                case "desempenho":
                    // volta ao jogo e roda o teste
                    Ajustes.Definir("video", "testar", true);
                    _p.FecharMenu();
                    break;
                case "recordes":
                    Ajustes.ZerarRecordes();
                    Sons.Tocar("pop");
                    break;
                case "padrao":
                    Ajustes.RestaurarPadrao();
                    ArduinoBridge.EnviarPonte();   // a ponte H volta ao de fábrica também
                    ArduinoBridge.ReenviarPorta();
                    Sons.Tocar("pop");
                    break;
                case "voltar":
                    _p.FecharMenu();
                    break;
            }
        }

        void EntrarNoTeste()
        {
            foreach (var l in _linhas) Ui.Ver(l, false);
            Ui.Ver(_testeRotulo, true);
            _testeRotulo.text = "";
            _testeRotulo.Tamanho(42 * u, 42 * u * 0.09f);
            Ui.Ret(_testeRotulo, 40 * u, 120 * u, _T.x - 80 * u, _T.y - 400 * u);
        }

        void SairDoTeste()
        {
            if (_teste) ArduinoBridge.ModoTeste(false);
            if (_porta) ArduinoBridge.Porta(false);
            _teste = _camera = _porta = _calibrando = _inicio = false;
            Ui.Ver(_testeRotulo, false);
            _testeRotulo.text = "";
            Ui.Ret(_testeRotulo, 0, _T.y * 0.35f, _T.x, 500 * u);
            _testeRotulo.Tamanho(54 * u, 54 * u * 0.09f);
            Ui.Ver(_cameraTela, false);
            Fotos.Desligar(2f);
            Atualizar();
        }

        /// <summary>As primeiras linhas do relatório do Arduino (USB, chip, firmware).</summary>
        static string RelatorioArduino()
        {
            var linhas = ArduinoBridge.Relatorio().Split('\n');
            return string.Join("\n", linhas, 0, Mathf.Min(12, linhas.Length));
        }

        /// <summary>A porta como o Arduino está usando: a versão do RELÉ (arduino/strike_kids, confirma
        /// na hora) ou a da PONTE H (o tempo e o fim de curso do menu).</summary>
        static string TextoPonte()
        {
            int versao = ArduinoBridge.Versao();
            if (versao < 3)
                return "Firmware antigo (versão " + versao + "): grave o arduino/strike_kids (relé/servo)\n" +
                    "ou o arduino/strike_kids_ponte_h (ponte H)";
            if (ArduinoBridge.SemPonte())
                return "Arduino do RELÉ/SERVO (sem ponte H): confirma a porta na hora\n" +
                    "(\"tempo do motor\" e \"fim de curso\" são só da ponte H)";
            string t = Ajustes.Bool("arduino", "porta_fim_de_curso")
                ? "Ponte H: anda até o FIM DE CURSO (A3 aberta, A4 fechada)"
                : "Ponte H: anda " + Segundos(Ajustes.Int("arduino", "porta_tempo")) + " (\"Porta: tempo do motor\")";
            if (ArduinoBridge.Ponte != "" && ArduinoBridge.Ponte != ArduinoBridge.PontePedida())
                t += "\n(mandando ao Arduino...)";
            return t;
        }

        /// <summary>Teste da porta: o que o jogo pediu e o que o Arduino confirmou.</summary>
        void MostrarPorta()
        {
            string t = "TESTE DA PORTA (trava das bolas)\n\n";
            if (ArduinoBridge.Completo())
            {
                string pedida = ArduinoBridge.PortaQuer ? "ABERTA" : "FECHADA";
                string confirmada = ArduinoBridge.PortaAberta ? "ABERTA" : "FECHADA";
                t += "Porta: " + pedida + "\n" + (confirmada == pedida ? "(o Arduino confirmou)" : "(esperando o Arduino...)") + "\n\n";
                if (ArduinoBridge.Aviso == "PORTA_TRAVOU")
                    t += "A PORTA NÃO CHEGOU: confira o motor, a ponte H e o fim de curso\n\n";
                t += TextoPonte() + "\n\n";
                t += "C ou START: abre / fecha\nCONFIGURAÇÃO: sai (a porta fecha)\n\n" +
                    "Abriu quando devia fechar? Ligue \"Porta invertida\" no menu\n(ponte H com fim de curso: inverta os fios do motor).";
            }
            else
            {
                _testeRotulo.Tamanho(30 * u, 30 * u * 0.09f);
                t += "SEM O ARDUINO\n" + ArduinoBridge.Status + "\n\n" + RelatorioArduino() + "\n\nCONFIGURAÇÃO ou START sai";
            }
            _testeRotulo.text = t;
        }

        /// <summary>Abrir o jogo ao ligar a TV: se o jogo já é a tela inicial do Android e,
        /// se não, como escolher (OK abre a escolha do Android).</summary>
        void MostrarInicio()
        {
            _testeRotulo.Tamanho(30 * u, 30 * u * 0.09f);
            string t = "ABRIR O JOGO AO LIGAR A TV\n\n";
            if (_inicioEstado == "")
                t += "Só na TV Box (Android).\n\nCONFIGURAÇÃO ou START sai";
            else if (_inicioEstado == "jogo")
                t += "PRONTO! A TV abre o STRIKE KIDS sozinha: ligando na tomada,\n" +
                    "pelo controle remoto (volta da espera) e a cada HOME.\n\n" +
                    "Para usar a TV (ajustes, outros apps): > SAIR PARA A TELA DA TV,\n" +
                    "ou HOME 5 vezes seguidas no controle remoto. Um HOME volta ao jogo.\n\n" +
                    "CONFIGURAÇÃO ou START sai";
            else
            {
                string nome = _inicioEstado.StartsWith("outra|") ? _inicioEstado.Substring(6) : "";
                t += nome != "" ? "AINDA NÃO: hoje a TV abre \"" + nome + "\".\n\n" : "AINDA NÃO: o Android ainda pergunta qual é a tela inicial.\n\n";
                t += "Aperte OK: na janela do Android escolha STRIKE KIDS e marque SEMPRE\n" +
                    "(ou: HOME no controle remoto > STRIKE KIDS > SEMPRE).\n";
                if (nome != "")
                    t += "Abriu a página do \"" + nome + "\"? Aperte LIMPAR PADRÕES (Abrir por padrão),\n" +
                        "volte e aperte HOME > STRIKE KIDS > SEMPRE.\n";
                if (_inicioPasso == "") t += "\nEsta TV não abriu nenhuma escolha: faça pelo PC, com o adb (veja o LEIA-ME).\n";
                t += "\nCONFIGURAÇÃO sai";
            }
            _testeRotulo.text = t;
        }

        // ================================================================ botões da placa
        /// <summary>CONFIGURAÇÃO DOS BOTÕES DA ZERO DELAY — um botão de cada vez, na sequência da
        /// máquina (configuracao_botoes.gd da edição Godot). O que for apertado aqui é gravado na TV
        /// Box e passa a valer no jogo inteiro (Controles). A placa vale do jeito que o Android
        /// entregar: botão de joystick ou, no modo teclado, tecla — a linha "SINAL RECEBIDO" mostra
        /// na hora o que chegou (o diagnóstico da placa). Sem toque por 20 s, volta ao jogo sem
        /// mudar nada; VOLTAR do controle remoto (ou Esc) sai sem gravar.</summary>
        public void ConfigurarBotoes()
        {
            _botoes = true;
            _bTerminado = false;
            _bPasso = 0;
            _bGravar.Clear();
            _bSegurando = "";
            _bVoltarEm = -1f;
            _bUltimoToque = Time.realtimeSinceStartup;
            Controles.Capturando = true;
            Controles.EsquecerSegurados();
            foreach (var l in _linhas) Ui.Ver(l, false);
            Ui.Ver(_info, false);
            Ui.Ver(_titulo, false);
            Ui.Ver(_testeRotulo, false);
            var T = _T;
            float y = T.y * 0.06f;
            BotaoRotulo("CONFIGURAR BOTÕES DA PLACA", y, 54 * u, Materiais.Hex("#7FE7FF"));
            _bPedido = BotaoRotulo("", y + 120 * u, 76 * u, Materiais.Hex("#FFD43B"));
            _bDica = BotaoRotulo("APERTE O BOTÃO DA MÁQUINA", y + 230 * u, 36 * u, new Color(1, 1, 1, 0.8f));
            _bSinal = BotaoRotulo("SINAL RECEBIDO: nenhum ainda", y + 300 * u, 30 * u, Materiais.Hex("#7FE7FF"));
            y += 420 * u;
            _bLinhas.Clear();
            for (int i = 0; i < Controles.SEQUENCIA.Length; i++)
            {
                _bLinhas.Add(BotaoRotulo("", y, 36 * u, new Color(1, 1, 1, 0.55f)));
                y += 82 * u;
            }
            _bRodape = BotaoRotulo("", T.y - 120 * u, 28 * u, new Color(1, 1, 1, 0.6f));
            AtualizarBotoes();
        }

        readonly List<Rotulo> _bTodos = new List<Rotulo>();

        Rotulo BotaoRotulo(string texto, float y, float tam, Color cor)
        {
            var l = Ui.Rotulo(transform, texto, tam, cor, tam * 0.09f);
            Ui.Ret(l, 0, y, _T.x, tam * 1.6f);
            _bTodos.Add(l);
            return l;
        }

        void PassoBotoes()
        {
            float agora = Time.realtimeSinceStartup;
            if (_bTerminado)
            {
                if (_bVoltarEm > 0f && agora >= _bVoltarEm) SairDosBotoes(true);
                return;
            }
            foreach (var ev in Controles.Brutos)
            {
                string codigo = ev.Key;
                if (!ev.Value)
                {
                    if (codigo == _bSegurando) _bSegurando = "";
                    continue;
                }
                if (Controles.EhVoltar(codigo))
                {
                    SairDosBotoes(true);   // VOLTAR do controle remoto (ou Esc): sai sem mudar nada
                    return;
                }
                _bUltimoToque = agora;
                _bSinal.text = "SINAL RECEBIDO: " + Controles.TextoDoCodigo(codigo) + "  ·  " +
                    (Controles.EhBotao(codigo) ? "botão de joystick" : "placa no modo teclado");
                if (_bSegurando != "") continue;
                if (_bGravar.ContainsValue(codigo))
                {
                    _bDica.text = "ESTE BOTÃO JÁ FOI USADO — APERTE OUTRO";
                    continue;
                }
                _bSegurando = codigo;
                _bGravar[Controles.SEQUENCIA[_bPasso].acao] = codigo;
                _bPasso++;
                _bDica.text = "APERTE O BOTÃO DA MÁQUINA";
                if (_bPasso >= Controles.SEQUENCIA.Length)
                {
                    ConcluirBotoes();
                    return;
                }
            }
            if (_bPasso >= Controles.OBRIGATORIOS && _bSegurando == "" && agora - _bUltimoToque > PULAR_OPCIONAL)
            {
                ConcluirBotoes();
                return;
            }
            if (agora - _bUltimoToque > ESPERA_MAXIMA)
            {
                SairDosBotoes(true);   // ninguém apertou nada: volta ao jogo sem mudar nada
                return;
            }
            AtualizarBotoes();
        }

        void AtualizarBotoes()
        {
            float agora = Time.realtimeSinceStartup;
            int total = Controles.SEQUENCIA.Length;
            if (_bPasso < total)
            {
                string nome = Controles.SEQUENCIA[_bPasso].nome;
                int corte = nome.IndexOf("  ·  ");
                _bPedido.text = corte > 0 ? nome.Substring(0, corte) : nome;
                if (_bPasso >= Controles.OBRIGATORIOS)
                    _bDica.text = "APERTE, OU AGUARDE " + Mathf.Max(0, Mathf.CeilToInt(PULAR_OPCIONAL - (agora - _bUltimoToque))) + " s PARA PULAR";
            }
            for (int i = 0; i < total; i++)
            {
                var passo = Controles.SEQUENCIA[i];
                var linha = _bLinhas[i];
                if (_bGravar.TryGetValue(passo.acao, out var codigo))
                {
                    linha.text = "OK  " + passo.nome + "   »   " + Controles.TextoDoCodigo(codigo);
                    linha.color = Materiais.Hex("#8DFF6A");
                }
                else if (i == _bPasso)
                {
                    linha.text = "»  " + passo.nome;
                    linha.color = Materiais.Hex("#FFD43B");
                }
                else
                {
                    linha.text = passo.nome + "   (atual: " + Controles.TextoAtual(passo.acao) + ")";
                    linha.color = new Color(1, 1, 1, 0.45f);
                }
            }
            _bRodape.text = "SEM TOQUE, VOLTA AO JOGO SEM ALTERAR EM " + Mathf.Max(0, Mathf.CeilToInt(ESPERA_MAXIMA - (agora - _bUltimoToque))) + " s";
        }

        void ConcluirBotoes()
        {
            _bTerminado = true;
            Controles.Gravar(_bGravar);
            AtualizarBotoes();
            _bPedido.text = "PRONTO!";
            _bDica.text = "GRAVADO. PARA REFAZER: BOTÃO DE CONFIGURAÇÃO, OU SEGURE UM BOTÃO 10 s NA TELA INICIAL";
            _bVoltarEm = Time.realtimeSinceStartup + 1.6f;
        }

        /// <summary>Fecha a configuração dos botões (e volta ao jogo, como na edição Godot).</summary>
        void SairDosBotoes(bool voltarAoJogo)
        {
            _botoes = false;
            _bTerminado = true;
            Controles.Capturando = false;
            Controles.EsquecerSegurados();
            foreach (var l in _bTodos)
                if (l != null) Destroy(l.gameObject);
            _bTodos.Clear();
            _bLinhas.Clear();
            Ui.Ver(_titulo, true);
            Ui.Ver(_info, true);
            if (voltarAoJogo) _p.FecharMenu();
            else Atualizar();
        }

        /// <summary>Calibração: com a pista vazia, o nível de cada sensor vira o "sem bola".</summary>
        void MostrarCalibracao()
        {
            string t = "CALIBRAR SENSORES\n\n";
            if (!ArduinoBridge.Completo())
            {
                _testeRotulo.Tamanho(30 * u, 30 * u * 0.09f);
                t += "SEM O ARDUINO\n" + ArduinoBridge.Status + "\n\n" + RelatorioArduino() + "\n\nCONFIGURAÇÃO ou START sai";
            }
            else if (ArduinoBridge.Calibracao.StartsWith("CAL:OK:"))
            {
                string niveis = ArduinoBridge.Calibracao.Substring(7);
                t += "PRONTO! Sem bola cada sensor fica em:\n";
                for (int i = 0; i < Mathf.Min(5, niveis.Length); i++)
                    t += "S" + (i + 1) + ": " + (niveis[i] == 'H' ? "ALTO (aciona em baixo)" : "BAIXO (aciona em alto)") + "\n";
                t += "\nRole a bola no TESTE DOS SENSORES para conferir.\nCONFIGURAÇÃO ou START sai";
            }
            else t += "Calibrando... deixe a pista VAZIA\n(nenhuma bola na frente dos sensores)";
            _testeRotulo.text = t;
        }

        /// <summary>Teste da câmera: a imagem ao vivo (Z / B giram).</summary>
        void CameraAoVivo()
        {
            _cameraTela.texture = Fotos.AoVivo();
            _cameraTela.uvRect = new Rect(0, 0, 1, 1);
            _cameraTela.SetVerticesDirty();
            Ui.Ver(_cameraTela, true);
            _testeRotulo.Tamanho(54 * u, 54 * u * 0.09f);
            Ui.Ret(_testeRotulo, 40 * u, 40 * u, _T.x - 80 * u, _T.y * 0.25f);
            _testeRotulo.text = "TESTE DA CÂMERA\n" + Fotos.NomeCamera + "\nZ / B giram a imagem · START sai";
        }

        /// <summary>Teste da câmera sem imagem: o que o Android enxerga e o que o jogo tentou.</summary>
        void CameraRelatorio(string aviso)
        {
            Ui.Ver(_cameraTela, false);
            var linhas = Fotos.Relatorio().Split('\n');
            string rel = string.Join("\n", linhas, 0, Mathf.Min(18, linhas.Length));
            _testeRotulo.Tamanho(30 * u, 30 * u * 0.09f);
            Ui.Ret(_testeRotulo, 40 * u, 30 * u, _T.x - 80 * u, _T.y - 60 * u);
            _testeRotulo.text = "TESTE DA CÂMERA" + aviso + "\n\nCâmera: " + Fotos.TextoStatus() + "\n\n" + rel + "\n\nSTART ou CONFIGURAÇÃO para sair";
        }

        void MostrarTeste(string ultimo)
        {
            string[] nomes = { "S1 ESQUERDA (Z)", "S2 MEIO-ESQ. (X)", "S3 CENTRO (C)", "S4 MEIO-DIR. (V)", "S5 DIREITA (B)" };
            string[] letras = { "Z", "X", "C", "V", "B" };
            string aoVivo = ArduinoBridge.Sensores ?? "";
            var txt = new System.Text.StringBuilder("TESTE DOS SENSORES\nrole a bola (ou passe a mão) em cada um\n\n");
            for (int i = 0; i < 5; i++)
            {
                string agora = "";
                if (aoVivo.Length == 5) agora = aoVivo[i] == '1' ? "   • BOLA" : "   – livre";
                int n = ArduinoBridge.Completo() ? Mathf.Max(ArduinoBridge.Contagem[i], _testeContagem[i]) : _testeContagem[i];
                txt.Append(letras[i] == ultimo ? "» " : "").Append(nomes[i]).Append(":  ").Append(n).Append(agora).Append('\n');
            }
            if (ArduinoBridge.Completo()) txt.Append("\nArduino conectado: • aparece enquanto a bola tapa o sensor.");
            else txt.Append("\nSem o Arduino (contando as teclas de teste).\n").Append(ArduinoBridge.Status);
            txt.Append("\nSTART ou CONFIGURAÇÃO para sair");
            _testeRotulo.text = txt.ToString();
            if (ultimo != "") Sons.Tocar("selecao");
        }
    }
}
