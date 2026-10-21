#if !UNITY_ANDROID || UNITY_EDITOR
using System;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// A WEBCAM NO PC (e no Editor): a WebCamTexture da Unity — nada de ffmpeg
    /// como na edição Godot. Fora da TV Box (lá a câmera é o StrikeCamera.java;
    /// a WebCamTexture nem entra no APK, para a Unity não pedir a permissão da
    /// câmera sozinha ao abrir o jogo).
    /// </summary>
    internal sealed class CameraWebcam
    {
        WebCamTexture _tex;

        /// <summary>A câmera a usar: a webcam USB (usb, sigma, uvc, extern... no nome), senão a primeira.</summary>
        public static bool Escolher(out string dispositivo)
        {
            dispositivo = "";
            WebCamDevice[] lista;
            try
            {
                lista = WebCamTexture.devices;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Fotos: sem lista de webcams: " + e.Message);
                return false;
            }
            if (lista == null || lista.Length == 0) return false;
            int melhor = 0, nota = -1;
            for (int i = 0; i < lista.Length; i++)
            {
                string nome = (lista[i].name ?? "").ToLowerInvariant();
                int n = 0;
                if (nome.Contains("usb") || nome.Contains("sigma") || nome.Contains("uvc") || nome.Contains("extern")) n += 4;
                if (n > nota)
                {
                    nota = n;
                    melhor = i;
                }
            }
            dispositivo = lista[melhor].name ?? "";
            return true;
        }

        public bool Abrir(string dispositivo, int largura, int altura)
        {
            Fechar();
            try
            {
                _tex = new WebCamTexture(dispositivo, largura, altura, 30)
                {
                    name = "Fotos.Webcam",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
                _tex.Play();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Fotos: a webcam não abriu: " + e.Message);
                Fechar();
                return false;
            }
        }

        public void Fechar()
        {
            if (_tex == null) return;
            try { _tex.Stop(); } catch (Exception) { }
            UnityEngine.Object.Destroy(_tex);
            _tex = null;
        }

        /// <summary>Chegou imagem nova neste quadro? (antes da primeira, a WebCamTexture tem 16x16)</summary>
        public bool NovoQuadro() => _tex != null && _tex.didUpdateThisFrame && _tex.width > 16 && _tex.height > 16;

        public Texture Textura => _tex;
        public int Largura => _tex != null ? _tex.width : 0;
        public int Altura => _tex != null ? _tex.height : 0;
        /// <summary>A imagem vem de cabeça para baixo (alguns sistemas)?</summary>
        public bool Virada => _tex != null && _tex.videoVerticallyMirrored;
        /// <summary>Quanto girar (x 90°, horário) para a imagem ficar de pé.</summary>
        public int Giro => _tex != null ? (_tex.videoRotationAngle / 90 % 4 + 4) % 4 : 0;
    }
}
#endif
