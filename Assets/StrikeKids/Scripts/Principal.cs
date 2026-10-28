using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StrikeKids
{
    /// <summary>
    /// STRIKE KIDS — CONTROLE DO JOGO
    ///
    /// Estados: ABERTURA -&gt; ATRACAO (demo) -&gt; JOGADORES (1 ou 2) -&gt; PERSONAGEM
    /// (1 jogador escolhe Bob ou Mia) -&gt; FOTO (câmera da máquina, se tiver) -&gt;
    /// a partida (PRONTO -&gt; ARREMESSO -&gt; ROLANDO -&gt; RESULTADO, com REPLAY no
    /// strike) -&gt; FIM -&gt; ATRACAO.
    ///
    /// Os botões e os 5 sensores vêm SÓ do Arduino (USB): START, sensores,
    /// FICHA e CONFIGURAÇÃO (ArduinoBridge -&gt; Controles). Para testar no PC:
    /// A S D F G = sensores, 1/Espaço = START, 5 = ficha, 9 = configuração; o
    /// controle remoto da TV Box (MENU) também abre o menu do operador.
    ///
    /// A cena tem só o objeto "Jogo" com este componente: todo o resto é
    /// montado por código aqui (mundo, pinos, máquina, bola, bichinhos,
    /// câmeras e telas). As sequências (abertura, vez, jogada, replay...) são
    /// corrotinas canceladas pelo número da sequência (_seq).
    /// </summary>
    public class Principal : MonoBehaviour
    {
        public enum E { ABERTURA, ATRACAO, JOGADORES, PERSONAGEM, PRONTO, ARREMESSO, ROLANDO, RESULTADO, REPLAY, FIM, DEMO, FOTO, TESTE, OPERADOR }

        const float Z_INICIO = 3.15f;
        const float X_MAO = 0.28f;          // a mão direita fica 0,28 m à direita do corpo
        const float MESMA_BOLA = 1.2f;      // s: o mesmo sensor de novo logo depois = a mesma bola
        /// <summary>o tempo da tela "QUANTOS VÃO JOGAR?" (START de novo dentro dele = 2 jogadores)</summary>
        const float TEMPO_JOGADORES = 4f;
        /// <summary>MODO LIVRE PELA BOLA (7.5, máquina sem botões): a bola rolada na espera começa a
        /// partida e já escolhe o amigo pelo lado (esquerda = Bob, direita = Mia, meio = Bob); outra
        /// bola na tela "QUANTOS VÃO JOGAR?" = 2 jogadores. A 2ª criança precisa pegar outra bola:
        /// o tempo é maior.</summary>
        const float TEMPO_JOGADORES_BOLA = 7f;
        /// <summary>a mesma bola pode passar por 2 sensores (ou o sensor piscar): a 2ª só vale depois disto</summary>
        const float OUTRA_BOLA = 1.5f;
        /// <summary>1 jogador pela bola: o amigo já vem escolhido e confirma sozinho neste tempo</summary>
        const float TEMPO_AMIGO_BOLA = 6f;
        /// <summary>o tempo máximo esperando a câmera assentar na pista (segundos de tela)</summary>
        const float ASSENTAR_MAX = 2.2f;
        static readonly Vector3[] PODIOS = { Medidas.G(-0.62f, 0f, 2.5f), Medidas.G(0.62f, 0f, 2.5f) };
        static readonly Vector3 POS_PISTA = Medidas.G(0f, 1.05f, 2.2f);
        static readonly Vector3 ALVO_PISTA = Medidas.G(0f, 0.18f, Medidas.Z_PINO1 + 0.6f);
        static readonly Dictionary<string, string> ROUPA_DO_TEMA = new Dictionary<string, string>
        {
            ["mar"] = "pirata", ["dinos"] = "dino", ["espaco"] = "astronauta", ["parque"] = "parque",
        };
        // linhas da imagem 3D por qualidade; a nitidez automática nunca desce de 85% (a imagem
        // não fica "quadriculada" na TV: no pior caso, 510 linhas)
        static readonly Dictionary<string, int> LADO_3D = new Dictionary<string, int> { ["alta"] = 1080, ["media"] = 720, ["baixa"] = 600 };
        const float FATOR_MIN = 0.85f;
        const int CAMADA_UI = 5;

        public E estado = E.ABERTURA;
        public Mundo mundo;
        public Pinos pinos;
        public Maquina maquina;
        public Bola bola;
        public Efeitos efeitos;
        public Hud hud;
        public Medidor medidor;
        public CameraCine cam;
        public Reflexos reflexos;
        Camera _camUi;
        Canvas _canvas;
        CanvasScaler _escala;
        readonly Dictionary<string, Personagem> personagens = new Dictionary<string, Personagem>();
        readonly Placar placar = new Placar();
        Jogada jogada;
        bool demo;
        int _seq;
        bool _pular;
        float _ultimaJogada = -1000f;
        /// <summary>a jogada só vale com o "ROLE A BOLA!" na tela (6.4: nem fica guardada para depois)</summary>
        bool _liberada;
        string _quemBola = "";
        float _bolaT = -1000f;
        int _seqDesempate = -1;
        float _inativo, _modalInativo;
        string _ativo = "bob", _plateia = "mia";
        List<int> _emPeAntes = new List<int>();
        float _tJogada, _tImpacto = -1f;
        bool _deckCam, _camBaixa;
        Transform _alvoReplay;
        float _velocidade = 1f;
        int _escolhaN = 1;
        float _escolhaT;
        int _destaque;
        float _fimT, _demoT, _cameraT;
        int _demoVez;
        float _ultimoSomPino;
        readonly List<string> _temasPartida = new List<string>();
        bool _temasProntos;
        int _seqFotos;
        RectTransform _resultadoTeste;
        float _mouseParado;

        // qualidade e nitidez do 3D
        string _qualidade = "media";
        float _fator = 1f;
        bool _adaptar = true;
        RenderTexture _rt3d;
        bool _parado3d;
        float _fpsT0 = -1f;
        int _fpsQ, _qLentos, _fpsRuins, _fpsBons;
        float _qUltimo, _teto = 1f, _tetoAte;
        float _fatorPendente = -1f;
        string _qualidadePendente = "";
        bool _fatorSalvar, _qualidadeSalvar;

        // menu do operador
        Operador _menu;
        string _antesMenu = "";
        int _quadroFechou = -1;

        public Vector2Int Tamanho3D => _rt3d != null ? new Vector2Int(_rt3d.width, _rt3d.height) : Vector2Int.zero;
        public float Fator => _fator;
        public string Qualidade => _qualidade;
        public bool EmTeste => estado == E.TESTE;

        // ================================================================ início
        void Awake()
        {
            Application.targetFrameRate = 60;
            // no PC a imagem sai junto com o monitor (sem rasgar); no Android é sempre assim
            QualitySettings.vSyncCount = Application.platform == RuntimePlatform.Android ? 0 : 1;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Tarefas.Preparar();
            // o jogo sem sombras nem luzes da Unity (a luz é dos shaders): nada a mais por quadro
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.pixelLightCount = 0;
            QualitySettings.antiAliasing = 0;
            QualitySettings.softParticles = false;
            QualitySettings.realtimeReflectionProbes = false;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            if (GetComponent<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            Tela.Iniciar();
            Medidas.ConfigurarFisica();
            Sons.Criar();
            ArduinoBridge.Criar();
            Fotos.Criar();
        }

        void Start()
        {
            _velocidade = Ajustes.Velocidade();
            Ritmo(1f);
            _qualidade = QualidadeEscolhida();
            // contornos (a casca escura em volta dos bichinhos e dos pinos): na qualidade
            // BAIXA ficam desligados (cada peça com contorno é desenhada duas vezes)
            Personagem.contornos = _qualidade != "baixa";
            LerNitidez();
            Montar3D();
            MontarTelas();
            // o aviso "marque Sempre abrir / toque em Permitir" antes das janelas do Android
            ArduinoBridge.JanelaAndroid += hud.AvisoAndroid;
            Fotos.JanelaAndroid += hud.AvisoAndroid;
            Tela.Mudou += TelaMudou;
            TelaMudou();
            ArduinoBridge.SendLed("MENU");
            // prepara os shaders de tudo antes da primeira cena (sem engasgo no primeiro strike)
            Shader.WarmupAllShaders();
            StartCoroutine(Abertura());
        }

        void OnDestroy()
        {
            Tela.Mudou -= TelaMudou;
            ArduinoBridge.JanelaAndroid -= hud.AvisoAndroid;
            Fotos.JanelaAndroid -= hud.AvisoAndroid;
            if (_rt3d != null) _rt3d.Release();
        }

        /// <summary>Qualidade gráfica valendo: a do operador (ou STRIKE_QUALIDADE nos testes);
        /// "auto" = a medida pelo TESTE DE DESEMPENHO, ou alta no PC e média na TV Box.</summary>
        string QualidadeEscolhida()
        {
            string q = Ajustes.Texto("video", "qualidade");
            string amb = Environment.GetEnvironmentVariable("STRIKE_QUALIDADE");
            if (!string.IsNullOrEmpty(amb)) q = amb;
            if (q == "auto")
            {
                if (_qualidadeSalvar) return _qualidade;
                string medida = Ajustes.Texto("video", "qualidade_auto");
                if (medida == "alta" || medida == "media" || medida == "baixa") q = medida;
                else q = Application.platform == RuntimePlatform.Android ? "media" : "alta";
            }
            if (!LADO_3D.ContainsKey(q)) q = "media";
            return q;
        }

        /// <summary>NITIDEZ: automática (começa do último fator bom desta máquina) ou fixa.</summary>
        void LerNitidez()
        {
            string n = Ajustes.Texto("video", "nitidez");
            _adaptar = n == "auto";
            if (_adaptar) _fator = Mathf.Clamp(Ajustes.Float("video", "fator"), FATOR_MIN, 1f);
            else if (float.TryParse(n.TrimEnd('%'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f))
                _fator = Mathf.Clamp(f / 100f, FATOR_MIN, 1f);
            else _fator = 1f;
        }

        void Montar3D()
        {
            var raiz3d = new GameObject("Mundo3D").transform;
            raiz3d.SetParent(transform, false);
            efeitos = Efeitos.Criar(raiz3d);
            mundo = Mundo.Criar(raiz3d);
            mundo.Construir(Ajustes.CenarioLeve());
            pinos = Pinos.Criar(raiz3d);
            pinos.Batida += BatidaPino;
            maquina = Maquina.Criar(raiz3d, pinos);
            maquina.Som += SomMaquina;
            maquina.Alcapao += AlcapaoMaquina;
            bola = Bola.Criar(raiz3d);
            bola.Bateu += BolaBateu;
            bola.Recolhida += BolaRecolhida;
            mundo.bola = bola.transform;
            foreach (var nome in new[] { "bob", "mia" })
            {
                var p = Personagem.Criar(raiz3d, nome, Ajustes.Detalhes());
                p.SoltouBola += SoltouBola;
                p.Som += SomPersonagem;
                personagens[nome] = p;
            }
            cam = CameraCine.Criar(transform);
            cam.cam.cullingMask = ~(1 << CAMADA_UI);
            // os pinos e a bola refletidos no verniz da pista
            reflexos = Reflexos.Criar(raiz3d, pinos, bola, cam.cam);
            _alvoReplay = new GameObject("alvo_replay").transform;
            _alvoReplay.SetParent(raiz3d, false);
        }

        void MontarTelas()
        {
            // câmera das telas: ortográfica, girada junto com o monitor em pé
            var gc = new GameObject("CameraTelas");
            gc.transform.SetParent(transform, false);
            gc.transform.position = new Vector3(0, -1000f, -100f);
            _camUi = gc.AddComponent<Camera>();
            _camUi.clearFlags = CameraClearFlags.SolidColor;
            _camUi.backgroundColor = Color.black;
            _camUi.cullingMask = 1 << CAMADA_UI;
            _camUi.depth = 10;
            _camUi.allowHDR = false;
            _camUi.allowMSAA = false;
            _camUi.useOcclusionCulling = false;
            _camUi.nearClipPlane = 0.1f;
            _camUi.farClipPlane = 200f;
            // a tela (Canvas no mundo, longe do 3D): 1 unidade = 1 ponto da tela lógica
            var go = new GameObject("Telas", typeof(RectTransform));
            go.layer = CAMADA_UI;
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0, -1000f, 0f);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            // o Canvas nasce "na tela" (Overlay) e mexe no próprio RectTransform: agora, no
            // mundo, volta para onde a câmera das telas olha
            go.transform.SetPositionAndRotation(new Vector3(0, -1000f, 0f), Quaternion.identity);
            go.transform.localScale = Vector3.one;
            _canvas.worldCamera = _camUi;
            _canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            _escala = go.AddComponent<CanvasScaler>();
            _escala.dynamicPixelsPerUnit = 1f;
            _escala.referencePixelsPerUnit = 100f;
            hud = Hud.Criar(go.transform);
            medidor = Medidor.Criar(go.transform, this);
        }

        // ================================================================ a imagem 3D
        /// <summary>Tamanho da imagem 3D: o lado menor da tela, limitado pelo LADO_3D da qualidade e
        /// pelo fator automático; a imagem é esticada até a tela (pelas telas, já girada).</summary>
        void TelaMudou()
        {
            var T = Tela.Tamanho;
            var f = Tela.Fisico;
            var rtc = (RectTransform)_canvas.transform;
            rtc.sizeDelta = T;
            rtc.SetPositionAndRotation(new Vector3(0, -1000f, 0f), Quaternion.identity);
            rtc.localScale = Vector3.one;
            _escala.dynamicPixelsPerUnit = Mathf.Max(1f, f.x / Mathf.Max(T.x, 1f));
            _camUi.projectionMatrix = Tela.Ortografica();
            float menor = Mathf.Min(f.x, f.y);
            float lado = Mathf.Min(menor, LADO_3D.TryGetValue(_qualidade, out int l) ? l : 720) * _fator;
            float k = lado / Mathf.Max(menor, 1f);
            int w = Mathf.Max(64, Mathf.RoundToInt(f.x * k)), h = Mathf.Max(64, Mathf.RoundToInt(f.y * k));
            int aa = _qualidade == "baixa" ? 2 : 4;
            if (reflexos != null) reflexos.ligado = _qualidade != "baixa";
            if (_rt3d == null || _rt3d.width != w || _rt3d.height != h || _rt3d.antiAliasing != aa)
            {
                var antiga = _rt3d;
                _rt3d = new RenderTexture(w, h, 24, RenderTextureFormat.Default)
                {
                    name = "imagem3d", antiAliasing = aa, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                };
                _rt3d.Create();
                cam.cam.targetTexture = _rt3d;
                hud.tela3d.texture = _rt3d;
                hud.tela3d.SetVerticesDirty();
                if (antiga != null)
                {
                    antiga.Release();
                    Destroy(antiga);
                }
                if (_parado3d && _menu == null) StartCoroutine(UmQuadro3D());
            }
            hud.Layout();
        }

        /// <summary>Com o 3D parado atrás do modal: a imagem do tamanho novo sai uma vez.</summary>
        IEnumerator UmQuadro3D()
        {
            cam.cam.enabled = true;
            yield return null;
            yield return new WaitForEndOfFrame();
            if (_parado3d) cam.cam.enabled = false;
        }

        /// <summary>Modal por cima do jogo ("QUANTOS VÃO JOGAR?"): a imagem 3D de trás fica parada
        /// (a última, com um véu escuro por cima) e não é desenhada de novo — a TV Box só desenha
        /// o modal. A câmera para onde está.</summary>
        void Congelar3D(bool sim)
        {
            if (_menu != null) return;
            _parado3d = sim;
            cam.cam.enabled = !sim;
            if (sim) cam.Congelar();
        }

        // ================================================================ entrada
        void LerEntrada()
        {
            if (Input.GetKeyDown(KeyCode.F3)) medidor.Alternar();
            if (Controles.Apertou(Acao.Config) || Controles.Apertou(Remoto.Menu))
            {
                AbrirMenu();
                return;
            }
            if (Controles.Apertou(Acao.Credito))
            {
                Ajustes.AdicionarCredito();
                Sons.Tocar("moeda", 0.65f);
                AtualizarAtracao();
                return;
            }
            if (Controles.Apertou(Acao.Start))
            {
                ApertouStart();
                return;
            }
            string z = Controles.TeclaJogada();
            if (z != "") JogadaRecebida(z);
        }

        void ApertouStart()
        {
            _inativo = 0f;
            if (_modalInativo > 0f)
            {
                FecharInatividade();
                return;
            }
            // a DEMONSTRAÇÃO (os bichinhos jogando) passa pelos estados da jogada — arremesso,
            // bola rolando, resultado, replay: o START vale nela inteira, como na abertura
            if (demo && EmDemo())
            {
                StartNaEspera();
                return;
            }
            switch (estado)
            {
                case E.TESTE:
                    if (_resultadoTeste != null)
                    {
                        Destroy(_resultadoTeste.gameObject);
                        _resultadoTeste = null;
                        Atracao();
                    }
                    break;
                case E.ABERTURA:
                case E.ATRACAO:
                case E.DEMO:
                    StartNaEspera();
                    break;
                case E.JOGADORES:
                    // como no Dragon Bowling: o 2º START = 2 jogadores (mostra e já começa)
                    DoisJogadores();
                    break;
                case E.PERSONAGEM:
                    StartCoroutine(ConfirmarPersonagem());
                    break;
                case E.FIM:
                    if (_fimT > 2.5f && Ajustes.PodeComecar())
                    {
                        _quemBola = "";
                        EscolherJogadores();
                    }
                    break;
            }
        }

        bool EmDemo() => estado == E.DEMO || estado == E.ARREMESSO || estado == E.ROLANDO || estado == E.RESULTADO || estado == E.REPLAY;

        void DoisJogadores()
        {
            if (_escolhaN != 1) return;
            _escolhaN = 2;
            _escolhaT = Mathf.Min(_escolhaT, 1.1f);
            Sons.Tocar("selecao");
            ArduinoBridge.SendLed("RED");
        }

        /// <summary>Modo livre começando pela bola (menu do operador: "Começar rolando a bola"): a
        /// porta das bolas fica aberta também na espera.</summary>
        bool InicioPelaBola() => Ajustes.ModoLivre() && Ajustes.Bool("jogo", "inicio_bola");

        float TempoJogadores() => InicioPelaBola() ? TEMPO_JOGADORES_BOLA : TEMPO_JOGADORES;

        /// <summary>Bola rolada na espera (abertura, atração, demonstração, fim): começa a partida,
        /// como o START, e o lado dela já escolhe o amigo.</summary>
        void ComecarPelaBola(string zona)
        {
            hud.SensorAcendeu(zona);
            _quemBola = zona == "V" || zona == "B" ? "mia" : "bob";
            _bolaT = Time.realtimeSinceStartup;
            EscolherJogadores();
        }

        /// <summary>START na espera (abertura, atração ou demonstração, a qualquer momento): com
        /// crédito (ou no modo livre) já vai para a escolha dos jogadores.</summary>
        void StartNaEspera()
        {
            if (Ajustes.PodeComecar())
            {
                _quemBola = "";
                EscolherJogadores();
            }
            else hud.TextoGrande("INSIRA FICHA", Hud.CREME, 1f, 0.55f);
        }

        void JogadaRecebida(string zona)
        {
            _inativo = 0f;
            if (_modalInativo > 0f) FecharInatividade();
            // modo livre pela bola: na demonstração inteira a bola de verdade começa, como o START
            if (demo && EmDemo() && InicioPelaBola())
            {
                ComecarPelaBola(zona);
                return;
            }
            switch (estado)
            {
                case E.PRONTO:
                    if (_liberada) Arremessar(zona);
                    break;
                case E.JOGADORES:
                    // outra bola (não a mesma passando por 2 sensores) = 2 jogadores
                    if (InicioPelaBola() && Time.realtimeSinceStartup - _bolaT > OUTRA_BOLA)
                    {
                        _bolaT = Time.realtimeSinceStartup;
                        DoisJogadores();
                    }
                    break;
                case E.PERSONAGEM:
                    if (Time.realtimeSinceStartup - _bolaT < OUTRA_BOLA) return;
                    if (zona == "Z" || zona == "X") MarcarDestaque(0);
                    else if (zona == "V" || zona == "B") MarcarDestaque(1);
                    else StartCoroutine(ConfirmarPersonagem());
                    break;
                case E.ATRACAO:
                case E.DEMO:
                case E.ABERTURA:
                    if (InicioPelaBola()) ComecarPelaBola(zona);
                    else
                    {
                        // rolar a bola no menu também convida a jogar
                        hud.SensorAcendeu(zona);
                        Sons.Tocar("selecao");
                    }
                    break;
                case E.FIM:
                    if (InicioPelaBola() && _fimT > 2.5f) ComecarPelaBola(zona);
                    break;
                // (arremesso, bola rolando, resultado, replay, começo da vez: a jogada não vale —
                // nem fica guardada para quando a pista abrir)
            }
        }

        // ================================================================ esperas e ritmo
        int NovoSeq()
        {
            _seq++;
            _pular = false;
            return _seq;
        }

        /// <summary>Espera `t` segundos de tela (mesmo em câmera lenta, encurtados pela velocidade do
        /// jogo). Quem chama confere `s != _seq` depois (cancelada). pulavel: um sensor acionado
        /// encurta a espera. acelera false: segundos de verdade (a contagem da foto).</summary>
        IEnumerator Esperar(float t, int s, bool pulavel = true, bool acelera = true)
        {
            float passou = 0f;
            while (passou < t)
            {
                yield return null;
                if (s != _seq) yield break;
                passou += Time.unscaledDeltaTime * (acelera ? _velocidade : 1f);
                if (pulavel && _pular)
                {
                    _pular = false;
                    break;
                }
            }
        }

        /// <summary>Ritmo do tempo SEM mudar a física: o passo da física continua sempre de 1/120 s
        /// (o da calibração). O jogo mais rápido dá MAIS passos por segundo; a câmera lenta, menos
        /// (a interpolação deixa a imagem lisa).</summary>
        void Ritmo(float v)
        {
            if (_menu != null) return;
            Time.timeScale = Mathf.Clamp(_velocidade * v, 0.1f, 4f);
        }

        float _lentaEscala = 1f, _lentaDur, _lentaT0 = -1f;

        /// <summary>Câmera lenta que ENTRA e SAI suave: desce até `escala` em 0,08 s, segura e volta
        /// ao normal em 0,3 s (a duração encurta junto com a velocidade do jogo).</summary>
        void CameraLenta(float escala, float durReal)
        {
            PararLenta();
            _lentaEscala = escala;
            _lentaDur = Mathf.Max(durReal / _velocidade - 0.08f, 0f);
            _lentaT0 = Time.realtimeSinceStartup;
            AndarLenta();
        }

        void AndarLenta()
        {
            if (_lentaT0 < 0f) return;
            float t = Time.realtimeSinceStartup - _lentaT0;
            float v = 1f;
            if (t < 0.08f) v = Mathf.Lerp(1f, _lentaEscala, Mathf.Sin(t / 0.08f * Mathf.PI * 0.5f));
            else if (t < 0.08f + _lentaDur) v = _lentaEscala;
            else if (t < 0.38f + _lentaDur)
            {
                float u = (t - 0.08f - _lentaDur) / 0.3f;
                v = Mathf.Lerp(_lentaEscala, 1f, 1f - Mathf.Cos(u * Mathf.PI * 0.5f));
            }
            else _lentaT0 = -1f;
            Ritmo(v);
        }

        void PararLenta()
        {
            _lentaT0 = -1f;
            Ritmo(1f);
        }

        void AtrasadoSom(float t, string nome) => Tarefas.Depois(t, () => Sons.Tocar(nome));

        static Vector3 G(float x, float y, float z) => Medidas.G(x, y, z);

        // ================================================================ abertura e atração
        IEnumerator Abertura()
        {
            estado = E.ABERTURA;
            ArduinoBridge.Porta(InicioPelaBola());
            hud.ModoBola(InicioPelaBola());
            int s = NovoSeq();
            hud.Atracao(false);
            hud.MostrarSensores(false);
            hud.MostrarPlacar(false);
            mundo.AplicarTema(Temas.ORDEM[0]);
            maquina.Instantaneo();
            // os bichinhos entram depois (na atração)
            foreach (var p in personagens.Values) p.Visivel = false;
            // 1) ABERTURA DE BOLICHE: a bola com o alvo da Lazer & Sport rola pela pista
            // (câmera colada nela, faixas de cinema)...
            bola.Vestir("alvo");
            bola.Fogo(false);
            hud.TarjasCinema(true);
            cam.Corte(G(0.16f, 0.3f, 1.45f), G(0f, 0.14f, -10f), 26, 30);
            yield return Esperar(0.5f, s, false);
            if (s != _seq) yield break;
            // o strike da abertura entra na FRENTE da lista (que os testes usam) sem apagá-la
            Jogada.Forcar.Insert(0, 10);
            var j = Jogada.Planejar("C", pinos.EmPe(), true, "facil", true);
            j.Lancamento(0.35f, out var origem, out var vel);
            bola.Lancar(origem, vel, j.efeito);
            pinos.SoltarTodos();
            Sons.Tocar("whoosh", 0.6f);
            cam.Seguir(bola.transform, G(0.14f, 0.3f, 1.3f), G(0f, 0.06f, -3f), 28, 32, 7f);
            float t = 0f;
            while (bola.transform.position.z > Medidas.Z_PINO1 + 0.32f && t < 3f)
            {
                yield return null;
                if (s != _seq)
                {
                    Sons.Rolando(false);
                    yield break;
                }
                t += Time.deltaTime;
                var b = bola.transform.position;
                Sons.Rolando(true, bola.Velocidade());
                mundo.bolaZ = -b.z;
                // os pinos fazem cara de medo quando a bola chega
                if (b.z < Medidas.Z_PINO1 + 3.3f) pinos.Susto(b);
            }
            Sons.Rolando(false);
            // 2) ...STRIKE em câmera lenta, vista de lado (de cima da canaleta)
            cam.Corte(G(0.6f, 0.36f, Medidas.Z_PINO1 + 1.2f), G(-0.12f, 0.27f, Medidas.Z_PINO1 - 0.45f), 40, 44);
            cam.Tremer(0.05f, 0.4f);
            Sons.Tocar("pinos_queda", 0.9f);
            CameraLenta(0.3f, 1.3f);
            yield return Esperar(1f, s, false);
            if (s != _seq) yield break;
            // 3) LAZER & SPORT GAMES apresenta (por cima do strike)
            Sons.Tocar("strike", 0.75f);
            hud.Flash(Color.white, 0.45f, 0.3f);
            hud.Marca(true, false, true);
            AtrasadoSom(0.1f, "brilho");
            AtrasadoSom(0.6f, "pop");
            efeitos.Estrelas(G(0f, 0.6f, Medidas.Z_PINO1 - 0.3f), 30);
            cam.Ir(G(0f, 1.25f, Medidas.Z_PINO1 + 4.6f), G(0f, 1f, Medidas.Z_PINO1 - 1f), 46, 44, 0.8f);
            yield return Esperar(2.7f, s, false);
            if (s != _seq) yield break;
            hud.Marca(false);
            hud.TarjasCinema(false);
            // 4) voo até a atração: pinos de pé, os bichinhos chegam, STRIKE KIDS
            hud.Flash(Color.white, 0.6f, 0.35f);
            bola.Guardar();
            bola.Vestir("bob");
            mundo.bolaZ = 100f;
            maquina.Instantaneo();
            foreach (var p in personagens.Values) p.Visivel = true;
            PosicionarAtracao();
            Sons.Musica("musica_menu");
            cam.Trilho(new[]
            {
                new CameraCine.Ponto(G(0f, 4f, Medidas.Z_PINO1 + 5f), G(0f, 0.4f, Medidas.Z_PINO1), 0f),
                new CameraCine.Ponto(G(0f, 3.3f, 0.6f), G(0f, 0.6f, -4f), 1.3f),
                new CameraCine.Ponto(G(0f, 1.4f, 5.6f), G(0f, 0.75f, 1.6f), 1.2f),
            }, 52, 40);
            yield return Esperar(2.4f, s);
            if (s != _seq) yield break;
            hud.Flash(Color.white, 0.6f);
            Sons.Tocar("brilho");
            Atracao();
            NoClarao();
            if (Ajustes.Bool("video", "testar"))
            {
                Ajustes.Definir("video", "testar", false);
                TesteDesempenho();
            }
        }

        /// <summary>TESTE DE DESEMPENHO (pedido no menu do operador): pelas vistas mais pesadas;
        /// mostra a média de quadros por segundo e a qualidade recomendada.</summary>
        void TesteDesempenho()
        {
            estado = E.TESTE;
            ArduinoBridge.Porta(false);
            NovoSeq();
            hud.Atracao(false);
            PosicionarAtracao();
            StartCoroutine(medidor.Testar(r =>
            {
                if (r == null) return;   // o menu do operador abriu no meio do teste
                _resultadoTeste = r.cartaz;
                mundo.AplicarTema(Temas.ORDEM[0]);
            }));
        }

        void Atracao()
        {
            estado = E.ATRACAO;
            if (_qualidadeSalvar)
            {
                _qualidadeSalvar = false;
                if (Ajustes.Texto("video", "qualidade") == "auto") Ajustes.Definir("video", "qualidade_auto", _qualidade);
            }
            if (_fatorSalvar)
            {
                _fatorSalvar = false;
                Ajustes.Definir("video", "fator", Mathf.Round(_fator * 100f) / 100f);
            }
            // a porta das bolas fica FECHADA na espera (no modo livre pela bola, aberta: a bola
            // começa); o Arduino pode pedir a permissão USB aqui
            ArduinoBridge.Porta(InicioPelaBola());
            hud.ModoBola(InicioPelaBola());
            ArduinoBridge.NaEspera(true);
            demo = false;
            Sons.MudoDemo = false;
            Sons.AtualizarVolume();
            NovoSeq();
            Limpar();
            RoupaDeSempre();
            hud.Marca(false, true);
            hud.TelaFim(false);
            hud.Jogadores(false);
            Congelar3D(false);
            hud.Selecao(false);
            hud.Etapa(0);
            hud.Inatividade(false);
            _modalInativo = 0f;
            bola.Guardar();
            maquina.Instantaneo();
            PosicionarAtracao();
            AtualizarAtracao();
            Sons.Musica("musica_menu");
            cam.Orbitar(G(0f, 0.75f, 1.6f), 4.4f, 0.9f, 0.35f, 40, 46, 0.75f);
            ArduinoBridge.SendLed("MENU");
            _demoT = 0f;
            _cameraT = 0f;
            Fotos.Desligar(0.5f);
            Fotos.NaEspera();
        }

        void PosicionarAtracao()
        {
            var t = personagens["bob"];
            var r = personagens["mia"];
            t.Posicionar(G(-0.6f, 0f, 1.6f), 0f);
            r.Posicionar(G(0.6f, 0f, 1.6f), 0f);
            foreach (var p in new[] { t, r })
            {
                p.SegurarBola(false);
                p.Jatos(false);
                p.olharAlvo = null;
                // à vontade: respiram, acenam, olham em volta, dançam um pouquinho
                p.Ocioso(true);
                p.Expressao("feliz");
            }
        }

        void AtualizarAtracao()
        {
            if (!(estado == E.ATRACAO || estado == E.DEMO || estado == E.ABERTURA)) return;
            var r1 = Ajustes.Recorde(1);
            string txt = "";
            int pts = Json.Int(Json.Pegar(r1, "pontos"));
            if (pts > 0) txt = "RECORDE: " + pts + " PONTOS (" + Json.Str(Json.Pegar(r1, "quem")).ToUpperInvariant() + ")";
            string cred = Ajustes.ModoLivre() ? "MODO LIVRE" : "CRÉDITOS: " + Ajustes.Creditos;
            var lista = Classificacao.Lista();
            hud.Atracao(true, txt, cred, Fotos.FotoRecorde(1), lista.GetRange(0, Mathf.Min(3, lista.Count)));
        }

        // ================================================================ escolha
        void EscolherJogadores()
        {
            estado = E.JOGADORES;
            // START valeu: a porta das bolas ABRE (a criança já escolhe o amigo rolando a bola)
            ArduinoBridge.Porta(true);
            ArduinoBridge.NaEspera(false);
            NovoSeq();
            Limpar();
            demo = false;
            maquina.Instantaneo();
            Sons.Tocar("start");
            Sons.MudoDemo = false;
            Sons.AtualizarVolume();
            hud.Marca(false, true);
            hud.Atracao(false);
            hud.TelaFim(false);
            hud.Etapa(1);
            Congelar3D(true);
            // a câmera pode ter sido ligada depois: procura de novo e já liga (imagem pronta no "SORRIA!")
            Fotos.Preparar();
            _escolhaN = 1;
            _escolhaT = TempoJogadores();
            ArduinoBridge.SendLed("MENU");
            bola.Guardar();
            PararLenta();
            // escolha e fotos com a roupa de sempre; a roupinha da 1ª fase já vai sendo carregada
            RoupaDeSempre();
            // (uma fase a mais que as rodadas: a do DESEMPATE)
            SortearTemas(Ajustes.Rodadas() + 1);
            _temasProntos = true;
            PedirRoupas(_temasPartida[0]);
        }

        void ConfirmarJogadores()
        {
            hud.Jogadores(false);
            Congelar3D(false);
            if (_escolhaN == 2) StartCoroutine(Comecar(new List<string> { "bob", "mia" }));
            else EscolherPersonagem();
        }

        void EscolherPersonagem()
        {
            estado = E.PERSONAGEM;
            NovoSeq();
            hud.Etapa(2);
            // pela bola: o amigo do lado da bola já vem escolhido e confirma sozinho (quem não liga
            // fica com o Bob; rolar de novo troca)
            _escolhaT = _quemBola != "" ? TEMPO_AMIGO_BOLA : 8f;
            SubirNosPodios();
            personagens["bob"].Tocar("acenar", 0.3f, true);
            personagens["mia"].Tocar("parado", 0.3f, true);
            cam.Ir(G(0f, 1.05f, 6.1f), G(0f, 0.78f, 2.5f), 32, 52, 3f);
            _destaque = _quemBola == "mia" ? 1 : 0;
            MarcarDestaque(_destaque);
        }

        void MarcarDestaque(int i)
        {
            if (i != _destaque) Sons.Tocar("selecao");
            _destaque = i;
            var t = personagens["bob"];
            var r = personagens["mia"];
            t.Tocar(i == 0 ? "acenar" : "parado", 0.25f);
            r.Tocar(i == 1 ? "acenar" : "parado", 0.25f);
            t.Destaque(i == 0 ? 0.12f : 0f);
            r.Destaque(i == 1 ? 0.12f : 0f);
            mundo.Podios(true, PODIOS, i);
            _escolhaT = Mathf.Max(_escolhaT, 3f);
        }

        /// <summary>Bob e Mia em cima dos pódios (escolha e foto).</summary>
        void SubirNosPodios()
        {
            mundo.Podios(true, PODIOS, -1);
            personagens["bob"].Posicionar(PODIOS[0] + new Vector3(0, Medidas.ALTURA_PODIO, 0), 0f);
            personagens["mia"].Posicionar(PODIOS[1] + new Vector3(0, Medidas.ALTURA_PODIO, 0), 0f);
        }

        /// <summary>Frente de cada pódio na tela (as placas com os nomes ficam logo abaixo).</summary>
        Vector2[] PesNaTela()
        {
            var r = new Vector2[2];
            for (int i = 0; i < 2; i++)
            {
                var vp = cam.cam.WorldToViewportPoint(PODIOS[i] + G(0f, 0f, 0.45f));
                r[i] = Tela.DeViewport(new Vector2(vp.x, vp.y));
            }
            return r;
        }

        IEnumerator ConfirmarPersonagem()
        {
            if (estado != E.PERSONAGEM) yield break;
            estado = E.RESULTADO;
            int s = NovoSeq();
            string escolhido = _destaque == 0 ? "bob" : "mia";
            string outro = _destaque == 0 ? "mia" : "bob";
            hud.Selecao(false);
            Sons.Tocar("escolha");
            personagens[escolhido].Tocar("escolhido", 0.15f, true);
            personagens[outro].Tocar("feliz", 0.2f, true);
            hud.Flash(Color.white, 0.4f);
            efeitos.Estrelas(personagens[escolhido].transform.position + new Vector3(0, 1.2f, 0), 26);
            yield return Esperar(1.5f, s);
            if (s != _seq) yield break;
            personagens["bob"].Destaque(0f);
            personagens["mia"].Destaque(0f);
            yield return Comecar(new List<string> { escolhido });
        }

        // ================================================================ partida
        IEnumerator Comecar(List<string> lista)
        {
            Ajustes.GastarCreditos();
            Ajustes.Contar("partidas");
            placar.Comecar(lista, Ajustes.Rodadas(), Ajustes.Tentativas());
            mundo.DefinirProtecao(Ajustes.ProtecaoCanaleta());
            maquina.Instantaneo();
            if (!_temasProntos || _temasPartida.Count != Mathf.Max(Ajustes.Rodadas(), 1) + 1)
            {
                SortearTemas(Ajustes.Rodadas() + 1);
                PedirRoupas(_temasPartida[0]);
            }
            _temasProntos = false;
            Fotos.LimparPartida();
            // uma foto por jogador (1 jogador = 1 foto, 2 jogadores = 2 fotos)
            if (Fotos.PodeFotografar())
            {
                yield return SessaoDeFotos(lista);
                if (_seqFotos != _seq) yield break;
            }
            hud.ConfigurarPlacar(lista);
            hud.AtualizarPlacar(placar);
            Sons.Musica("musica_jogo");
            hud.Sequencia(0);
            StartCoroutine(Vez(true));
        }

        /// <summary>"SORRIA!": cada jogador tira uma foto com a câmera da máquina (3, 2, 1).</summary>
        IEnumerator SessaoDeFotos(List<string> lista)
        {
            estado = E.FOTO;
            int s = NovoSeq();
            _seqFotos = s;
            hud.Etapa(3);
            Fotos.Ligar();
            SubirNosPodios();
            personagens["bob"].Destaque(0f);
            personagens["mia"].Destaque(0f);
            cam.Ir(G(0f, 1.05f, 6.1f), G(0f, 0.78f, 2.5f), 32, 52, 3f);
            for (int i = 0; i < lista.Count; i++)
            {
                string p = lista[i];
                var a = personagens[p];
                mundo.Podios(true, PODIOS, p == "bob" ? 0 : 1);
                string sub = "olhe para a câmera da máquina!";
                if (lista.Count > 1) sub = "quem joga com " + (p == "bob" ? "o BOB" : "a MIA") + ": olhe para a câmera!";
                a.Tocar("acenar", 0.2f, true);
                hud.FotoTela(true, Fotos.AoVivo(), p, "SORRIA!", sub);
                Sons.Tocar("vez");
                yield return Esperar(1.9f, s, false, false);
                if (s != _seq) yield break;
                foreach (int n in new[] { 3, 2, 1 })
                {
                    hud.FotoContagem(n);
                    Sons.Tocar("tique", 0.9f, 1f + (3 - n) * 0.15f);
                    yield return Esperar(0.85f, s, false, false);
                    if (s != _seq) yield break;
                }
                hud.FotoContagem(0);
                hud.Flash(Color.white, 0.95f, 0.45f);
                Sons.Tocar("brilho");
                Sons.Tocar("pop", 0.8f, 0.8f);
                Texture2D foto = null;
                yield return Fotos.Fotografar("j" + i, f => foto = f);
                if (s != _seq) yield break;
                hud.FotoPronta(foto, p);
                a.Tocar("feliz", 0.2f, true);
                a.Expressao("empolgado");
                yield return Esperar(1.5f, s, false);
                if (s != _seq) yield break;
            }
            hud.FotoTela(false);
            Fotos.Desligar();
            mundo.Podios(false);
        }

        /// <summary>A fase (cenário) da rodada atual, do sorteio da partida.</summary>
        string TemaDaRodada()
        {
            if (_temasPartida.Count == 0) return mundo.tema;
            return _temasPartida[Mathf.Clamp(placar.rodadaAtual, 0, _temasPartida.Count - 1)];
        }

        /// <summary>FASES SORTEADAS: todas aparecem antes de alguma se repetir, e nunca a mesma
        /// duas rodadas seguidas. Sai sempre uma a mais que as rodadas: a do DESEMPATE (com 3
        /// rodadas, as 3 primeiras são 3 pistas diferentes e a 4ª fica para o desempate).</summary>
        void SortearTemas(int n)
        {
            _temasPartida.Clear();
            string antes = "";
            var usados = new HashSet<string>();
            for (int i = 0; i < Mathf.Max(n, 1); i++)
            {
                var opcoes = new List<string>();
                foreach (var t in Temas.ORDEM)
                    if (t != antes && !usados.Contains(t)) opcoes.Add(t);
                if (opcoes.Count == 0)
                {
                    usados.Clear();
                    foreach (var t in Temas.ORDEM)
                        if (t != antes) opcoes.Add(t);
                }
                string esc = opcoes[UnityEngine.Random.Range(0, opcoes.Count)];
                _temasPartida.Add(esc);
                usados.Add(esc);
                antes = esc;
            }
        }

        // ================================================================ roupinhas das fases
        void Vestir(string tema)
        {
            ROUPA_DO_TEMA.TryGetValue(tema ?? "", out var r);
            foreach (var p in personagens.Values) p.TrocarRoupa(r ?? "");
        }

        void PedirRoupas(string tema)
        {
            ROUPA_DO_TEMA.TryGetValue(tema ?? "", out var r);
            foreach (var p in personagens.Values) p.PedirRoupa(r ?? "");
        }

        /// <summary>Roupa de sempre (tela de início, escolha, fotos): se alguém ainda está de
        /// roupinha, troca escondido num clarão.</summary>
        void RoupaDeSempre()
        {
            bool trocar = false;
            foreach (var p in personagens.Values)
            {
                p.SoltarPedidos();
                if (p.roupa != "") trocar = true;
            }
            if (trocar)
            {
                hud.Flash(Color.white, 0.6f, 0.35f);
                Vestir("");
            }
        }

        /// <summary>Começo da vez de um jogador (ou da próxima bola dele).</summary>
        IEnumerator Vez(bool novaRodada)
        {
            estado = E.RESULTADO;
            int s = NovoSeq();
            _pular = false;
            _ativo = placar.Atual.personagem;
            _plateia = _ativo == "bob" ? "mia" : "bob";
            mundo.Podios(false);
            hud.Etapa(0);
            var a = personagens[_ativo];
            var p = personagens[_plateia];
            a.Jatos(false);
            p.Jatos(false);
            a.Destaque(0f);
            p.Destaque(0f);
            a.Ocioso(false);
            p.Ocioso(false);
            a.Posicionar(G(-X_MAO, 0f, Z_INICIO), 180f);
            a.SegurarBola(true);
            mundo.GuardarRetorno();
            a.Tocar("pronto", 0.15f, true);
            a.Expressao("feliz");
            a.olharAlvo = null;
            float lado = _ativo == "bob" ? 1f : -1f;
            p.Posicionar(G(lado * 1.85f, 0f, 1f), -(180f - lado * 38f));
            p.Tocar(placar.jogadores.Count == 1 ? "torcida" : "parado", 0.2f, true);
            p.olharAlvo = null;
            hud.MostrarPlacar(true);
            hud.AtualizarPlacar(placar);
            hud.MostrarMapa(pinos.EmPe());
            hud.MostrarSensores(true);
            hud.Sequencia(placar.Atual.seguidos);
            bola.Fogo(placar.Atual.seguidos >= 2);
            bool comeco = placar.rodadaAtual == 0 && placar.jogadorAtual == 0;
            if (novaRodada && (TemaDaRodada() != mundo.tema || comeco))
            {
                // cenário novo: clarão, troca e um passeio de câmera pelo lugar
                string tema = TemaDaRodada();
                hud.Flash(Color.white, 0.95f, 0.6f);
                mundo.AplicarTema(tema);
                Vestir(tema);
                // a roupinha da próxima fase já vai carregando durante esta rodada (na última, com
                // 2 jogadores, a da pista do desempate: se empatar)
                int prox = placar.rodadaAtual + 1;
                if (prox < _temasPartida.Count && (prox < placar.rodadas || (placar.jogadores.Count == 2 && !placar.desempate)))
                    PedirRoupas(_temasPartida[prox]);
                NoClarao();
                Sons.Tocar("brilho");
                Sons.Tocar("whoosh", 0.6f);
                hud.AnunciarCenario(tema, 1.7f);
                // passeio pelo cenário que termina na PISTA DE FRENTE (a vista da espera)
                cam.Trilho(new[]
                {
                    new CameraCine.Ponto(G(-2.6f, 4.6f, Medidas.Z_PINO1 + 1f), G(0f, 2.2f, Medidas.Z_FUNDO - 3f), 0f),
                    new CameraCine.Ponto(G(2.2f, 3.4f, -5f), G(0f, 1.4f, Medidas.Z_FUNDO), 1.4f),
                    new CameraCine.Ponto(POS_PISTA, ALVO_PISTA, 1.3f),
                }, 34, 40);
                yield return Esperar(2.9f, s, false);
                if (s != _seq) yield break;
            }
            else if (novaRodada) PlanoPista(false);
            if (novaRodada)
            {
                string artigo = _ativo == "bob" ? "DO" : "DA";
                string sub = "RODADA " + (placar.rodadaAtual + 1) + " DE " + placar.rodadasNormais;
                if (placar.desempate) sub = "DESEMPATE!";
                else if (placar.rodadaAtual + 1 == placar.rodadasNormais && placar.rodadasNormais > 1) sub = "ÚLTIMA RODADA!";
                hud.Faixa(_ativo, "VEZ " + artigo + " " + _ativo.ToUpperInvariant() + "!", sub, Fotos.Foto("j" + placar.jogadorAtual));
                Sons.Tocar("vez");
                ArduinoBridge.SendLed(_ativo == "bob" ? "BLUE" : "RED");
                yield return Esperar(1.6f, s, false);
                if (s != _seq) yield break;
            }
            StartCoroutine(Pronto());
        }

        /// <summary>Esperando a jogada: a PISTA DE FRENTE, como a criança vê da máquina. O mascote
        /// fica fora do quadro e só aparece depois que o sensor dispara.</summary>
        void PlanoPista(bool corte = false)
        {
            if (corte) cam.Corte(POS_PISTA, ALVO_PISTA, 34, 40);
            else cam.Ir(POS_PISTA, ALVO_PISTA, 34, 40, 2.6f);
        }

        IEnumerator Pronto()
        {
            estado = E.PRONTO;
            int s = NovoSeq();
            _liberada = false;
            _inativo = 0f;
            PlanoPista();
            hud.Convite(false);
            personagens[_ativo].Expressao("concentrado");
            // a jogada só vale com a pista na tela: a câmera assenta, aparece o ROLE A BOLA! e
            // só então os sensores e as teclas valem
            float t = 0f;
            while (t < ASSENTAR_MAX && !cam.Assentou())
            {
                yield return null;
                if (s != _seq || estado != E.PRONTO) yield break;
                t += Time.unscaledDeltaTime;
            }
            if (s != _seq || estado != E.PRONTO) yield break;
            hud.Convite(true);
            ArduinoBridge.SendLed("IDLE");
            _liberada = true;
        }

        void Arremessar(string zona)
        {
            if (estado != E.PRONTO && estado != E.DEMO) return;
            if (Time.realtimeSinceStartup - _ultimaJogada < MESMA_BOLA) return;
            _ultimaJogada = Time.realtimeSinceStartup;
            _liberada = false;
            NovoSeq();
            estado = E.ARREMESSO;
            hud.SensorAcendeu(zona);
            hud.Convite(false);
            _emPeAntes = pinos.EmPe();
            bool primeira = demo || placar.bolaAtual == 0;
            jogada = Jogada.Planejar(zona, _emPeAntes, primeira && _emPeAntes.Count == 10, Ajustes.Dificuldade(), Ajustes.ProtecaoCanaleta());
            var a = personagens[_ativo];
            // passo para o lado: a bola sai na linha do sensor (a conta é a das edições Godot)
            float xCorpoG = Mathf.Clamp(-jogada.origemX - X_MAO, -0.85f, 0.55f);
            StartCoroutine(MoverBase(a, -xCorpoG, 0.45f));
            a.Tocar("arremesso", 0.12f, true);
            // corte seco da pista para o mascote arremessando
            var pa = a.transform.position;
            cam.Corte(pa + G(1.15f, 2.05f, 3.3f), pa + G(-0.35f, 0.05f, -7f), 50, 40);
            cam.Seguir(a.transform, G(1.15f, 2.05f, 3.3f), G(-0.35f, 0.05f, -7f), 50, 40, 3f);
            Sons.Tocar("whoosh", 0.5f);
        }

        IEnumerator MoverBase(Personagem a, float x, float dur)
        {
            float de = a.BaseX;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                a.BaseX = Mathf.Lerp(de, x, Tw.Curva(Facil.Seno, t / dur));
                yield return null;
            }
            a.BaseX = x;
        }

        void SoltouBola(Vector3 pos)
        {
            if (estado != E.ARREMESSO) return;
            var a = personagens[_ativo];
            bola.Vestir(_ativo);
            // a bola segue a linha planejada (a mesma da calibração)
            float z0 = Mathf.Min(pos.z, 0.45f);
            jogada.Lancamento(z0, out var origem, out var vel);
            bola.Lancar(origem, vel, jogada.efeito);
            pinos.SoltarTodos();
            estado = E.ROLANDO;
            _tJogada = 0f;
            _tImpacto = -1f;
            _deckCam = false;
            _nGravados = 0;
            _gravando = true;
            mundo.AcenderPista(true);
            a.olharAlvo = bola.transform.position;
            // primeiro por cima (a câmera não atravessa o personagem), depois desce
            cam.Seguir(bola.transform, G(0.5f, 2.1f, 3.6f), G(0f, 0f, -5f), 50, 40, 3f);
            _camBaixa = false;
            // o resto no quadro seguinte (tudo no mesmo quadro dava um tranco na TV Box)
            int s = _seq;
            Tarefas.NoProximoQuadro(() => DepoisDeSoltar(s, a));
        }

        void DepoisDeSoltar(int s, Personagem a)
        {
            if (s != _seq) return;
            a.Tocar("assistindo", 0.25f);
            personagens[_plateia].Tocar("torcida", 0.2f);
            Sons.Tocar("start", 0.35f, 1.4f);
        }

        /// <summary>A bola entrou na caçapa do fundo: barulhinho do túnel e, um pouco depois, ela
        /// aparece no retorno de bolas, perto de quem joga.</summary>
        void BolaRecolhida()
        {
            if (estado == E.ABERTURA || estado == E.TESTE) return;
            Sons.Tocar("whoosh", 0.45f, 0.55f);
            string nome = _ativo;
            Tarefas.Depois(1.3f, () => BolaNoRetorno(nome));
        }

        void BolaNoRetorno(string nome)
        {
            if (mundo == null) return;
            mundo.RetornoBola(Bola.MaterialDe(nome));
            Sons.Tocar("pop", 0.55f, 0.5f);
        }

        // ================================================================ a cada quadro
        void Update()
        {
            Controles.Atualizar();
            Tela.Atualizar();
            // sons de ambiente das fases (Temas): na espera da jogada, na abertura e nas telas —
            // não durante a bola, o resultado, o replay e as fotos
            if (mundo != null && mundo.temas != null)
                mundo.temas.somAmbiente = _menu == null && (estado == E.PRONTO || estado == E.ATRACAO || estado == E.DEMO
                    || estado == E.JOGADORES || estado == E.PERSONAGEM || estado == E.FIM);
            if (_menu != null) return;
            if (Time.frameCount != _quadroFechou) LerEntrada();
            if (_menu != null) return;
            MedirFps();
            OrcamentoBichos();
            AndarLenta();
            float real = Time.unscaledDeltaTime;
            // PC em tela cheia: a setinha do mouse some sozinha quando ninguém mexe
            if (Input.GetAxisRaw("Mouse X") != 0f || Input.GetAxisRaw("Mouse Y") != 0f)
            {
                _mouseParado = 0f;
                Cursor.visible = true;
            }
            else
            {
                _mouseParado += real;
                if (_mouseParado > 2.5f && Cursor.visible) Cursor.visible = false;
            }
            switch (estado)
            {
                case E.ROLANDO:
                    Rolando();
                    break;
                case E.PRONTO:
                    ContarInatividade(real);
                    break;
                case E.JOGADORES:
                    _escolhaT -= real;
                    hud.Jogadores(true, _escolhaN, _escolhaT / TempoJogadores(), Mathf.CeilToInt(_escolhaT));
                    if (_escolhaT <= 0f) ConfirmarJogadores();
                    break;
                case E.PERSONAGEM:
                    _escolhaT -= real;
                    string dica = InicioPelaBola()
                        ? "QUER O OUTRO AMIGO? ROLE A BOLA DO LADO DELE\nbola no MEIO = JOGAR JÁ · sem bola, começa em " + Mathf.CeilToInt(_escolhaT)
                        : "ROLE A BOLA PARA ESCOLHER SEU AMIGO\nbola no MEIO ou START confirma   (" + Mathf.CeilToInt(_escolhaT) + ")";
                    hud.Selecao(true, _destaque, dica, PesNaTela());
                    if (_escolhaT <= 0f) StartCoroutine(ConfirmarPersonagem());
                    break;
                case E.ATRACAO:
                    _demoT += real;
                    // a câmera (permissão, webcam ligada depois) é conferida aqui, na espera
                    _cameraT += real;
                    if (_cameraT > 6f)
                    {
                        _cameraT = 0f;
                        Fotos.NaEspera();
                    }
                    if (Ajustes.Bool("jogo", "demo") && _demoT > 9f)
                    {
                        _demoT = 0f;
                        StartCoroutine(JogadaDemo());
                    }
                    break;
                case E.FIM:
                    _fimT += real;
                    int resta = Mathf.CeilToInt(16f - _fimT);
                    hud.FimContagem(Mathf.Max(resta, 0));
                    if (_fimT > 16f) Atracao();
                    break;
            }
            if (_replayAtivo) RodarReplay(Time.deltaTime);
            foreach (var p in personagens.Values)
                if (p.olharAlvo.HasValue && bola.Visivel) p.olharAlvo = bola.transform.position;
        }

        void Rolando()
        {
            var b = bola.transform.position;
            mundo.bolaZ = -b.z;
            Sons.Rolando(b.z > Medidas.Z_PINO1 - 0.5f, bola.Velocidade());
            foreach (var p in personagens.Values) p.olharAlvo = b;
            if (b.z < Medidas.Z_PINO1 + 3.3f) pinos.Susto(b);
            // câmera: desce para perseguir a bola, depois vai para os pinos
            if (!_camBaixa && b.z < -2.5f)
            {
                _camBaixa = true;
                cam.Seguir(bola.transform, G(0f, 0.85f, 2.5f), G(0f, 0.05f, -4.5f), 52, 40, 2.8f);
            }
            if (!_deckCam && b.z < Medidas.Z_PINO1 + 4.6f)
            {
                _deckCam = true;
                var bg = Medidas.G(b);   // (a conta é a das edições Godot)
                cam.Ir(G(bg.x * 0.25f + 0.32f, 0.9f, Medidas.Z_PINO1 + 3f), G(0f, 0.16f, Medidas.Z_PINO1 - 0.5f), 38, 36, 3.2f);
            }
            // impacto
            if (_tImpacto < 0f && b.z < Medidas.Z_PINO1 + 0.22f && Mathf.Abs(b.x) < Medidas.MEIA_LARGURA) Impacto(b);
            bool fim = false;
            if (_tImpacto >= 0f)
            {
                float dt = _tJogada - _tImpacto;
                fim = dt > 2.8f || (dt > 1.4f && pinos.TodosParados());
            }
            else if (b.z < Medidas.Z_FIM_DECK - 0.4f || b.y < -0.5f) fim = _tJogada > 0.5f;
            if (_tJogada > 7.5f) fim = true;
            if (fim) StartCoroutine(FinalizarJogada());
        }

        void Impacto(Vector3 b)
        {
            _tImpacto = _tJogada;
            bool cheio = Mathf.Abs(b.x) < 0.2f && _emPeAntes.Count >= 6;
            int forte = cheio ? 8 : 3;
            cam.Tremer(0.03f + forte * 0.005f, 0.4f);
            cam.Soco(cheio ? 3.5f : 1.8f);
            efeitos.Poeira(new Vector3(b.x, 0.05f, b.z), 1f + forte * 0.08f);
            if (cheio) Sons.Tocar("pinos_queda", 0.9f);
            if (jogada.StrikeProvavel)
            {
                mundo.ModoLampadas(1);
                CameraLenta(0.4f, 0.5f);
            }
        }

        // ================================================================ gravação (replay)
        struct Quadro
        {
            public float t;
            public Vector3 bp;
            public Quaternion br;
            public bool v;
            public Vector3[] pp;
            public Quaternion[] pr;
        }

        Quadro[] _gravacao = new Quadro[0];
        int _nGravados;
        bool _gravando;

        /// <summary>O relógio da jogada anda junto com a FÍSICA (cada passo gravado tem o seu
        /// próprio instante: o replay sai liso).</summary>
        void FixedUpdate()
        {
            if (estado == E.ROLANDO) _tJogada += Time.fixedDeltaTime;
            if (!_gravando) return;
            if (_nGravados >= _gravacao.Length)
            {
                int n = Mathf.Max(256, _gravacao.Length * 2);
                Array.Resize(ref _gravacao, n);
                for (int i = 0; i < n; i++)
                    if (_gravacao[i].pp == null)
                    {
                        _gravacao[i].pp = new Vector3[10];
                        _gravacao[i].pr = new Quaternion[10];
                    }
            }
            ref var q = ref _gravacao[_nGravados++];
            q.t = _tJogada;
            q.bp = bola.rb.position;
            q.br = bola.rb.rotation;
            q.v = bola.Visivel;
            pinos.Foto(q.pp, q.pr);
        }

        // ================================================================ resultado
        IEnumerator FinalizarJogada()
        {
            estado = E.RESULTADO;
            int s = NovoSeq();
            _gravando = false;
            PararLenta();
            Sons.Rolando(false);
            mundo.AcenderPista(false);
            mundo.ModoLampadas(0);
            var derrubados = pinos.CaidosDesde(_emPeAntes);
            int n = derrubados.Count;
            int festaPinos = pinos.Reagir();
            hud.MostrarMapa(pinos.EmPe());
            var a = personagens[_ativo];
            var p = personagens[_plateia];
            a.olharAlvo = null;
            p.olharAlvo = null;
            var res = new Placar.Resultado { tipo = "normal", pontos = n * Placar.PONTOS_PINO };
            if (!demo)
            {
                res = placar.Registrar(n);
                hud.AtualizarPlacar(placar);
            }
            else
            {
                if (n == _emPeAntes.Count && n == 10) res.tipo = "strike";
                else if (n == 0) res.tipo = "zero";
            }
            string tipo = res.tipo;
            int sobrou = pinos.EmPe().Count;
            switch (tipo)
            {
                case "strike":
                {
                    hud.Flash(Hud.CREME, 0.75f);
                    hud.Letreiro("strike", "", 1.9f);
                    Sons.Tocar("strike");
                    Sons.Tocar("torcida", 0.8f);
                    ArduinoBridge.SendLed("STRIKE");
                    mundo.ModoLampadas(1);
                    int jog = placar.jogadorAtual;
                    string ativo = _ativo;
                    Tarefas.NoProximoQuadro(() => FestaStrike(s, jog, ativo));
                    break;
                }
                case "spare":
                    hud.Flash(Materiais.Hex("#C9F7FF"), 0.55f);
                    hud.Letreiro("spare", "", 1.7f);
                    Sons.Tocar("spare");
                    Sons.Tocar("torcida", 0.6f);
                    ArduinoBridge.SendLed("SPARE");
                    mundo.ModoLampadas(2);
                    Tarefas.NoProximoQuadro(() => { if (s == _seq) efeitos.Estrelas(G(0f, 0.6f, Medidas.Z_PINO1 - 0.4f), 28); });
                    break;
                case "zero":
                    hud.Letreiro("ops", "", 1.5f);
                    Sons.Tocar("ops", 0.75f);
                    ArduinoBridge.SendLed("MISS");
                    break;
                default:
                {
                    string nome = "boa";
                    if (n >= 7) nome = UnityEngine.Random.value < 0.5f ? "super" : "uau";
                    else if (sobrou == 1) nome = "quase";
                    hud.Letreiro(nome, n + " " + (n == 1 ? "PINO" : "PINOS") + "!", 1.5f);
                    Sons.Tocar("boa");
                    ArduinoBridge.SendLed("NORMAL");
                    if (n >= 5) efeitos.Estrelas(G(0f, 0.5f, Medidas.Z_PINO1 - 0.3f), 16, 0.8f);
                    break;
                }
            }
            if (!demo && res.pontos > 0) hud.PontosVoando(res.pontos, placar.jogadorAtual);
            if (tipo == "strike")
            {
                Ajustes.Contar("strikes");
                int seguidos = demo ? 0 : placar.Atual.seguidos;
                if (seguidos >= 2)
                {
                    hud.Sequencia(seguidos);
                    Tarefas.Depois(1f, () => AvisoSeguidos(seguidos));
                }
            }
            // replay do strike
            if (tipo == "strike" && Ajustes.Bool("jogo", "replay_strike") && _nGravados > 20)
            {
                yield return Esperar(1.5f, s, false);
                if (s != _seq) yield break;
                yield return Replay(s);
                if (s != _seq) yield break;
            }
            else
            {
                // se sobrou pino comemorando, a câmera fica um pouco mais nos pinos
                yield return Esperar(festaPinos > 0 ? 1.6f : 1.1f, s);
                if (s != _seq) yield break;
            }
            // reação do personagem (câmera bem de frente)
            var pa = a.transform.position;
            if (tipo == "strike") cam.Ir(pa + G(0.45f, 1.35f, -3.6f), pa + G(0f, 1.2f, 0f), 44, 34, 3f);
            else cam.Ir(pa + G(0.35f, 0.9f, -2.3f), pa + G(0f, 0.72f, 0f), 36, 30, 3.5f);
            switch (tipo)
            {
                case "strike":
                    a.Tocar("comemora_strike", 0.15f, true);
                    p.Tocar("feliz", 0.2f, true);
                    efeitos.Confete(pa + G(0f, 2f, -0.6f), 1.2f, 70);
                    break;
                case "spare":
                    a.Tocar("feliz", 0.15f, true);
                    p.Tocar("feliz", 0.2f, true);
                    break;
                case "zero":
                    a.Tocar("triste", 0.15f, true);
                    p.Tocar("acenar", 0.2f, true);
                    break;
                default:
                    a.Tocar(sobrou == 1 ? "quase" : "feliz", 0.15f, true);
                    break;
            }
            yield return Esperar(tipo == "strike" ? 2.6f : 1.8f, s);
            if (s != _seq) yield break;
            if (demo)
            {
                FimDemo();
                yield break;
            }
            string proximo = placar.Avancar(res.fimRodada);
            bola.Guardar();
            if (proximo == "fim" && placar.PedeDesempate())
            {
                StartCoroutine(Desempate());
                yield break;
            }
            if (proximo == "fim")
            {
                FimDeJogo();
                yield break;
            }
            // a máquina de pinos: barra varre os caídos, a mesa recoloca / desce rack novo
            PlanoMaquina();
            // (o alçapão só abre se esta bola derrubou algum pino)
            if (proximo == "bola") maquina.Reposicionar(n > 0);
            else maquina.RackNovo(n > 0);
            yield return Esperar(0.3f, s, false);
            if (s != _seq) yield break;
            while (maquina.trabalhando)
            {
                yield return Esperar(0.1f, s, false);
                if (s != _seq) yield break;
            }
            hud.MostrarMapa(pinos.EmPe());
            StartCoroutine(Vez(proximo == "rodada"));
        }

        void FestaStrike(int s, int jogador, string ativo)
        {
            if (s != _seq) return;
            if (!demo)
            {
                var ft = Fotos.Foto("j" + jogador);
                if (ft != null) hud.FotoDestaque(ft, ativo == "bob" ? Hud.COR_BOB : Hud.COR_MIA, 1.4f);
            }
            efeitos.Estrelas(G(0f, 0.6f, Medidas.Z_PINO1 - 0.4f), 40, 1.3f);
            efeitos.Confete(G(0f, 0.3f, Medidas.Z_PINO1 + 1.5f), 1.6f, 90);
        }

        void AvisoSeguidos(int seguidos)
        {
            if (estado == E.RESULTADO || estado == E.REPLAY)
            {
                hud.Letreiro("pegando_fogo", seguidos + " STRIKES SEGUIDOS!", 1.6f, 0.55f);
                Sons.Tocar("brilho");
            }
        }

        /// <summary>Câmera de frente para os pinos, baixinha, para ver a máquina trabalhar.</summary>
        void PlanoMaquina()
        {
            cam.Corte(G(0.55f, 0.62f, Medidas.Z_PINO1 + 2.5f), G(0f, 0.5f, Medidas.Z_PINO1 - 0.45f), 40, 40);
            cam.Ir(G(-0.35f, 0.72f, Medidas.Z_PINO1 + 2.2f), G(0f, 0.5f, Medidas.Z_PINO1 - 0.45f), 40, 40, 0.4f);
        }

        /// <summary>O ALÇAPÃO do fosso: corta para o fosso visto de cima enquanto os pinos caem;
        /// depois volta para a mesa repondo os pinos.</summary>
        void AlcapaoMaquina(bool abrindo)
        {
            if (estado != E.RESULTADO || _seq == _seqDesempate) return;
            if (abrindo)
            {
                cam.Corte(G(0.98f, 1.08f, Medidas.ALCAPAO_Z0 - 0.15f), G(-0.12f, -0.45f, Medidas.ALCAPAO_Z1 + 0.75f), 56, 56);
                cam.Ir(G(0.9f, 1f, Medidas.ALCAPAO_Z0 - 0.25f), G(-0.1f, -0.55f, Medidas.ALCAPAO_Z1 + 0.8f), 56, 56, 1f);
            }
            else PlanoMaquina();
        }

        // ================================================================ replay
        /// <summary>Câmera lenta do replay: a MESMA jogada (cada passo da física gravado), mais
        /// devagar — um pouco mais lenta em volta da batida, sempre suave.</summary>
        const float REPLAY_VEL = 0.45f;
        const float REPLAY_VEL_BATIDA = 0.27f;
        const float REPLAY_ANTES = 0.8f;
        const float REPLAY_DEPOIS = 1.6f;
        const float REPLAY_RASTRO = 1f / 60f;
        bool _replayAtivo;
        float _replayT, _replayInicio, _replayFim, _replayRastro;
        readonly Vector3[] _fotoP = new Vector3[10];
        readonly Quaternion[] _fotoR = new Quaternion[10];

        IEnumerator Replay(int s)
        {
            estado = E.REPLAY;
            hud.ModoReplay(true);
            Sons.Tocar("whoosh");
            pinos.PrenderTodos();
            pinos.Replay(true);
            pinos.Reviver();
            _replayInicio = Mathf.Max(_gravacao[0].t, _tImpacto - REPLAY_ANTES);
            _replayFim = Mathf.Min(_tImpacto + REPLAY_DEPOIS, _gravacao[_nGravados - 1].t);
            _replayT = _replayInicio;
            // o rastro já começa comprido (como estava na jogada nesse instante)
            _replayRastro = Mathf.Max(_gravacao[0].t, _replayInicio - 27f * REPLAY_RASTRO);
            _replayAtivo = true;
            bola.Replay(true);
            MostrarQuadro(_replayT);
            bola.Acender(0.8f);
            // 1) câmera rasante colada na bola (corte seco, como na TV)
            var ap = _alvoReplay.position;
            cam.Corte(ap + G(0.42f, 0.16f, 0.75f), ap + G(-0.1f, 0.08f, -1.2f), 40, 46);
            cam.Seguir(_alvoReplay, G(0.42f, 0.16f, 0.75f), G(-0.1f, 0.08f, -1.2f), 40, 46, 12f);
            while (_replayAtivo && _replayT < _tImpacto - 0.12f)
            {
                yield return null;
                if (s != _seq) yield break;
            }
            // 2) no impacto: lateral, baixinho, câmera bem lenta (o giro da órbita espelha junto)
            cam.Corte(G(0.5f, 0.32f, Medidas.Z_PINO1 + 1.1f), G(-0.05f, 0.24f, Medidas.Z_PINO1 - 0.35f), 40, 48);
            cam.Orbitar(G(0f, 0.24f, Medidas.Z_PINO1 - 0.4f), 1.55f, 0.1f, 0.3f, 40, 48);
            while (_replayAtivo && _replayT < _tImpacto)
            {
                yield return null;
                if (s != _seq) yield break;
            }
            // a batida: som (mais grave, em câmera lenta) junto com a imagem
            cam.Tremer(0.02f, 0.5f);
            Sons.Tocar("pinos_queda", 0.8f, 0.85f);
            while (_replayAtivo && _replayT < _tImpacto + 0.65f)
            {
                yield return null;
                if (s != _seq) yield break;
            }
            // 3) abre o plano: a explosão de pinos
            cam.Ir(G(-0.2f, 1.3f, Medidas.Z_PINO1 + 2.1f), G(0f, 0.25f, Medidas.Z_PINO1 - 0.6f), 44, 46, 1.6f);
            while (_replayAtivo)
            {
                yield return null;
                if (s != _seq) yield break;
            }
            bola.Acender(0f);
            bola.Replay(false);
            pinos.Replay(false);
            Sons.Rolando(false);
            pinos.Reagir();
            hud.ModoReplay(false);
            hud.MostrarPlacar(true);
            hud.MostrarSensores(true);
            if (s == _seq) estado = E.RESULTADO;
        }

        float VelReplay(float t) => Mathf.Lerp(REPLAY_VEL_BATIDA, REPLAY_VEL, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.75f, Mathf.Abs(t - _tImpacto))));

        void RodarReplay(float dt)
        {
            float vel = _pular ? 3f : VelReplay(_replayT);
            _replayT = Mathf.Min(_replayT + dt * vel, _replayFim);
            bola.Ritmo(vel);
            // o rastro: pontos nos mesmos instantes da jogada de verdade
            while (_replayRastro + REPLAY_RASTRO <= _replayT)
            {
                _replayRastro += REPLAY_RASTRO;
                QuadroBola(_replayRastro, out var bp, out _, out bool vis);
                if (vis) bola.MarcarRastro(bp);
            }
            var antes = bola.transform.position;
            float dtJogada = dt * vel;
            MostrarQuadro(_replayT);
            // o barulho da bola rolando, mais grave (câmera lenta)
            if (dtJogada > 0f)
            {
                var b = bola.transform.position;
                float v = Vector3.Distance(b, antes) / dtJogada;
                Sons.Rolando(bola.Visivel && b.z > Medidas.Z_PINO1 - 0.5f, v * vel);
            }
            if (_replayT < _tImpacto + 0.1f) pinos.Susto(bola.transform.position);
            if (_replayT >= _replayFim)
            {
                _replayAtivo = false;
                // termina exatamente como ficou de verdade
                var ult = _gravacao[_nGravados - 1];
                bola.Pose(ult.bp, ult.br);
                bola.Ver(ult.v);
                pinos.AplicarFoto(ult.pp, ult.pr);
            }
        }

        /// <summary>Último quadro com tempo &lt;= t (busca binária) e o peso até o seguinte.</summary>
        void Achar(float t, out int lo, out float u)
        {
            int n = _nGravados;
            lo = 0;
            int hi = n - 1;
            while (lo < hi)
            {
                int m = (lo + hi + 1) >> 1;
                if (_gravacao[m].t <= t) lo = m;
                else hi = m - 1;
            }
            int l1 = Mathf.Min(lo + 1, n - 1);
            float t0 = _gravacao[lo].t, t1 = _gravacao[l1].t;
            u = t1 > t0 ? Mathf.Clamp01((t - t0) / (t1 - t0)) : 0f;
        }

        void QuadroBola(float t, out Vector3 pos, out Quaternion rot, out bool vis)
        {
            Achar(t, out int lo, out float u);
            int l1 = Mathf.Min(lo + 1, _nGravados - 1);
            pos = Vector3.Lerp(_gravacao[lo].bp, _gravacao[l1].bp, u);
            rot = Quaternion.Slerp(_gravacao[lo].br, _gravacao[l1].br, u);
            vis = _gravacao[lo].v;
        }

        /// <summary>Põe a bola e os pinos como estavam no instante t da jogada (interpolado entre
        /// os dois passos de física vizinhos).</summary>
        void MostrarQuadro(float t)
        {
            Achar(t, out int lo, out float u);
            int l1 = Mathf.Min(lo + 1, _nGravados - 1);
            var f0 = _gravacao[lo];
            var f1 = _gravacao[l1];
            var bp = Vector3.Lerp(f0.bp, f1.bp, u);
            bola.Pose(bp, Quaternion.Slerp(f0.br, f1.br, u));
            bola.Ver(f0.v);
            _alvoReplay.position = bp;
            for (int k = 0; k < 10; k++)
            {
                _fotoP[k] = Vector3.Lerp(f0.pp[k], f1.pp[k], u);
                _fotoR[k] = Quaternion.Slerp(f0.pr[k], f1.pr[k], u);
            }
            pinos.AplicarFoto(_fotoP, _fotoR);
        }

        // ================================================================ desempate
        /// <summary>DESEMPATE (2 jogadores empatados no fim): letreiro EMPATE! e o DUELO (os cartões
        /// de cada um com os pontos iguais); depois o letreiro DESEMPATE! avisa a rodada extra,
        /// jogada na pista que ficou de fora do sorteio (a 4ª, com 3 rodadas) — a troca de cenário
        /// anuncia a pista e a faixa de cada um diz "DESEMPATE!". Empatou de novo: fim com os dois
        /// campeões.</summary>
        IEnumerator Desempate()
        {
            estado = E.RESULTADO;
            int s = NovoSeq();
            _seqDesempate = s;
            placar.RodadaExtra();
            PedirRoupas(TemaDaRodada());
            hud.MostrarSensores(false);
            hud.MostrarPlacar(true);
            hud.AtualizarPlacar(placar);
            hud.Sequencia(0);
            bola.Fogo(false);
            ArduinoBridge.SendLed("MENU");
            var t = personagens["bob"];
            var r = personagens["mia"];
            foreach (var pp in new[] { t, r })
            {
                pp.SegurarBola(false);
                pp.Jatos(false);
                pp.Destaque(0f);
                pp.olharAlvo = null;
                pp.Expressao("empolgado");
            }
            t.Posicionar(G(-0.62f, 0f, 2.3f), 0f);
            r.Posicionar(G(0.62f, 0f, 2.3f), 0f);
            t.Tocar("quase", 0.25f, true);
            r.Tocar("quase", 0.25f, true);
            cam.Corte(G(0f, 1.3f, 5.6f), G(0f, 1f, 2.3f), 40, 44);
            // a mesa já arruma os 10 pinos para a rodada extra (fora do quadro: o alçapão não
            // puxa a câmera nesta sequência)
            maquina.RackNovo();
            hud.Flash(Color.white, 0.7f, 0.4f);
            Sons.Tocar("whoosh");
            Sons.Tocar("brilho");
            hud.Letreiro("empate", "", 2.6f, 0.25f);
            var js = new List<Hud.Duelista>();
            for (int i = 0; i < placar.jogadores.Count; i++)
                js.Add(new Hud.Duelista { personagem = placar.jogadores[i].personagem, pontos = placar.jogadores[i].pontos, foto = Fotos.Foto("j" + i) });
            hud.Duelo(js, 4.4f);
            yield return Esperar(2.6f, s, false);
            if (s != _seq) yield break;
            t.Tocar("torcida", 0.25f, true);
            r.Tocar("torcida", 0.25f, true);
            Sons.Tocar("torcida");
            Sons.Tocar("vez");
            string onde = placar.rodadasNormais < Temas.ORDEM.Length ? "NA PISTA QUE FALTAVA!" : "VALENDO A VITÓRIA!";
            hud.Letreiro("desempate", "RODADA EXTRA " + onde, 3f, 0.25f);
            yield return Esperar(3f, s, false);
            if (s != _seq) yield break;
            while (maquina.trabalhando)
            {
                yield return Esperar(0.1f, s, false);
                if (s != _seq) yield break;
            }
            hud.MostrarMapa(pinos.EmPe());
            StartCoroutine(Vez(true));
        }

        // ================================================================ fim de jogo
        void FimDeJogo()
        {
            estado = E.FIM;
            // fim da partida: a porta FECHA (as bolas da última jogada voltam e ficam); no modo
            // livre pela bola fica aberta (a bola começa outra)
            ArduinoBridge.Porta(InicioPelaBola());
            NovoSeq();
            _fimT = 0f;
            hud.MostrarPlacar(false);
            hud.MostrarSensores(false);
            Sons.Musica("musica_fim");
            Sons.Tocar("fim");
            ArduinoBridge.SendLed("MENU");
            var dados = new Hud.DadosFim { vencedor = placar.Vencedor(), desempate = placar.desempate };
            int melhor = 0, melhorI = 0;
            string quem = "";
            for (int i = 0; i < placar.jogadores.Count; i++)
            {
                var j = placar.jogadores[i];
                dados.jogadores.Add(new Hud.JogadorFim
                {
                    personagem = j.personagem, pontos = j.pontos, estrelas = placar.Estrelas(i), strikes = j.strikes, spares = j.spares,
                    foto = Fotos.Foto("j" + i),
                });
                if (j.pontos > melhor)
                {
                    melhor = j.pontos;
                    melhorI = i;
                    quem = j.personagem;
                }
            }
            int nj = placar.jogadores.Count;
            if (Ajustes.RegistrarPontos(nj, melhor, quem) && melhor > 0)
            {
                string caminho = Fotos.SalvarRecorde("j" + melhorI, nj);
                if (!string.IsNullOrEmpty(caminho)) Ajustes.DefinirFotoRecorde(nj, caminho);
                dados.recorde = true;
                dados.recordeValor = melhor;
                Tarefas.Depois(3.3f, () => Sons.Tocar("recorde"));
            }
            // classificação do dia, com a foto de cada um
            var entradas = new List<Classificacao.Entrada>();
            for (int i = 0; i < nj; i++)
            {
                var j = placar.jogadores[i];
                entradas.Add(new Classificacao.Entrada
                {
                    pontos = j.pontos, personagem = j.personagem, chaveFoto = Fotos.Foto("j" + i) != null ? "j" + i : "",
                });
            }
            var posicoes = Classificacao.Registrar(entradas);
            for (int i = 0; i < posicoes.Count && i < dados.jogadores.Count; i++) dados.jogadores[i].posicao = posicoes[i];
            dados.classificacao = Classificacao.Lista();
            hud.TelaFim(true, dados);
            // personagens no "pódio"
            var t = personagens["bob"];
            var r = personagens["mia"];
            bola.Guardar();
            foreach (var pp in new[] { t, r })
            {
                pp.SegurarBola(false);
                pp.olharAlvo = null;
            }
            t.Posicionar(G(-0.62f, 0f, 2.3f), 0f);
            r.Posicionar(G(0.62f, 0f, 2.3f), 0f);
            int venc = dados.vencedor;
            bool jogouBob = false, jogouMia = false;
            foreach (var j in placar.jogadores)
            {
                if (j.personagem == "bob") jogouBob = true;
                else jogouMia = true;
            }
            if (nj == 2 && venc >= 0)
            {
                var ganhador = personagens[placar.jogadores[venc].personagem];
                var outro = ganhador == t ? r : t;
                ganhador.Tocar("vitoria", 0.3f, true);
                ganhador.Jatos(true);
                ganhador.Expressao("empolgado");
                outro.Tocar("feliz", 0.3f, true);
            }
            else
            {
                t.Ocioso(true);
                r.Ocioso(true);
                Personagem prot = jogouBob && !jogouMia ? t : (jogouMia && !jogouBob ? r : null);
                if (prot != null && placar.Estrelas(0) >= 2)
                {
                    prot.Tocar("vitoria", 0.3f, true);
                    prot.Jatos(true);
                }
            }
            cam.Ir(G(0f, 1.35f, 6.6f), G(0f, 1.1f, 2.3f), 40, 44, 2f);
            efeitos.Confete(G(0f, 3.2f, 2.3f), 2.2f, 160);
            efeitos.Fogos(G(0f, 3.8f, -2f), 7);
        }

        // ================================================================ demo
        IEnumerator JogadaDemo()
        {
            estado = E.DEMO;
            demo = true;
            Sons.MudoDemo = !Ajustes.Bool("som", "som_na_demo");
            Sons.AtualizarVolume();
            int s = NovoSeq();
            _ativo = _demoVez % 2 == 0 ? "bob" : "mia";
            _plateia = _ativo == "bob" ? "mia" : "bob";
            _demoVez++;
            maquina.Instantaneo();
            var a = personagens[_ativo];
            var p = personagens[_plateia];
            a.Ocioso(false);
            p.Ocioso(false);
            a.Posicionar(G(-X_MAO, 0f, Z_INICIO), 180f);
            a.SegurarBola(true);
            a.Tocar("pronto", 0.1f, true);
            float lado = _ativo == "bob" ? 1f : -1f;
            p.Posicionar(G(lado * 1.85f, 0f, 1f), -(180f - lado * 38f));
            p.Tocar("torcida", 0.2f, true);
            PlanoPista(true);
            yield return Esperar(1.6f, s, false);
            if (s != _seq) yield break;
            string[] zonas = { "C", "C", "X", "V", "C", "Z", "B", "C" };
            _ultimaJogada = -1000f;
            estado = E.DEMO;
            Arremessar(zonas[UnityEngine.Random.Range(0, zonas.Length)]);
        }

        void FimDemo()
        {
            demo = false;
            Atracao();
            // a atração vai mostrando os 4 cenários
            hud.Flash(Color.white, 0.8f, 0.5f);
            mundo.AplicarTema(Temas.Proximo(mundo.tema));
            NoClarao();
        }

        // ================================================================ inatividade
        void ContarInatividade(float dt)
        {
            if (demo) return;
            if (_modalInativo > 0f)
            {
                _modalInativo -= dt;
                hud.Inatividade(true, Mathf.CeilToInt(_modalInativo));
                if (_modalInativo <= 0f)
                {
                    hud.Inatividade(false);
                    FimDeJogo();
                }
                return;
            }
            _inativo += dt;
            if (_inativo > Ajustes.Float("jogo", "tempo_inatividade"))
            {
                _modalInativo = 10f;
                Sons.Tocar("tique");
            }
        }

        void FecharInatividade()
        {
            _modalInativo = 0f;
            _inativo = 0f;
            hud.Inatividade(false);
        }

        // ================================================================ sons do mundo
        void BatidaPino(Vector3 pos, float forca)
        {
            if (estado == E.REPLAY) return;
            float agora = Time.realtimeSinceStartup;
            if (agora - _ultimoSomPino < 0.035f) return;
            _ultimoSomPino = agora;
            Sons.Tocar("pino_batida", Mathf.Clamp(forca / 4f, 0.25f, 1f), UnityEngine.Random.Range(0.9f, 1.15f));
            if (forca > 2.5f) efeitos.Poeira(pos, 0.5f);
        }

        void BolaBateu(Collider c, float vel)
        {
            if (c == null || c.attachedRigidbody != null || vel <= 0.8f) return;
            // "boing" só nos para-choques (como a edição Godot): no resto a bola rola ou cai
            if (c.name != "bumper_fisica") return;
            Sons.Tocar("boing", Mathf.Clamp(vel / 6f, 0.4f, 1f));
            cam.Tremer(0.012f, 0.15f);
        }

        void SomMaquina(string nome)
        {
            if (estado == E.ATRACAO || demo) return;
            switch (nome)
            {
                case "varrer": Sons.Tocar("whoosh", 0.45f, 0.7f); break;
                case "mesa": Sons.Tocar("whoosh", 0.25f, 1.3f); break;
                case "pousar":
                    Sons.Tocar("pinos_queda", 0.3f, 1.25f);
                    Sons.Tocar("pop", 0.4f, 0.9f);
                    break;
                case "barra": Sons.Tocar("tique", 0.35f, 0.7f); break;
                case "alcapao":
                    Sons.Tocar("tique", 0.4f, 0.5f);
                    Sons.Tocar("pinos_queda", 0.22f, 0.72f);
                    break;
            }
        }

        void SomPersonagem(string nome)
        {
            switch (nome)
            {
                case "passo": Sons.Tocar("tique", 0.25f, UnityEngine.Random.Range(0.8f, 1f)); break;
                case "palma": Sons.Tocar("pop", 0.6f, UnityEngine.Random.Range(1.1f, 1.3f)); break;
                case "jato": Sons.Tocar("start", 0.7f, 0.9f); break;
            }
        }

        // ================================================================ menu do operador
        /// <summary>MENU POR CIMA DO JOGO: o botão de CONFIGURAÇÃO abre o menu do operador por cima
        /// do jogo PAUSADO — nada é desmontado, a imagem 3D só para de ser desenhada. Só o
        /// "Cenário (fundo)" pede montar o jogo de novo.</summary>
        public void AbrirMenu()
        {
            if (_menu != null) return;
            NovoSeq();     // o que estava acontecendo para (demo, jogada, replay...)
            PararLenta();
            Sons.Rolando(false);
            _gravando = false;
            _replayAtivo = false;
            estado = E.OPERADOR;
            _antesMenu = AjustesQueRemontam();
            Time.timeScale = 0f;
            cam.cam.enabled = false;
            Ui.Ver(hud.Raiz, false);
            _menu = Operador.Abrir(_canvas.transform, this);
        }

        public void FecharMenu()
        {
            if (_menu == null) return;
            if (AjustesQueRemontam() != _antesMenu)
            {
                // o mundo é montado de outro jeito: monta de novo, com aviso na tela
                StartCoroutine(Remontar());
                return;
            }
            Destroy(_menu.gameObject);
            _menu = null;
            _quadroFechou = Time.frameCount;
            // o nível do cenário 3D troca na hora (é só ligar um nó e desligar o outro)
            if (mundo != null && mundo.temas != null && Ajustes.Cenario3dLeve() != mundo.temas.cenarioLeve)
                mundo.temas.NivelCenario(Ajustes.Cenario3dLeve());
            Ui.Ver(hud.Raiz, true);
            cam.cam.enabled = true;
            _parado3d = false;
            _fpsT0 = -1f;
            // ajustes que valem na hora
            _velocidade = Ajustes.Velocidade();
            Ritmo(1f);
            string q = QualidadeEscolhida();
            if (q != _qualidade) _qualidade = q;
            LerNitidez();
            TelaMudou();
            if (Ajustes.Bool("video", "medidor") != medidor.Visivel) medidor.Mostrar(Ajustes.Bool("video", "medidor"));
            if (_resultadoTeste != null)
            {
                Destroy(_resultadoTeste.gameObject);
                _resultadoTeste = null;
            }
            // a abertura pode ter ficado pela metade
            hud.TarjasCinema(false);
            hud.Marca(false, true);
            bola.Fogo(false);
            bola.Vestir("bob");
            mundo.bolaZ = 100f;
            hud.Flash(Color.white, 0.5f, 0.3f);
            NoClarao();
            foreach (var p in personagens.Values) p.Visivel = true;
            if (Ajustes.Bool("video", "testar"))
            {
                Ajustes.Definir("video", "testar", false);
                Atracao();
                TesteDesempenho();
            }
            else Atracao();
        }

        public bool MenuAberto => _menu != null;

        /// <summary>O que muda a MONTAGEM do jogo (e não dá para trocar com ele montado).</summary>
        string AjustesQueRemontam() => "cenario=" + Ajustes.CenarioLeve() + "|detalhes=" + Ajustes.Detalhes();

        IEnumerator Remontar()
        {
            var T = Tela.Tamanho;
            var fundo = Ui.Retangulo(_canvas.transform, new Color(0.03f, 0.09f, 0.22f));
            var c = fundo.gameObject.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = 100;
            Ui.Ret(fundo, 0, 0, T.x, T.y);
            ((RectTransform)fundo.transform).anchorMin = ((RectTransform)fundo.transform).anchorMax = new Vector2(0.5f, 0.5f);
            ((RectTransform)fundo.transform).anchoredPosition = Vector2.zero;
            var l = Ui.Rotulo(fundo.transform, "APLICANDO OS AJUSTES...\naguarde um instante", 54, Materiais.Hex("#FFD43B"), 10, Ui.MARINHO);
            Ui.Ret(l, 0, 0, T.x, T.y);
            Ajustes.GravarJa();
            yield return null;
            yield return null;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ================================================================ utilidades
        /// <summary>Interrompe o que estiver acontecendo (demo, replay, câmera lenta...).</summary>
        void Limpar()
        {
            _replayAtivo = false;
            maquina.Instantaneo();
            pinos.Replay(false);
            _gravando = false;
            PararLenta();
            Sons.Rolando(false);
            mundo.AcenderPista(false);
            mundo.ModoLampadas(0);
            bola.Acender(0f);
            bola.Replay(false);
            bola.Fogo(false);
            bola.Guardar();
            hud.ModoReplay(false);
            hud.Inatividade(false);
            hud.FotoTela(false);
            Fotos.Desligar();
            mundo.Podios(false);
            _modalInativo = 0f;
            hud.Marca(false, true);
            foreach (var p in personagens.Values)
            {
                p.Visivel = true;
                p.Ocioso(false);
                p.Jatos(false);
                p.Destaque(0f);
                p.olharAlvo = null;
                p.SegurarBola(false);
            }
        }

        // ================================================================ nitidez x lisura (sozinho)
        /// <summary>A cada 2 s mede os quadros por segundo de verdade E as travadinhas. Ruim duas
        /// vezes seguidas, alivia: na automática ALTA passa para a MÉDIA; depois a imagem 3D
        /// diminui 10% (até 70%); ainda muito lento: qualidade BAIXA. Com folga, a imagem VOLTA A
        /// CRESCER 5% (sem passar, por 90 s, de um tamanho que já se mostrou pesado). As trocas
        /// ficam para o próximo clarão (o tranco de refazer a imagem fica escondido).</summary>
        bool _quadroFolgado = true, _quadroRuim;

        /// <summary>O "GERENTE DE POLÍGONOS" dos bichinhos (o _orcamento_bichos da edição Godot):
        /// o nível de PERTO (as malhas "_hd", ~2,5x) só para o bichinho maior na tela, inteiro
        /// nela, e só com o quadro folgado — na comemoração cabe; com o cenário inteiro atrás e
        /// a placa no limite, não. Já de perto, fica enquanto o quadro não ficar ruim.</summary>
        void OrcamentoBichos()
        {
            if (!Ajustes.Detalhes() || _parado3d || _menu != null) return;
            Personagem maior = null;
            foreach (var p in personagens.Values)
                if (p.CandidatoPerto() && (maior == null || p.TamanhoNaTela > maior.TamanhoNaTela)) maior = p;
            foreach (var p in personagens.Values)
            {
                bool quer = p == maior;
                if (quer && !p.podePerto) quer = _quadroFolgado;
                else if (quer) quer = !_quadroRuim;
                p.PermitirPerto(quer);
            }
        }

        void MedirFps()
        {
            if (!_adaptar) return;
            const float alvo = 60f;
            float agora = Time.realtimeSinceStartup;
            if (_qUltimo > 0f && agora - _qUltimo > 1.5f / alvo) _qLentos++;
            _qUltimo = agora;
            // nunca no meio da jogada: a troca fica para a espera da próxima bola
            if (estado == E.ARREMESSO || estado == E.ROLANDO || estado == E.RESULTADO || estado == E.REPLAY) return;
            if (_fpsT0 < 0f || estado == E.ABERTURA || estado == E.TESTE || _parado3d)
            {
                _fpsT0 = agora;
                _fpsQ = Time.frameCount;
                _qLentos = 0;
                return;
            }
            if (agora - _fpsT0 < 2f) return;
            int quadros = Mathf.Max(Time.frameCount - _fpsQ, 1);
            float fps = quadros / (agora - _fpsT0);
            float atrasados = (float)_qLentos / quadros;
            _fpsT0 = agora;
            _fpsQ = Time.frameCount;
            _qLentos = 0;
            if (_qualidadePendente != "" || _fatorPendente > 0f) return;
            bool auto = Ajustes.Texto("video", "qualidade") == "auto";
            // (o orçamento do bichinho de perto: só com o quadro folgado)
            _quadroFolgado = fps >= alvo * 0.97f && atrasados < 0.01f;
            _quadroRuim = fps < alvo * 0.9f || atrasados > 0.04f;
            if (fps < alvo * 0.9f || atrasados > 0.04f)
            {
                _fpsBons = 0;
                _fpsRuins++;
                if (_fpsRuins < 2) return;
                _fpsRuins = 0;
                if (Ajustes.Texto("video", "cenario3d") == "auto" && mundo != null && mundo.temas != null && !mundo.temas.cenarioLeve)
                {
                    // 0) o CENÁRIO 3D mais leve (governador do quadro da edição Godot 7.0): um
                    // passo mais leve em cada peça do cenário — trocar é só mostrar um lote e
                    // esconder outro; fica guardado para as próximas vezes
                    mundo.temas.NivelCenario(true);
                    Ajustes.Definir("video", "cenario3d_medido", "leve");
                    Debug.Log($"Cenário 3D automático: leve ({fps:0} quadros/s, {atrasados * 100f:0}% atrasados)");
                }
                else if (auto && _qualidade == "alta") _qualidadePendente = "media";
                else if (_fator > FATOR_MIN + 0.01f)
                {
                    _teto = _fator;
                    _tetoAte = agora + 90f;
                    _fatorPendente = Mathf.Max(FATOR_MIN, _fator - 0.1f);
                }
                else if (auto && _qualidade != "baixa" && fps < alvo * 0.75f)
                {
                    _qualidadePendente = "baixa";
                    _fatorPendente = 1f;
                }
            }
            else if (fps >= alvo * 0.97f && atrasados < 0.01f)
            {
                _fpsRuins = 0;
                _fpsBons++;
                if (_fpsBons >= 4 && _fator < 1f)
                {
                    _fpsBons = 0;
                    float limite = agora < _tetoAte ? _teto - 0.01f : 1f;
                    float novo = Mathf.Min(Mathf.Min(1f, _fator + 0.05f), limite);
                    if (novo > _fator + 0.001f) _fatorPendente = novo;
                }
            }
            else
            {
                _fpsRuins = 0;
                _fpsBons = 0;
            }
        }

        /// <summary>Um clarão branco cobre a tela agora: a troca de resolução pendente acontece aqui.</summary>
        void NoClarao()
        {
            bool mudou = false;
            if (_qualidadePendente != "")
            {
                _qualidade = _qualidadePendente;
                _qualidadePendente = "";
                _qualidadeSalvar = Ajustes.Texto("video", "qualidade") == "auto";
                mudou = true;
            }
            if (_fatorPendente > 0f)
            {
                _fator = _fatorPendente;
                _fatorPendente = -1f;
                _fatorSalvar = true;
                mudou = true;
            }
            if (mudou) TelaMudou();
            _fpsT0 = -1f;
        }
    }
}
