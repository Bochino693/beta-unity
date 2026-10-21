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
    ///     (4 bits por ponto); com transparência fica sem perda (RGBA 32) —
    ///     a ETC2 não existe na Mali-400/450;
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
            var a = ti.GetPlatformTextureSettings("Android");
            a.overridden = true;
            a.maxTextureSize = 4096;
            a.format = ti.DoesSourceTextureHaveAlpha() ? TextureImporterFormat.RGBA32 : TextureImporterFormat.ETC_RGB4;
            a.compressionQuality = 100;
            ti.SetPlatformTextureSettings(a);
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
