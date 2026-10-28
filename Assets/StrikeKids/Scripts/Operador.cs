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
            new Item("Modo livre (sem ficha)", "jogo", "modo_livre", "bool"),
            new Item("Modo livre: começar rolando a bola", "jogo", "inicio_bola", "bool"),
            new Item("Foto das crianças (câmera)", "jogo", "foto", "bool"),
            new Item("Giro da câmera", "jogo", "camera_giro", "lista", 0, 90, 180, 270),
            new Item("Espelhar a câmera", "jogo", "camera_espelho", "bool"),
            new Item("Fichas por partida", "jogo", "creditos_por_partida", "int", 1, 5),
            new Item("Tempo sem jogar até o aviso (s)", "jogo", "tempo_inatividade", "int", 20, 180),
            new Item("Volume da música", "som", "musica", "vol"),
            new Item("Volume dos efeitos", "som", "efeitos", "vol"),
            new Item("Som na demonstração", "som", "som_na_demo", "bool"),
            new Item("Qualidade gráfica", "video", "qualidade", "lista", "auto", "alta", "media", "baixa"),
            new Item("Cenário (fundo)", "video", "cenario", "lista", "auto", "leve", "completo"),
            new Item("Detalhes extras (vento, enfeites, borboletas)", "video", "detalhes", "bool"),
            new Item("Cenário 3D (auto = leve na Mali-450)", "video", "cenario3d", "lista", "auto", "alto", "leve"),
            new Item("Nitidez do 3D (resolução)", "video", "nitidez", "lista", "auto", "100%", "90%", "80%", "70%"),
            new Item("Mostrar desempenho na tela (QPS)", "video", "medidor", "bool"),
            new Item("> TESTE DE DESEMPENHO (18 s)", "", "desempenho", "acao"),
            new Item("Giro da tela", "tela", "giro", "lista", 1, -1, 0),
            new Item("> TESTE DOS SENSORES", "", "teste", "acao"),
            new Item("> CALIBRAR SENSORES (pista vazia)", "", "calibrar", "acao"),
            new Item("Sensores invertidos (direita/esquerda)", "arduino", "sensores_invertidos", "bool"),
            new Item("> TESTE DA PORTA (trava das bolas)", "", "porta", "acao"),
            new Item("Porta invertida", "arduino", "porta_invertida", "bool"),
            new Item("> TESTE DA CÂMERA", "", "camera", "acao"),
            new Item("> ZERAR RECORDES", "", "recordes", "acao"),
            new Item("> RESTAURAR PADRÃO DE FÁBRICA", "", "padrao", "acao"),
            new Item("> VOLTAR AO JOGO", "", "voltar", "acao"),
        };

        static readonly Dictionary<string, string> NOMES = new Dictionary<string, string>
        {
            ["facil"] = "FÁCIL", ["normal"] = "NORMAL", ["dificil"] = "DIFÍCIL", ["auto"] = "AUTOMÁTICA", ["alta"] = "ALTA",
            ["media"] = "MÉDIA", ["baixa"] = "BAIXA", ["leve"] = "LEVE (TV Box fraca)", ["completo"] = "COMPLETO",
            ["giro:1"] = "1 (topo à esquerda)", ["giro:-1"] = "-1 (topo à direita)", ["giro:0"] = "0 (sem giro)",
            ["cam:0"] = "0°", ["cam:90"] = "90°", ["cam:180"] = "180°", ["cam:270"] = "270°",
            ["vel:100"] = "100% (tempo real)", ["vel:115"] = "115%", ["vel:130"] = "130% (padrão)", ["vel:145"] = "145%", ["vel:160"] = "160%",
        };

        Principal _p;
        int _sel, _topo, _visiveis;
        float _esp, u = 1f;
        readonly List<Rotulo> _linhas = new List<Rotulo>();
        Rotulo _info, _testeRotulo;
        Figura _cameraTela;
        bool _teste, _camera, _porta, _calibrando;
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
            Fotos.Mudou += o.Atualizar;
            ArduinoBridge.Mudou += o.ArduinoMudou;
            // no menu a porta das bolas fica fechada; aqui pode aparecer a janela de permissão USB
            ArduinoBridge.Porta(false);
            ArduinoBridge.NaEspera(true);
            o.Atualizar();
            return o;
        }

        void OnDestroy()
        {
            Fotos.Mudou -= Atualizar;
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
            var titulo = Ui.Rotulo(transform, "STRIKE KIDS · OPERADOR", 58 * u, Materiais.Hex("#FFD43B"), 58 * u * 0.18f * 0.5f);
            Ui.Ret(titulo, 0, 40 * u, T.x, 58 * u * 1.5f);
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

        static object ValorAtual(Item it)
        {
            if (it.secao == "tela") return Tela.Giro;
            return Ajustes.Valor(it.secao, it.chave);
        }

        static string ValorTexto(Item it)
        {
            if (it.tipo == "acao") return "";
            var v = ValorAtual(it);
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

        void Atualizar()
        {
            if (_sel < _topo) _topo = _sel;
            else if (_sel >= _topo + _visiveis) _topo = _sel - _visiveis + 1;
            bool emTeste = _teste || _camera || _porta || _calibrando;
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

        void ArduinoMudou()
        {
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
            if (_teste || _camera || _porta || _calibrando)
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
            _teste = _camera = _porta = _calibrando = false;
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
