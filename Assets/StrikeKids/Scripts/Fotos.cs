using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif
using Debug = UnityEngine.Debug;

namespace StrikeKids
{
    /// <summary>
    /// A CÂMERA DA MÁQUINA (webcam USB, ex.: a câmera Sigma ligada na TV Box) e
    /// as FOTOS DAS CRIANÇAS. Quando a partida começa, cada jogador tira uma
    /// foto ("SORRIA!" 3-2-1) e o rostinho aparece no placar, na faixa
    /// "VEZ DE", no strike, na CLASSIFICAÇÃO do fim e na atração.
    ///
    /// De onde vem a imagem (escolhido sozinho):
    ///   * TV Box (Android): StrikeCamera.java (CameraAndroid.cs), o mesmo
    ///     método do jogo de soco: câmera do sistema (API clássica, por onde as
    ///     TV Box publicam a webcam USB), depois Camera2 e, se a TV Box não
    ///     publicar a webcam, a UVC DIRETA (libuvc, pelo USB). Se um caminho
    ///     abre mas não manda imagem, o Java passa para o próximo sozinho.
    ///     Como no jogo de soco, a câmera ACORDA 15 s depois da abertura e
    ///     FICA LIGADA (sem ninguém vendo, só conta os quadros — não pesa): a
    ///     foto sai na hora, sem esperar a webcam "esquentar", e o driver USB
    ///     não fica abrindo e fechando. Sem imagem, reabre do zero só na tela
    ///     de espera, no máximo a cada 30 s e 3 vezes.
    ///     PERMISSÕES SÓ NA PRIMEIRA VEZ: a da câmera é pedida UMA vez (na tela
    ///     de espera — NaEspera —, com o aviso na tela "toque em PERMITIR":
    ///     evento JanelaAndroid; o Android guarda a resposta para sempre). A da
    ///     webcam USB só serve para a UVC direta e também é pedida uma vez só
    ///     (marque "Usar por padrão"). Depois disso o jogo nunca mais pede
    ///     sozinho: só o operador, no TESTE DA CÂMERA do menu
    ///     (PedirPermissaoAgora). Guardado em fotos/camera.json;
    ///   * PC / Editor: a WebCamTexture da Unity (CameraWebcam.cs);
    ///   * teste: STRIKE_CAMERA_FALSA=1 (ou --camera-falsa) — CameraFalsa.cs.
    /// Sem câmera, o jogo segue normalmente com os mascotes no lugar das fotos.
    ///
    /// A imagem passa pelo shader StrikeKids/Camera (converte YUV/RGB, recorta
    /// o quadrado do meio, espelha e gira se a câmera estiver de lado) numa
    /// RenderTexture de 384x384 (AoVivo) — que é a foto (CameraImagem.cs).
    ///
    /// As fotos ficam SÓ na máquina (persistentDataPath/fotos/), em PNG gravado
    /// e lido numa linha de trabalho (CameraPng.cs): nada de tranco no fim da
    /// partida nem na tela de espera.
    ///
    /// Todo o estado é estático; o objeto (Criar) anda o Update. Nenhum método
    /// deixa escapar exceção (a câmera nunca derruba o jogo).
    /// </summary>
    public class Fotos : MonoBehaviour
    {
        public static Fotos I { get; private set; }
        public const int LADO = 384;

        public static event Action Mudou;
        /// <summary>Aviso na tela antes da janela de permissão do Android ("" = some).</summary>
        public static event Action<string> JanelaAndroid;

        /// <summary>procurando | desligada | sem_permissao | sem_permissao_usb | sem_camera | pronta</summary>
        public static string Status = "procurando";
        public static string NomeCamera = "";
        /// <summary>De onde vem a imagem: "android" (TV Box), "webcam" (PC), "falsa" (teste) ou "".</summary>
        public static string Fonte { get; private set; } = "";

        // "j0", "j1" -> foto desta partida (e a mesma foto em RGB24 de baixo para cima, para gravar o PNG)
        static readonly Dictionary<string, Texture2D> _fotos = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, byte[]> _imagens = new Dictionary<string, byte[]>();
        // fotos gravadas (recorde, classificação): caminho -> textura (null = o arquivo não existe)
        static readonly Dictionary<string, Texture2D> _guardadas = new Dictionary<string, Texture2D>();

        const string ANDROID = "android", WEBCAM = "webcam", FALSA = "falsa";
        const string DICA_CAMERA = "A FOTO DAS CRIANÇAS USA A CÂMERA: NA JANELA DO ANDROID, TOQUE EM \"PERMITIR\"\nela só aparece desta vez";
        const long JANELA_MAX_MS = 25000;
        const long ESPERA_JANELA_MS = 600;    // o aviso aparece antes da janela (que congela a imagem do jogo)
        const int W_CAM = 640, H_CAM = 480;   // o tamanho pedido à câmera
        /// <summary>no PC a câmera que não foi achada é procurada de novo no máximo a cada 15 s</summary>
        const long BUSCA_MS = 15000;
        const long INICIAR_MS = 1000;         // a câmera é procurada logo depois da abertura começar
        const long DESPERTAR_MS = 15000;      // no Android ela acorda (e fica ligada) 15 s depois
        const long ESPERA_USB_MS = 20000;     // janela USB só depois de 20 s sem câmera do sistema
        const long REABRIR_MS = 30000;        // sem imagem: reabre no máximo a cada 30 s...
        const int REABERTURAS_MAX = 3;        // ...e no máximo 3 vezes (o driver USB não gosta)
        const long QUADRO_MS = 60;            // a prévia sobe no máximo ~16 quadros por segundo
        const long FOTO_MAX_MS = 4000;        // a foto espera um quadro bom até 4 s

        static string _pasta;
        static CameraImagem _imagem;
        static CameraFalsa _falsa;
        static bool _ligada;                  // a câmera está aberta
        static bool _vendo;                   // alguém vendo (prévia/foto): os quadros viram imagem
        static float _desligarEm = -1f;
        static long _buscaMs = -BUSCA_MS;
        static long _iniciarEm = -1, _acordarEm = -1, _abrirJanelaEm = -1;
        static bool _sempre;                  // Android: acordou, fica ligada direto
        static long _buscaDesdeMs = -1;       // quando a procura no Android começou
        static bool _sistemaVisto;            // a webcam já apareceu como câmera do sistema
        static long _abertaMs;
        static long _vivoMs;                  // quando chegou quadro pela última vez
        static int _ultimoN = -1;
        static float _vigiaT;
        static int _reaberturas;
        static long _reabertaMs = -1000000;
        static bool _pedirAgora;              // o operador pediu (TESTE DA CÂMERA no menu)
        static long _janelaAte;               // a janela do Android está aberta (até este ms)
        static bool _procurando;
        static long _procurandoDesde;
        static int _listagemVista;
        static int _quadroVisto = -1;         // >= 0: a imagem ao vivo já tem quadro desta ligada
        static long _quadroMs;
        static int _tentado = -1;             // o quadro em que a foto já foi tentada
        static float _falsaT, _falsaQuadroT;
        static int _chaveDesenho = -1;
        static long _ultimoErro = -100000;
        static int _respostaPermissao;        // (do Java) 1 = deu, 2 = negou
        static Dictionary<string, object> _lembrancas;
        static volatile bool _pngProprio = true;
        static readonly Stopwatch _relogio = Stopwatch.StartNew();
#if UNITY_ANDROID && !UNITY_EDITOR
        static CameraAndroid _android;
        static bool _tentouAndroid;
        static PermissionCallbacks _respostas;
#else
        static CameraWebcam _webcam;
        static string _webcamNome = "";
#endif
        // o que foi desenhado por último (para redesenhar quando o operador gira/espelha)
        static Texture _dPrincipal, _dU, _dV;
        static bool _dYuv, _dDeBaixo;
        static int _dLargura, _dAltura, _dGiroExtra;

        public static void Criar()
        {
            if (I != null) return;
            var go = new GameObject("Fotos");
            DontDestroyOnLoad(go);
            I = go.AddComponent<Fotos>();
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
                PastaFotos();
                if (_imagem == null) _imagem = new CameraImagem(LADO);
                if (_falsa == null) _falsa = CameraFalsa.DoAmbiente();
                long agora = Agora();
                _iniciarEm = agora + INICIAR_MS;
                _acordarEm = agora + DESPERTAR_MS;
                LerFotosGuardadas();
            }
            catch (Exception e)
            {
                Falhou("Awake", e);
            }
        }

        static long Agora() => _relogio.ElapsedMilliseconds;

        public static bool Pronta() => Status == "pronta";

        public static string TextoStatus()
        {
            switch (Status)
            {
                case "pronta":
#if UNITY_ANDROID && !UNITY_EDITOR
                    if (Fonte == ANDROID && _android != null && _ligada) return "OK (" + NomeCamera + ") - " + _android.Estado();
#endif
                    return "OK (" + NomeCamera + ")";
                case "desligada":
                    return "desligada no menu";
                case "sem_permissao":
                    return "sem permissão do Android (aceite a janela da câmera)";
                case "sem_permissao_usb":
                    return "webcam no USB: aceite a janela do Android (marque Usar por padrão)";
                case "sem_camera":
#if UNITY_ANDROID && !UNITY_EDITOR
                    return "nenhuma câmera encontrada";
#else
                    return "nenhuma webcam encontrada (teste sem webcam: STRIKE_CAMERA_FALSA=1)";
#endif
            }
            return "procurando...";
        }

        // ================================================================ iniciar / procurar
        public static void Iniciar()
        {
            try
            {
                IniciarAgora();
            }
            catch (Exception e)
            {
                Falhou("Iniciar", e);
            }
        }

        static void IniciarAgora()
        {
            if (!Ajustes.Bool("jogo", "foto"))
            {
                Parar();
                Mudar("desligada");
                return;
            }
            if (Status == "desligada") Mudar("procurando");
            if (_falsa != null)
            {
                Fonte = FALSA;
                NomeCamera = CameraFalsa.NOME;
                Mudar("pronta");
                return;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!TemPermissao())
            {
                // a janela do Android só na tela de espera (NaEspera), uma vez
                Mudar("sem_permissao");
                return;
            }
            if (Android() != null)
            {
                Fonte = ANDROID;
                // a lista chega sem parar o jogo e então a câmera liga (ListaChegou)
                ProcurarAndroidSemParar();
                return;
            }
            Fonte = "";
            NomeCamera = "";
            Mudar("sem_camera");
#else
            Fonte = WEBCAM;
            _buscaMs = Agora();
            if (CameraWebcam.Escolher(out string dispositivo))
            {
                if (_ligada && _webcam != null && dispositivo != _webcamNome)
                {
                    // outra webcam virou a escolhida: a ligada fecha (religa no próximo Ligar)
                    _webcam.Fechar();
                    _ligada = false;
                }
                _webcamNome = dispositivo;
                NomeCamera = dispositivo;
                Mudar("pronta");
            }
            else
            {
                NomeCamera = "";
                Mudar("sem_camera");
            }
#endif
        }

        /// <summary>Procura de novo agora (o jogo chama ao começar a escolha dos jogadores:
        /// alguém pode ter ligado a câmera depois).</summary>
        public static void ProcurarAgora()
        {
            if (Status != "pronta" && Status != "desligada")
            {
                _buscaMs = Agora();
                Iniciar();
            }
        }

        static bool TemPermissao()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                return Permission.HasUserAuthorizedPermission(Permission.Camera);
            }
            catch (Exception)
            {
                return false;
            }
#else
            return true;
#endif
        }

        // ---------------------------------------------------------------- janela do Android
        /// <summary>A janela do Android "Permitir que o Strike Kids tire fotos?": UMA vez
        /// sozinha (na espera); depois, só quando o operador pede.</summary>
        static void PedirPermissao()
        {
            if (JanelaAberta() || ArduinoBridge.JanelaAberta()) return;   // uma janela do Android por vez
            if (_pedirAgora) _pedirAgora = false;
            else if (Lembrado("permissao_pedida")) return;
            Lembrar("permissao_pedida");
            long agora = Agora();
            _janelaAte = agora + JANELA_MAX_MS;
            // a janela do Android congela a imagem do jogo: o aviso entra antes
            Avisar(DICA_CAMERA);
            _abrirJanelaEm = agora + ESPERA_JANELA_MS;
        }

        static void AbrirJanelaAndroid()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (_respostas == null)
                {
                    // (a resposta chega numa linha do Java: só anota; o Update trata)
                    _respostas = new PermissionCallbacks();
                    _respostas.PermissionGranted += _ => Interlocked.Exchange(ref _respostaPermissao, 1);
                    _respostas.PermissionDenied += _ => Interlocked.Exchange(ref _respostaPermissao, 2);
                    _respostas.PermissionDeniedAndDontAskAgain += _ => Interlocked.Exchange(ref _respostaPermissao, 2);
                }
                Permission.RequestUserPermission(Permission.Camera, _respostas);
            }
            catch (Exception e)
            {
                Falhou("permissão da câmera", e);
                FechouJanela();
            }
#endif
        }

        /// <summary>O operador pediu (TESTE DA CÂMERA, ou ligou a foto no menu): a janela
        /// que faltar (câmera ou webcam USB) pode abrir de novo.</summary>
        public static void PedirPermissaoAgora()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                _pedirAgora = true;
                if (!TemPermissao())
                {
                    Mudar("sem_permissao");
                    PedirPermissao();
                }
                else if (Status == "sem_permissao_usb" && Android() != null) PedirUsb(Agora());
                else _pedirAgora = false;
            }
            catch (Exception e)
            {
                Falhou("PedirPermissaoAgora", e);
            }
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

        void OnApplicationFocus(bool foco)
        {
            // o jogo voltou para a frente: a janela do Android fechou
            if (foco) FechouJanela();
        }

        void OnApplicationPause(bool pausa)
        {
            if (!pausa) FechouJanela();
        }

        static void Mudar(string s)
        {
            if (s == Status) return;
            Status = s;
            try { Mudou?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
        }

        // ================================================================ Android
#if UNITY_ANDROID && !UNITY_EDITOR
        static CameraAndroid Android()
        {
            if (_android == null && !_tentouAndroid)
            {
                _tentouAndroid = true;
                _android = CameraAndroid.Criar();
                if (_android == null) Debug.LogWarning("Fotos: este APK não tem a ponte da câmera (StrikeCamera.java)");
            }
            return _android;
        }

        static void ProcurarAndroid(string lista)
        {
            if (_buscaDesdeMs < 0) _buscaDesdeMs = Agora();
            string melhor = "";
            bool usbSemPermissao = false;
            foreach (string bruta in lista.Split('\n'))
            {
                string linha = bruta.Trim();
                if (linha.Length == 0 || linha[0] == '#') continue;
                string[] p = linha.Split('|');
                if (p.Length < 3) continue;
                if (p[2] == "uvc")
                {
                    // webcam vista direto no USB (UVC): só serve com a permissão USB
                    if (p.Length > 3 && p[3] == "0")
                    {
                        usbSemPermissao = true;
                        continue;
                    }
                    if (melhor == "") melhor = "webcam USB";
                    continue;
                }
                string nome = p[1] == "externa" ? "câmera USB" : "câmera " + p[1];
                _sistemaVisto = true;
                if (melhor == "" || p[1] == "externa") melhor = nome;
            }
            if (melhor == "")
            {
                Mudar(usbSemPermissao ? "sem_permissao_usb" : "sem_camera");
                return;
            }
            NomeCamera = melhor;
            Mudar("pronta");
        }

        /// <summary>
        /// Na TV Box a lista de câmeras vem do Android (câmera clássica, Camera2
        /// e USB): numa TV Box fraca isso pode levar dezenas de ms — o Java faz
        /// numa linha de execução separada, o jogo não para enquanto procura.
        /// </summary>
        static void ProcurarAndroidSemParar()
        {
            var a = Android();
            if (_procurando || a == null) return;
            _procurando = true;
            _procurandoDesde = Agora();
            _listagemVista = a.Listagens();
            a.Procurar();
        }

        static void ConferirLista()
        {
            var a = Android();
            if (a == null)
            {
                _procurando = false;
                return;
            }
            if (a.Listagens() == _listagemVista)
            {
                if (Agora() - _procurandoDesde > 15000) _procurando = false;   // (o Java não respondeu: tenta de novo depois)
                return;
            }
            _procurando = false;
            ListaChegou(a.Lista());
        }

        static void ListaChegou(string lista)
        {
            if (Android() == null || Status == "desligada") return;
            ProcurarAndroid(string.IsNullOrEmpty(lista) ? "#vazia" : lista);
            if (Status == "sem_permissao_usb") PedirUsb(Agora());
            else if (Status == "pronta") ManterLigada();
        }

        /// <summary>
        /// A janela USB da webcam (só para a UVC direta). Como no jogo de soco:
        /// nunca logo depois do boot (o serviço de câmera do Android ainda conta
        /// zero câmeras) e nunca se a webcam já apareceu como câmera do sistema —
        /// aí ela não precisa de permissão USB nenhuma, só abrir.
        /// </summary>
        static void PedirUsb(long agora)
        {
            if (agora - _buscaDesdeMs < ESPERA_USB_MS && !_pedirAgora) return;
            if (_sistemaVisto || (Lembrado("sistema") && agora - _buscaDesdeMs < 45000)) return;
            if (Lembrado("usb_pedida") && !_pedirAgora) return;   // já pediu uma vez: nunca mais sozinho
            _pedirAgora = false;
            Lembrar("usb_pedida");
            Android().PedirUsb();
        }

        /// <summary>
        /// Ligada e sem imagem há 30 s: reabre do zero. Raro e só na tela de
        /// espera — no jogo de soco, abrir e fechar a webcam a cada poucos
        /// segundos mexia no driver USB e derrubava a TV Box.
        /// </summary>
        static void ReabrirSeMuda(long agora)
        {
            if (agora - Math.Max(_vivoMs, _abertaMs) < REABRIR_MS) return;
            if (_reaberturas >= REABERTURAS_MAX || agora - _reabertaMs < REABRIR_MS) return;
            _reaberturas++;
            _reabertaMs = agora;
            _android.Fechar();
            AbrirAndroid();
        }

        static void AbrirAndroid()
        {
            var a = Android();
            if (a == null) return;
            a.Ver(_vendo);
            a.Abrir("auto", W_CAM, H_CAM, LADO);
            _ligada = true;
            _quadroVisto = -1;
            _ultimoN = -1;
            _abertaMs = Agora();
        }

        /// <summary>Uma vez por segundo: a câmera está viva? (só o contador de quadros: não custa nada)</summary>
        static void VigiarAndroid(float dt)
        {
            _vigiaT += dt;
            if (_vigiaT < 1f) return;
            _vigiaT = 0f;
            var a = Android();
            // a TV Box pausou o jogo (o Java solta a câmera): liga de novo
            if (a.Parada())
            {
                AbrirAndroid();
                return;
            }
            int n = a.Quadros();
            if (n > 0 && n != _ultimoN)
            {
                _ultimoN = n;
                _vivoMs = Agora();
                _reaberturas = 0;
                if (!Lembrado("sistema"))
                {
                    string f = a.Fonte();
                    if (f == "sistema" || f == "camera2") Lembrar("sistema");
                }
            }
        }

        /// <summary>Com alguém vendo: o quadro novo (Y, U, V do quadrado) sobe e vira a imagem ao vivo.</summary>
        static void QuadroAndroid(long agora)
        {
            if (_quadroVisto >= 0 && agora - _quadroMs < QUADRO_MS) return;
            int n = _android.LerQuadro(LADO);
            if (n <= 0) return;
            _quadroMs = agora;
            _quadroVisto = n;
            Desenhar(_android.TexY, _android.TexU, _android.TexV, true, LADO, LADO, false, 0);
        }
#endif

        /// <summary>
        /// Tela de espera (atração), a cada poucos segundos: confere a câmera. As
        /// janelas do Android aparecem SÓ aqui, nunca no meio do jogo (como no
        /// jogo de soco), e cada uma UMA vez na vida: a da câmera e a da webcam
        /// USB. A câmera ligada e sem imagem reabre aqui.
        /// </summary>
        public static void NaEspera()
        {
            try
            {
                if (Status == "desligada") return;
                long agora = Agora();
                if (_janelaAte > 0 && agora >= _janelaAte) FechouJanela();
#if UNITY_ANDROID && !UNITY_EDITOR
                if (_falsa == null && !TemPermissao())
                {
                    Mudar("sem_permissao");
                    PedirPermissao();
                    return;
                }
                if (Fonte != ANDROID || Android() == null)
                {
                    if ((Status == "sem_camera" || Status == "procurando" || Status == "sem_permissao") && agora - _buscaMs >= BUSCA_MS)
                        ProcurarAgora();
                    return;
                }
                // (a permissão USB recém-dada precisa ser vista logo: procura em toda
                // espera, como no jogo de soco — sem parar o jogo)
                if (Status != "pronta") ProcurarAndroidSemParar();
                if (Status == "sem_permissao_usb") PedirUsb(agora);
                else if (Status == "pronta")
                {
                    if (_ligada) ReabrirSeMuda(agora);
                    else ManterLigada();
                }
#else
                if ((Status == "sem_camera" || Status == "procurando" || Status == "sem_permissao") && agora - _buscaMs >= BUSCA_MS)
                    ProcurarAgora();
#endif
            }
            catch (Exception e)
            {
                Falhou("NaEspera", e);
            }
        }

        // ---------------------------------------------------------------- lembranças (fotos/camera.json)
        /// <summary>O que o jogo guarda entre uma abertura e outra: a webcam já mandou
        /// imagem como câmera do sistema? as janelas de permissão já foram pedidas?</summary>
        static bool Lembrado(string chave)
        {
            if (_lembrancas == null)
            {
                try
                {
                    string f = Path.Combine(PastaFotos(), "camera.json");
                    if (File.Exists(f)) _lembrancas = Json.Dic(Json.Ler(File.ReadAllText(f)));
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Fotos: não li camera.json: " + e.Message);
                }
                if (_lembrancas == null) _lembrancas = new Dictionary<string, object>();
            }
            return Json.Bool(Json.Pegar(_lembrancas, chave));
        }

        static void Lembrar(string chave)
        {
            if (Lembrado(chave)) return;
            _lembrancas[chave] = true;
            try
            {
                // (pequeno e raro: grava na hora, antes da janela aparecer)
                File.WriteAllText(Path.Combine(PastaFotos(), "camera.json"), Json.Escrever(_lembrancas, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Fotos: não gravei camera.json: " + e.Message);
            }
        }

        // ================================================================ ligar / desligar
        /// <summary>Alguém apertou START. No Android a câmera já fica ligada (só garante);
        /// no PC ela liga agora, para a imagem estar pronta no "SORRIA!".</summary>
        public static void Preparar()
        {
            try
            {
                ProcurarAgora();
                if (!Pronta() || !Ajustes.Bool("jogo", "foto")) return;
                if (Fonte == ANDROID)
                {
                    _sempre = true;
                    ManterLigada();
                }
                else Ligar();
            }
            catch (Exception e)
            {
                Falhou("Preparar", e);
            }
        }

        /// <summary>Vale fazer a sessão de fotos? Câmera achada e, no Android, mandando
        /// imagem (ou ainda subindo). Câmera muda: o jogo segue com os mascotes.</summary>
        public static bool PodeFotografar()
        {
            if (!Pronta() || !Ajustes.Bool("jogo", "foto")) return false;
            if (Fonte == ANDROID && _ligada && !_vendo)
            {
                long agora = Agora();
                return (_vivoMs > 0 && agora - _vivoMs < 5000) || agora - _abertaMs < 25000;
            }
            return true;
        }

        /// <summary>Android: 15 s depois da abertura (longe do pico de memória do
        /// carregamento) a câmera acorda e fica ligada.</summary>
        static void Acordar()
        {
            if (Fonte != ANDROID) return;
            _sempre = true;
            ManterLigada();
        }

        /// <summary>Abre a câmera (se ainda não está), sem ninguém vendo: só conta quadros.</summary>
        static void ManterLigada()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!_sempre || Fonte != ANDROID || Android() == null || _ligada) return;
            if (!Pronta() || !Ajustes.Bool("jogo", "foto")) return;
            AbrirAndroid();
#endif
        }

        /// <summary>O que o Android enxerga (para o teste da câmera no menu do operador).</summary>
        public static string Relatorio()
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (Fonte == ANDROID && _android != null) return _android.Relatorio();
#endif
                return TextoStatus();
            }
            catch (Exception e)
            {
                Falhou("Relatorio", e);
                return TextoStatus();
            }
        }

        /// <summary>Liga a câmera para a prévia e a foto (AoVivo passa a ter imagem).</summary>
        public static bool Ligar()
        {
            try
            {
                if (!Pronta() || _imagem == null) return false;
                _desligarEm = -1f;
                if (!_vendo)
                {
                    _vendo = true;
                    // a foto só com quadro NOVO (nunca o da partida anterior)
                    _quadroVisto = -1;
                    _tentado = -1;
                    _imagem.Limpar();
#if UNITY_ANDROID && !UNITY_EDITOR
                    if (Fonte == ANDROID && _ligada) _android.Ver(true);
#endif
                }
                if (!_ligada)
                {
                    switch (Fonte)
                    {
#if UNITY_ANDROID && !UNITY_EDITOR
                        case ANDROID:
                            AbrirAndroid();
                            break;
#else
                        case WEBCAM:
                            if (_webcam == null) _webcam = new CameraWebcam();
                            if (!_webcam.Abrir(_webcamNome, W_CAM, H_CAM))
                            {
                                _vendo = false;
                                Mudar("sem_camera");
                                return false;
                            }
                            break;
#endif
                    }
                    _ligada = true;
                }
                return true;
            }
            catch (Exception e)
            {
                Falhou("Ligar", e);
                return false;
            }
        }

        /// <summary>Desliga depois de um tempinho (a webcam não gosta de liga-desliga seguido).</summary>
        public static void Desligar(float atraso = 6f)
        {
            _desligarEm = Mathf.Max(0f, atraso);
        }

        /// <summary>Ninguém mais vendo. No Android a câmera continua ligada (só contando
        /// os quadros); no PC ela desliga.</summary>
        static void Soltar()
        {
            _vendo = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Fonte == ANDROID && _sempre && _ligada && Ajustes.Bool("jogo", "foto"))
            {
                _android.Ver(false);
                return;
            }
#endif
            Parar();
        }

        static void Parar()
        {
            _vendo = false;
            if (!_ligada) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Fonte == ANDROID && _android != null) _android.Fechar();
#else
            if (_webcam != null) _webcam.Fechar();
#endif
            _ligada = false;
        }

        /// <summary>A imagem ao vivo já recortada (quadrada), espelhada e girada.</summary>
        public static Texture AoVivo() => _imagem != null ? _imagem.Alvo : null;

        /// <summary>A câmera já mandou imagem desde que foi ligada?</summary>
        public static bool TemImagem() => _ligada && _quadroVisto >= 0;

        // ================================================================ quadro a quadro
        void Update()
        {
            try
            {
                Passo(Time.unscaledDeltaTime);
            }
            catch (Exception e)
            {
                Falhou("Update", e);
            }
        }

        static void Passo(float dt)
        {
            long agora = Agora();
            ReceberLidas();
            if (_iniciarEm >= 0 && agora >= _iniciarEm)
            {
                _iniciarEm = -1;
                Iniciar();
            }
            if (_acordarEm >= 0 && agora >= _acordarEm)
            {
                _acordarEm = -1;
                Acordar();
            }
            if (_abrirJanelaEm >= 0 && agora >= _abrirJanelaEm)
            {
                _abrirJanelaEm = -1;
                AbrirJanelaAndroid();
            }
            int resposta = Interlocked.Exchange(ref _respostaPermissao, 0);
            if (resposta != 0)
            {
                FechouJanela();
                if (resposta == 1) Iniciar();
            }
            if (_janelaAte > 0 && agora >= _janelaAte) FechouJanela();
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_procurando) ConferirLista();
#endif
            if (_desligarEm >= 0f)
            {
                _desligarEm -= dt;
                if (_desligarEm < 0f) Soltar();
            }
            // (procurar câmera trava o quadro por dezenas de ms: só na abertura e
            // quando alguém aperta START — ProcurarAgora — nunca no meio do jogo)
            if (!_ligada || _imagem == null) return;
            switch (Fonte)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                case ANDROID:
                    VigiarAndroid(dt);
                    if (_vendo) QuadroAndroid(agora);
                    break;
#else
                case WEBCAM:
                    if (_vendo && _webcam != null && _webcam.NovoQuadro())
                    {
                        _quadroVisto = Math.Max(_quadroVisto, 0) + 1;
                        Desenhar(_webcam.Textura, null, null, false, _webcam.Largura, _webcam.Altura, !_webcam.Virada, _webcam.Giro);
                    }
                    break;
#endif
                case FALSA:
                    if (_vendo && _falsa != null)
                    {
                        // mexe um tiquinho (a cada 0,1 s), para parecer imagem ao vivo
                        _falsaT += dt;
                        _falsaQuadroT += dt;
                        if (_quadroVisto < 0 || _falsaQuadroT >= 0.1f)
                        {
                            _falsaQuadroT = 0f;
                            _quadroVisto = Math.Max(_quadroVisto, 0) + 1;
                            var t = _falsa.Textura;
                            Desenhar(t, null, null, false, t.width, t.height, true, 0);
                        }
                    }
                    break;
            }
            // o operador girou/espelhou (TESTE DA CÂMERA): redesenha o último quadro na hora
            if (_vendo && _quadroVisto >= 0 && ChaveDesenho() != _chaveDesenho) Redesenhar();
        }

        static int GiroOperador() => (Ajustes.Int("jogo", "camera_giro") / 90 % 4 + 4) % 4;

        static int ChaveDesenho() => GiroOperador() * 2 + (Ajustes.Bool("jogo", "camera_espelho") ? 1 : 0);

        static void Desenhar(Texture principal, Texture u, Texture v, bool yuv, int largura, int altura, bool deBaixoParaCima, int giroExtra)
        {
            _dPrincipal = principal;
            _dU = u;
            _dV = v;
            _dYuv = yuv;
            _dLargura = largura;
            _dAltura = altura;
            _dDeBaixo = deBaixoParaCima;
            _dGiroExtra = giroExtra;
            Redesenhar();
        }

        static void Redesenhar()
        {
            if (_imagem == null || _dPrincipal == null) return;
            _chaveDesenho = ChaveDesenho();
            // a câmera pode estar montada de lado/ao contrário: o operador acerta no menu ("Giro da câmera")
            int giro = (GiroOperador() + _dGiroExtra) & 3;
            float desloc = Fonte == FALSA && _falsa != null ? _falsa.Deslocamento(_falsaT) : 0f;
            _imagem.Desenhar(_dPrincipal, _dU, _dV, _dYuv, _dLargura, _dAltura, _dDeBaixo, giro, Ajustes.Bool("jogo", "camera_espelho"), desloc);
        }

        void OnApplicationQuit()
        {
            Fechar();
        }

        void OnDestroy()
        {
            if (I != this) return;
            I = null;
            Fechar();
        }

        static void Fechar()
        {
            try
            {
                Parar();
#if UNITY_ANDROID && !UNITY_EDITOR
                if (_android != null) _android.Fechar();
#endif
            }
            catch (Exception e)
            {
                Falhou("Fechar", e);
            }
            EsperarDisco(2000);
        }

        // ================================================================ foto
        /// <summary>
        /// Tira a foto AGORA ("j0", "j1"...) e guarda para a partida. Só sai foto
        /// com imagem da câmera e quadro BOM (nem escuro nem chapado — tampa na
        /// lente); senão null (o jogo segue com o mascote). Para esperar um quadro
        /// bom (até 4 s, como a edição Godot), use Fotografar.
        /// </summary>
        public static Texture2D Tirar(string chave)
        {
            try
            {
                if (!TemImagem() || _imagem == null || string.IsNullOrEmpty(chave)) return null;
                _tentado = _quadroVisto;
                if (!_imagem.Ler() || !_imagem.Boa()) return null;
                return GuardarFoto(chave, _imagem.Copiar());
            }
            catch (Exception e)
            {
                Falhou("Tirar", e);
                return null;
            }
        }

        /// <summary>
        /// Tira a foto como no jogo de soco: espera um quadro BOM (até 4 s) — com
        /// imagem, nem escuro nem chapado. A foto não se perde porque a câmera
        /// demorou um pouco ou mandou um quadro ruim. No fim chama pronta(foto)
        /// (null = sem foto). Numa corrotina:
        ///   Texture2D foto = null;
        ///   yield return Fotos.Fotografar("j0", f =&gt; foto = f);
        /// </summary>
        public static IEnumerator Fotografar(string chave, Action<Texture2D> pronta)
        {
            Texture2D foto = null;
            long limite = Agora() + FOTO_MAX_MS;
            while (_ligada && Agora() < limite)
            {
                // (ler a placa de vídeo para o jogo um instante: só uma vez por quadro novo da câmera)
                if (TemImagem() && _quadroVisto != _tentado)
                {
                    foto = Tirar(chave);
                    if (foto != null) break;
                }
                yield return null;
            }
            try
            {
                pronta?.Invoke(foto);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        static Texture2D GuardarFoto(string chave, byte[] rgb)
        {
            var t = NovaTextura(rgb, LADO, LADO, 3, "Foto " + chave);
            if (_fotos.TryGetValue(chave, out var velha))
            {
                _fotos.Remove(chave);
                Liberar(velha);
            }
            _fotos[chave] = t;
            _imagens[chave] = rgb;
            // câmera de teste: o próximo jogador é outra criança
            if (_falsa != null) _falsa.Proxima();
            return t;
        }

        /// <summary>Textura da foto (com mipmaps se a placa aguentar: fica bonita pequena no placar).</summary>
        static Texture2D NovaTextura(byte[] pontos, int largura, int altura, int canais, string nome)
        {
            bool mip = SystemInfo.npotSupport == NPOTSupport.Full || (Mathf.IsPowerOfTwo(largura) && Mathf.IsPowerOfTwo(altura));
            var t = new Texture2D(largura, altura, canais == 4 ? TextureFormat.RGBA32 : TextureFormat.RGB24, mip)
            {
                name = nome,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = mip ? FilterMode.Trilinear : FilterMode.Bilinear,
            };
            t.SetPixelData(pontos, 0);
            // (fica legível: se a TV Box perder o contexto da placa de vídeo, a Unity sobe de novo)
            t.Apply(mip, false);
            return t;
        }

        public static Texture2D Foto(string chave) => chave != null && _fotos.TryGetValue(chave, out var t) ? t : null;

        public static void LimparPartida()
        {
            if (_fotos.Count == 0) return;
            var velhas = new List<Texture2D>(_fotos.Values);
            _fotos.Clear();
            _imagens.Clear();
            foreach (var t in velhas) Liberar(t);
        }

        /// <summary>Some com a textura se ninguém mais a guarda (partida, recorde, classificação).</summary>
        static void Liberar(Texture2D t)
        {
            if (t == null || _fotos.ContainsValue(t) || _guardadas.ContainsValue(t)) return;
            Destroy(t);
        }

        // ================================================================ disco
        static string PastaFotos()
        {
            if (_pasta == null)
            {
                _pasta = Path.Combine(Ajustes.Pasta, "fotos");
                try { Directory.CreateDirectory(_pasta); } catch (Exception e) { Debug.LogWarning("Fotos: " + e.Message); }
            }
            return _pasta;
        }

        /// <summary>O caminho como chave (absoluto; aceita "user://..." e "fotos/..." das edições antigas).</summary>
        static string Resolver(string caminho)
        {
            string c = caminho;
            if (c.StartsWith("user://", StringComparison.Ordinal)) c = Path.Combine(Ajustes.Pasta, c.Substring(7));
            else if (!Path.IsPathRooted(c)) c = Path.Combine(Ajustes.Pasta, c);
            try { return Path.GetFullPath(c); } catch (Exception) { return c; }
        }

        /// <summary>
        /// Grava a foto da partida num PNG (fotos/nome) e volta o caminho ("" =
        /// sem foto). O PNG é escrito numa linha de trabalho (levava dezenas de ms
        /// no PC e centenas na TV Box, travando o fim da partida); a foto já fica
        /// guardada, então ninguém precisa ler o arquivo enquanto ele é escrito.
        /// </summary>
        public static string Gravar(string chave, string nomeArquivo)
        {
            try
            {
                if (string.IsNullOrEmpty(chave) || string.IsNullOrEmpty(nomeArquivo)) return "";
                if (!_fotos.TryGetValue(chave, out var t) || t == null) return "";
                _imagens.TryGetValue(chave, out byte[] rgb);
                string caminho = Resolver(Path.Combine(PastaFotos(), nomeArquivo));
                _guardadas.TryGetValue(caminho, out var velha);
                _guardadas[caminho] = t;
                _preCarregando.Remove(caminho);
                if (velha != t) Liberar(velha);
                if (rgb != null && _pngProprio) GravarNoDisco(caminho, rgb);
                else GravarComUnity(caminho, t);
                return caminho;
            }
            catch (Exception e)
            {
                Falhou("Gravar", e);
                return "";
            }
        }

        static void GravarNoDisco(string caminho, byte[] rgb)
        {
            NoDisco(() =>
            {
                byte[] png;
                try
                {
                    png = CameraPng.Codificar(rgb, LADO, LADO, 3, true);
                }
                catch (Exception e)
                {
                    // (sem o compressor do .NET nesta plataforma: a Unity grava, na linha principal)
                    Debug.LogWarning("Fotos: PNG próprio indisponível (" + e.Message + "): a Unity grava");
                    _pngProprio = false;
                    lock (_travaLidas) _gravarComUnity.Add(caminho);
                    return;
                }
                EscreverArquivo(caminho, png);
            });
        }

        static void GravarComUnity(string caminho, Texture2D t)
        {
            byte[] png = t.EncodeToPNG();
            if (png != null) NoDisco(() => EscreverArquivo(caminho, png));
        }

        static void EscreverArquivo(string caminho, byte[] dados)
        {
            string pasta = Path.GetDirectoryName(caminho);
            if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);
            string tmp = caminho + ".tmp";
            File.WriteAllBytes(tmp, dados);
            if (File.Exists(caminho)) File.Delete(caminho);
            File.Move(tmp, caminho);
        }

        /// <summary>Foto gravada (classificação, recorde) — lida do disco uma vez e guardada.</summary>
        public static Texture2D Carregar(string caminho)
        {
            try
            {
                if (string.IsNullOrEmpty(caminho)) return null;
                string c = Resolver(caminho);
                if (_guardadas.TryGetValue(c, out var t)) return t;
                // (ainda não veio da leitura da abertura: lê agora mesmo)
                _preCarregando.Remove(c);
                t = File.Exists(c) ? TexturaDoPng(File.ReadAllBytes(c), c) : null;
                _guardadas[c] = t;
                return t;
            }
            catch (Exception e)
            {
                Falhou("Carregar", e);
                return null;
            }
        }

        static Texture2D TexturaDoPng(byte[] png, string caminho)
        {
            byte[] pontos = CameraPng.Decodificar(png, true, out int w, out int h, out int canais);
            if (pontos != null) return NovaTextura(pontos, w, h, canais, Path.GetFileName(caminho));
            // (PNG de outro tipo: a Unity lê)
            var t = new Texture2D(2, 2, TextureFormat.RGB24, true) { name = Path.GetFileName(caminho), wrapMode = TextureWrapMode.Clamp };
            if (t.LoadImage(png, false)) return t;
            Destroy(t);
            return null;
        }

        /// <summary>Some com a foto guardada desse caminho (o arquivo foi apagado).</summary>
        public static void Esquecer(string caminho)
        {
            try
            {
                if (string.IsNullOrEmpty(caminho)) return;
                string c = Resolver(caminho);
                _preCarregando.Remove(c);
                if (_guardadas.TryGetValue(c, out var t))
                {
                    _guardadas.Remove(c);
                    Liberar(t);
                }
            }
            catch (Exception e)
            {
                Falhou("Esquecer", e);
            }
        }

        // ---------------------------------------------------------------- recorde
        public static string SalvarRecorde(string chave, int jogadores) => Gravar(chave, "recorde_j" + jogadores + ".png");

        public static Texture2D FotoRecorde(int jogadores) => Carregar(Json.Str(Json.Pegar(Ajustes.Recorde(jogadores), "foto")));

        public static void ApagarRecordes()
        {
            try
            {
                var velhas = new List<Texture2D>(_guardadas.Values);
                _guardadas.Clear();
                _preCarregando.Clear();
                foreach (var t in velhas) Liberar(t);
                string pasta = PastaFotos();
                // (na fila do disco: depois de qualquer PNG que ainda estava sendo gravado)
                NoDisco(() =>
                {
                    if (!Directory.Exists(pasta)) return;
                    foreach (string f in Directory.GetFiles(pasta))
                    {
                        string n = Path.GetFileName(f);
                        if (!n.StartsWith("recorde_", StringComparison.Ordinal) && !n.StartsWith("rank_", StringComparison.Ordinal)) continue;
                        try { File.Delete(f); } catch (Exception) { }
                    }
                });
            }
            catch (Exception e)
            {
                Falhou("ApagarRecordes", e);
            }
        }

        // ---------------------------------------------------------------- fotos guardadas, lidas na abertura
        struct Lida
        {
            public string caminho;
            public byte[] pontos;     // null: a Unity lê (png)
            public byte[] png;
            public int largura, altura, canais;
        }

        static readonly object _travaLidas = new object();
        static readonly List<Lida> _lidas = new List<Lida>();
        static readonly List<Lida> _lidasAgora = new List<Lida>();
        static readonly List<string> _gravarComUnity = new List<string>();
        static readonly HashSet<string> _preCarregando = new HashSet<string>();

        /// <summary>
        /// As fotos do recorde e da classificação são lidas do disco já na
        /// abertura, numa linha de trabalho: ler e preparar cada PNG na hora em
        /// que a tela de espera aparece dava um tranco.
        /// </summary>
        static void LerFotosGuardadas()
        {
            string pasta = PastaFotos();
            var caminhos = new List<string>();
            try
            {
                foreach (string f in Directory.GetFiles(pasta, "*.png"))
                {
                    string n = Path.GetFileName(f);
                    if (!n.StartsWith("recorde_", StringComparison.Ordinal) && !n.StartsWith("rank_", StringComparison.Ordinal)) continue;
                    string c = Resolver(f);
                    if (_guardadas.ContainsKey(c)) continue;
                    caminhos.Add(c);
                    _preCarregando.Add(c);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Fotos: não li a pasta das fotos: " + e.Message);
            }
            if (caminhos.Count == 0) return;
            NoDisco(() =>
            {
                foreach (string c in caminhos)
                {
                    var l = new Lida { caminho = c };
                    try
                    {
                        byte[] png = File.ReadAllBytes(c);
                        l.pontos = CameraPng.Decodificar(png, true, out l.largura, out l.altura, out l.canais);
                        if (l.pontos == null) l.png = png;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("Fotos: não li " + c + ": " + e.Message);
                        continue;
                    }
                    lock (_travaLidas) _lidas.Add(l);
                }
            });
        }

        /// <summary>(linha principal) As fotos lidas viram texturas — no máximo 2 por quadro, sem tranco.</summary>
        static void ReceberLidas()
        {
            string gravar = null;
            lock (_travaLidas)
            {
                if (_lidas.Count == 0 && _gravarComUnity.Count == 0) return;
                int n = Math.Min(2, _lidas.Count);
                for (int i = 0; i < n; i++) _lidasAgora.Add(_lidas[i]);
                _lidas.RemoveRange(0, n);
                if (_gravarComUnity.Count > 0)
                {
                    gravar = _gravarComUnity[0];
                    _gravarComUnity.RemoveAt(0);
                }
            }
            foreach (var l in _lidasAgora)
            {
                // (o jogo já leu sozinho, apagou ou trocou a foto enquanto isto lia: vale a de agora)
                if (!_preCarregando.Remove(l.caminho) || _guardadas.ContainsKey(l.caminho)) continue;
                _guardadas[l.caminho] = l.pontos != null
                    ? NovaTextura(l.pontos, l.largura, l.altura, l.canais, Path.GetFileName(l.caminho))
                    : TexturaDoPng(l.png, l.caminho);
            }
            _lidasAgora.Clear();
            if (gravar != null && _guardadas.TryGetValue(gravar, out var t) && t != null) GravarComUnity(gravar, t);
        }

        // ---------------------------------------------------------------- a fila do disco
        // (uma linha de trabalho por vez, na ordem: apagar os recordes nunca passa
        // na frente de um PNG que ainda estava sendo gravado)
        static readonly object _travaDisco = new object();
        static readonly Queue<Action> _filaDisco = new Queue<Action>();
        static bool _discoRodando;

        static void NoDisco(Action a)
        {
            lock (_travaDisco)
            {
                _filaDisco.Enqueue(a);
                if (_discoRodando) return;
                _discoRodando = true;
            }
            if (!ThreadPool.QueueUserWorkItem(RodarDisco))
            {
                lock (_travaDisco) _discoRodando = false;
                RodarDisco(null);
            }
        }

        static void RodarDisco(object _)
        {
            while (true)
            {
                Action a;
                lock (_travaDisco)
                {
                    if (_filaDisco.Count == 0)
                    {
                        _discoRodando = false;
                        Monitor.PulseAll(_travaDisco);
                        return;
                    }
                    a = _filaDisco.Dequeue();
                }
                try
                {
                    a();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Fotos (disco): " + e.Message);
                }
            }
        }

        /// <summary>Fechando o jogo: espera o que ainda estiver sendo gravado (até `ms`).</summary>
        static void EsperarDisco(int ms)
        {
            long fim = Agora() + ms;
            lock (_travaDisco)
            {
                while (_discoRodando || _filaDisco.Count > 0)
                {
                    int resta = (int)(fim - Agora());
                    if (resta <= 0) break;
                    Monitor.Wait(_travaDisco, resta);
                }
            }
        }

        // ---------------------------------------------------------------- eventos e erros
        static void Avisar(string t)
        {
            try { JanelaAndroid?.Invoke(t); } catch (Exception e) { Debug.LogException(e); }
        }

        static void Falhou(string onde, Exception e)
        {
            long agora = Agora();
            if (agora - _ultimoErro < 10000) return;
            _ultimoErro = agora;
            Debug.LogWarning("Fotos (" + onde + "): " + e);
        }
    }
}
