using System;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// A PONTE COM O ANDROID: as teclas e botões crus (StrikeAtividade.java).
    /// Na TV Box, a StrikeAtividade (a atividade principal do APK, no lugar da
    /// UnityPlayerActivity) anota TODO código de tecla do Android — inclusive
    /// os botões que a Unity não numera — e o jogo lê a fila a cada quadro.
    /// Se ela não estiver rodando (manifesto sem ela), vale o Input da Unity.
    /// </summary>
    public static class AndroidPonte
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaClass _atividade;
        static bool _falhou;
        static int _conferida = -1;

        static AndroidJavaClass Atividade
        {
            get
            {
                if (_atividade == null && !_falhou)
                {
                    try { _atividade = new AndroidJavaClass("com.lazersport.strikekids.StrikeAtividade"); }
                    catch (Exception e) { _falhou = true; Debug.LogWarning("StrikeAtividade: " + e.Message); }
                }
                return _atividade;
            }
        }

        /// <summary>A StrikeAtividade está rodando (as teclas vêm por ela)?</summary>
        public static bool Ativa
        {
            get
            {
                if (_conferida >= 0) return _conferida == 1;
                bool r = false;
                try { r = Atividade != null && Atividade.CallStatic<bool>("ativa"); } catch (Exception) { r = false; }
                // só guarda o "sim" (a atividade pode nascer depois do primeiro quadro)
                if (r) _conferida = 1;
                return r;
            }
        }

        /// <summary>"codigo:0;codigo:1;..." (0 = apertou, 1 = soltou) desde a última leitura.</summary>
        public static string LerTeclas()
        {
            var a = Atividade;
            if (a == null) return "";
            try { return a.CallStatic<string>("lerTeclas") ?? ""; }
            catch (Exception e)
            {
                _falhou = true;
                _atividade = null;
                Debug.LogWarning("lerTeclas: " + e.Message);
                return "";
            }
        }

        /// <summary>A atividade Android (para os plugins).</summary>
        public static AndroidJavaObject AtividadeAtual()
        {
            using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                return up.GetStatic<AndroidJavaObject>("currentActivity");
        }
#else
        public static bool Ativa => false;

        public static string LerTeclas() => "";
#endif
    }
}
