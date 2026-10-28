using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace StrikeKids.Editor
{
    /// <summary>
    /// COMO AS IMAGENS E OS SONS ENTRAM NO JOGO (sozinho, ao importar):
    ///   * telas (Resources/UI, Icone): sem mipmaps, no tamanho exato (sem
    ///     esticar para potência de 2: a proporção das placas vem da imagem),
    ///     transparência limpa nas bordas;
    ///   * texturas do 3D: com mipmaps (a pista vista de longe não cintila);
    ///   * na TV Box (OpenGL ES 2.0 = só ETC1): sem transparência vai em ETC1
    ///     (4 bits por ponto: o atlas 4096 de cada fase do cenário fica com
    ///     8 MB + mipmaps); com transparência fica sem perda (RGBA 32) —
    ///     a ETC2 não existe na Mali-400/450; os detalhes do chão
    ///     (Texturas/cenario/chao_*) são pesos e ficam sem perda (RGB 24);
    ///   * sons curtos: ADPCM descomprimido ao carregar (toca na hora, quase sem
    ///     processador); músicas: Vorbis tocando direto do arquivo.
    /// </summary>
    public class Importacao : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/StrikeKids/")) return;
            var ti = (TextureImporter)assetImporter;
            bool tela = assetPath.Contains("/Resources/UI/") || assetPath.Contains("/Icone/");
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.alphaIsTransparency = true;
            ti.isReadable = false;
            ti.mipmapEnabled = !tela;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.anisoLevel = 1;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            // os detalhes do chão (Texturas/cenario/chao_*: florzinhas, crateras,
            // conchinhas) são PESOS de cor, não imagem: comprimidos em ETC1 os
            // blocos de 4x4 viram manchas — ficam sem perda (como na edição
            // Godot) e se repetem pelo terreno
            bool pesos = assetPath.Contains("/Texturas/cenario/chao_");
            if (pesos)
            {
                ti.sRGBTexture = false;
                ti.alphaIsTransparency = false;
                ti.wrapMode = TextureWrapMode.Repeat;
            }
            var a = ti.GetPlatformTextureSettings("Android");
            a.overridden = true;
            a.maxTextureSize = 4096;
            a.format = pesos ? TextureImporterFormat.RGB24
                : TemTransparencia(assetPath) ? TextureImporterFormat.RGBA32 : TextureImporterFormat.ETC_RGB4;
            a.compressionQuality = 100;
            ti.SetPlatformTextureSettings(a);
        }

        /// <summary>
        /// A imagem tem transparência? Lido do próprio PNG (tipo de cor 4 ou 6, ou o
        /// bloco tRNS): na primeira importação (a pasta de trabalho nova do
        /// GERAR_APK) a Unity ainda não sabe, e o DoesSourceTextureHaveAlpha volta
        /// "não" — tudo iria para ETC1 sem transparência. Na dúvida: com transparência.
        /// </summary>
        static bool TemTransparencia(string caminho)
        {
            try
            {
                var b = File.ReadAllBytes(caminho);
                if (b.Length < 33 || b[0] != 0x89 || b[1] != (byte)'P' || b[2] != (byte)'N' || b[3] != (byte)'G') return true;
                int tipo = b[25];
                if (tipo == 4 || tipo == 6) return true;
                for (int i = 8; i + 8 <= b.Length;)
                {
                    int n = (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];
                    string t = Encoding.ASCII.GetString(b, i + 4, 4);
                    if (t == "tRNS") return true;
                    if (t == "IDAT" || t == "IEND" || n < 0) break;
                    i += 12 + n;
                }
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/StrikeKids/")) return;
            var ai = (AudioImporter)assetImporter;
            var s = ai.defaultSampleSettings;
            bool musica = assetPath.Contains("/musica_");
            if (musica)
            {
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.5f;
            }
            else
            {
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.ADPCM;
            }
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            ai.defaultSampleSettings = s;
            ai.loadInBackground = musica;
        }
    }
}
