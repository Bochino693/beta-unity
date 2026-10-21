using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace StrikeKids.Editor
{
    /// <summary>
    /// GERAÇÃO DO JOGO: deixa o projeto do jeito certo para a TV Box (OpenGL ES
    /// 2.0, IL2CPP 32 e 64 bits, Android 5.1+, Android TV, a chave do projeto),
    /// monta a cena (um objeto "Jogo" com o Principal: o resto é montado por
    /// código), calibra as jogadas se a física mudou e gera o APK.
    ///
    /// Pelo GERAR_APK.bat (Unity sem janela):
    ///   Unity -batchmode -quit -projectPath . -buildTarget Android
    ///         -executeMethod StrikeKids.Editor.Construir.Apk
    ///         [-saida caminho.apk] [-giro 1|-1|0] [-versao 6.0] [-calibrar] [-so32]
    /// No editor: menu "Strike Kids".
    /// </summary>
    public static class Construir
    {
        public const string CENA = "Assets/StrikeKids/Cenas/Jogo.unity";
        public const string PACOTE = "com.lazersport.strikekids.unity";
        const string FABRICA = "Assets/StrikeKids/Resources/Dados/fabrica.json";
        const string CHAVE = "tools/android/strikekids.keystore";

        static void Log(string t) => Debug.Log("[StrikeKids] " + t);

        static string Arg(string nome, string padrao = null)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length; i++)
                if (string.Equals(a[i], nome, StringComparison.OrdinalIgnoreCase))
                    return i + 1 < a.Length && !a[i + 1].StartsWith("-", StringComparison.Ordinal) || (i + 1 < a.Length && int.TryParse(a[i + 1], out _))
                        ? a[i + 1] : "";
            return padrao;
        }

        static bool TemArg(string nome) => Arg(nome) != null;

        // ------------------------------------------------------------ projeto
        [MenuItem("Strike Kids/Configurar o projeto (TV Box)")]
        public static void Configurar()
        {
            string versao = Arg("-versao", PlayerSettings.bundleVersion);
            if (string.IsNullOrEmpty(versao) || versao == "0.1" || versao == "1.0") versao = "6.0";
            PlayerSettings.companyName = "Lazer & Sport";
            PlayerSettings.productName = "Strike Kids";
            PlayerSettings.bundleVersion = versao;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, PACOTE);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, PACOTE);
            // número da versão do Android: data e hora da geração (sempre maior que a anterior)
            PlayerSettings.Android.bundleVersionCode = (int)((DateTime.UtcNow - new DateTime(2025, 1, 1)).TotalMinutes / 10);
            // cores como nas edições Godot (sRGB direto, sem conversão linear: mais leve)
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            // SÓ OpenGL ES 2.0: a Mali-400/450 da TX9 (os shaders são todos "target 2.0")
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES2 });
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            // -so32: só ARMv7 (o APK roda nas TV Box de 32 e de 64 bits e gera na metade do
            // tempo: é o que a geração na nuvem usa); sem ele, ARMv7 + ARM64
            PlayerSettings.Android.targetArchitectures = TemArg("-so32")
                ? AndroidArchitecture.ARMv7
                : AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);
            // a TV Box é deitada (HDMI): o jogo gira a imagem sozinho para o monitor em pé
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.Android.androidTVCompatibility = true;
            PlayerSettings.Android.androidIsGame = true;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            // desenha direto na tela (sem cópia a mais por quadro) e com a linha de desenho separada
            PlayerSettings.Android.blitType = AndroidBlitType.Never;
            PlayerSettings.SetMobileMTRendering(BuildTargetGroup.Android, true);
            PlayerSettings.gpuSkinning = false;
            PlayerSettings.runInBackground = true;
            // a chave do projeto (a mesma das edições Godot: o APK novo instala por cima)
            string chave = Path.GetFullPath(CHAVE);
            if (File.Exists(chave))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = chave;
                PlayerSettings.Android.keystorePass = "strikekids";
                PlayerSettings.Android.keyaliasName = "strikekids";
                PlayerSettings.Android.keyaliasPass = "strikekids";
            }
            else Debug.LogWarning("[StrikeKids] sem a chave " + chave + ": o APK sai com a chave de teste da Unity");
            // ícone
            var icone = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/StrikeKids/Icone/icone.png");
            if (icone != null) PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icone });
            // abertura da Unity (obrigatória na licença Personal): fundo azul do jogo
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.03f, 0.09f, 0.22f);
            PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
            // PC (para testar): janela cheia, sem a caixinha de resolução
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            // o passo da física (o jogo também acerta isso ao começar)
            Time.fixedDeltaTime = 1f / Medidas.PASSOS_FISICA;
            AssetDatabase.SaveAssets();
            Log("projeto configurado: OpenGL ES 2.0, IL2CPP (ARMv7 + ARM64), Android 5.1+, versão " + versao);
        }

        /// <summary>A cena do jogo: um objeto só, "Jogo", com o Principal.</summary>
        [MenuItem("Strike Kids/Criar a cena do jogo")]
        public static void CriarCena()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CENA));
            var cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Jogo");
            go.AddComponent<Principal>();
            go.AddComponent<AudioListener>();
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            EditorSceneManager.SaveScene(cena, CENA);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(CENA, true) };
            Log("cena criada: " + CENA);
        }

        /// <summary>O que vem de fábrica no APK (o giro da tela da máquina).</summary>
        static void GravarFabrica(int giroPadrao)
        {
            string g = Arg("-giro");
            int giro = giroPadrao;
            if (!string.IsNullOrEmpty(g) && int.TryParse(g, out int v)) giro = Mathf.Clamp(v, -1, 1);
            Directory.CreateDirectory(Path.GetDirectoryName(FABRICA));
            File.WriteAllText(FABRICA, "{\"giro\":" + giro + ",\"versao\":\"" + PlayerSettings.bundleVersion + "\"}");
            AssetDatabase.ImportAsset(FABRICA, ImportAssetOptions.ForceUpdate);
            Log("giro de fábrica da tela: " + giro);
        }

        static void CalibrarSePrecisa()
        {
            bool forcar = TemArg("-calibrar");
            if (forcar || Calibrar.Precisa())
            {
                Log(forcar ? "calibrando as jogadas (pedido)..." : "a física mudou: calibrando as jogadas (alguns minutos)...");
                if (!Calibrar.Rodar(!Application.isBatchMode)) throw new Exception("a calibração das jogadas falhou");
            }
            else Log("tabela das jogadas em dia (" + Calibrar.Assinatura + ")");
        }

        // ------------------------------------------------------------ geração
        [MenuItem("Strike Kids/Gerar APK (TV Box)")]
        static void ApkMenu() => Apk();

        /// <summary>Gera o APK da TV Box (chamado pelo GERAR_APK.bat).</summary>
        public static void Apk()
        {
            int codigo = 1;
            try
            {
                Configurar();
                CriarCena();
                CalibrarSePrecisa();
                GravarFabrica(Tela.GIRO_TV_BOX);
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                EditorUserBuildSettings.development = false;
                string saida = Arg("-saida");
                if (string.IsNullOrEmpty(saida)) saida = "Build/Android/StrikeKids-Unity.apk";
                saida = Path.GetFullPath(saida);
                Directory.CreateDirectory(Path.GetDirectoryName(saida));
                Log("gerando o APK: " + saida);
                var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { CENA }, locationPathName = saida, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None,
                });
                var s = r.summary;
                Log("resultado: " + s.result + " · " + (s.totalSize / 1048576) + " MB · " + (int)s.totalTime.TotalSeconds + " s · erros " + s.totalErrors);
                if (s.result == BuildResult.Succeeded)
                {
                    Log("APK PRONTO: " + saida);
                    codigo = 0;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            if (Application.isBatchMode) EditorApplication.Exit(codigo);
        }

        [MenuItem("Strike Kids/Gerar versão Windows (para testar no PC)")]
        static void WindowsMenu() => Windows();

        /// <summary>O jogo para Windows (testar no PC: A S D F G = sensores, 1 = START, 5 = ficha, 9 = menu).</summary>
        public static void Windows()
        {
            int codigo = 1;
            try
            {
                Configurar();
                CriarCena();
                CalibrarSePrecisa();
                GravarFabrica(0);   // no PC: monitor deitado, sem giro
                string saida = Arg("-saida");
                if (string.IsNullOrEmpty(saida)) saida = "Build/Windows/StrikeKids.exe";
                saida = Path.GetFullPath(saida);
                Directory.CreateDirectory(Path.GetDirectoryName(saida));
                var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { CENA }, locationPathName = saida, target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone, options = BuildOptions.None,
                });
                Log("resultado (Windows): " + r.summary.result);
                if (r.summary.result == BuildResult.Succeeded)
                {
                    Log("PRONTO: " + saida);
                    codigo = 0;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            if (Application.isBatchMode) EditorApplication.Exit(codigo);
        }
    }
}
