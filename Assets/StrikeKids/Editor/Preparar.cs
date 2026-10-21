using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StrikeKids.Editor
{
    /// <summary>
    /// AO ABRIR O PROJETO NA UNITY (uma vez por sessão do editor): na primeira
    /// vez, configura o projeto para a TV Box e cria a cena do jogo
    /// (Assets/StrikeKids/Cenas/Jogo.unity: um objeto "Jogo" com o Principal —
    /// o resto é montado por código); depois, abre a cena se o editor estiver
    /// numa cena vazia. Aí é só apertar PLAY para jogar no editor, ou usar o
    /// menu "Strike Kids" para gerar o APK e a versão de Windows.
    /// </summary>
    [InitializeOnLoad]
    static class Preparar
    {
        const string SESSAO = "StrikeKids.Preparado";

        static Preparar()
        {
            if (Application.isBatchMode || SessionState.GetBool(SESSAO, false)) return;
            EditorApplication.delayCall += Fazer;
        }

        static void Fazer()
        {
            // espera a Unity terminar de importar/compilar
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Fazer;
                return;
            }
            SessionState.SetBool(SESSAO, true);
            bool primeira = !File.Exists(Construir.CENA);
            if (primeira || PlayerSettings.productName != "Strike Kids") Construir.Configurar();
            if (primeira) Construir.CriarCena();
            var ativa = EditorSceneManager.GetActiveScene();
            if (ativa.path != Construir.CENA && string.IsNullOrEmpty(ativa.path) && !ativa.isDirty)
                EditorSceneManager.OpenScene(Construir.CENA);
            var cenas = EditorBuildSettings.scenes;
            if (cenas.Length == 0 || cenas[0].path != Construir.CENA)
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Construir.CENA, true) };
            if (primeira)
                EditorUtility.DisplayDialog("Strike Kids",
                    "Projeto pronto!\n\n" +
                    "JOGAR AQUI: aperte o PLAY (o triângulo lá em cima).\n" +
                    "Teclas: 5 = ficha, 1 ou Espaço = START, A S D F G = sensores, 9 = menu, F3 = medidor.\n\n" +
                    "GERAR: menu Strike Kids > Gerar APK (TV Box) ou > Gerar versão Windows.\n" +
                    "A 1ª geração calibra as jogadas com a física (alguns minutos, uma vez só).", "OK");
        }

        [MenuItem("Strike Kids/Jogar aqui no editor (Play)", false, 0)]
        static void Jogar()
        {
            if (EditorApplication.isPlaying) return;
            if (!File.Exists(Construir.CENA))
            {
                Construir.Configurar();
                Construir.CriarCena();
            }
            if (EditorSceneManager.GetActiveScene().path != Construir.CENA)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(Construir.CENA);
            }
            EditorApplication.isPlaying = true;
        }
    }
}
