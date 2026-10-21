using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// A CÂMERA DE TESTE (PC sem webcam): ligada pela variável de ambiente
    /// STRIKE_CAMERA_FALSA (ou --camera-falsa na linha de comando), vale no
    /// lugar de qualquer câmera. A imagem mexe um tiquinho, para parecer ao
    /// vivo, e cada foto tirada passa para a próxima "criança".
    ///
    ///   STRIKE_CAMERA_FALSA=1                um desenho de teste (rosto, marcas
    ///                                        coloridas nos cantos de cima)
    ///   STRIKE_CAMERA_FALSA=C:\foto.png      essa foto (png/jpg)
    ///   STRIKE_CAMERA_FALSA=C:\pasta         as fotos da pasta, uma por criança
    ///
    /// As marcas: VERMELHA no canto de cima à esquerda e AMARELA à direita (como
    /// a câmera vê) — com o espelho ligado elas trocam de lado; o giro da
    /// câmera (menu do operador) gira tudo.
    /// </summary>
    internal sealed class CameraFalsa
    {
        public const string NOME = "Câmera de teste";
        readonly List<Texture2D> _bases = new List<Texture2D>();
        int _atual;

        public Texture2D Textura => _bases[_atual];

        public static CameraFalsa DoAmbiente()
        {
            string v = null;
            bool linha = false;
            try
            {
                v = Environment.GetEnvironmentVariable("STRIKE_CAMERA_FALSA");
                linha = Array.IndexOf(Environment.GetCommandLineArgs(), "--camera-falsa") >= 0;
            }
            catch (Exception)
            {
                // (sem ambiente nem linha de comando: sem câmera de teste)
            }
            if (string.IsNullOrEmpty(v) && !linha) return null;
            var c = new CameraFalsa();
            c.Carregar(v);
            return c;
        }

        void Carregar(string v)
        {
            try
            {
                if (!string.IsNullOrEmpty(v) && v.Length > 2)
                {
                    var arquivos = new List<string>();
                    if (File.Exists(v)) arquivos.Add(v);
                    else if (Directory.Exists(v))
                    {
                        foreach (string f in Directory.GetFiles(v))
                        {
                            string e = Path.GetExtension(f).ToLowerInvariant();
                            if (e == ".png" || e == ".jpg" || e == ".jpeg") arquivos.Add(f);
                        }
                        arquivos.Sort(StringComparer.Ordinal);
                    }
                    foreach (string f in arquivos)
                    {
                        if (_bases.Count >= 8) break;
                        var t = new Texture2D(2, 2, TextureFormat.RGB24, false) { name = "Fotos.Teste", hideFlags = HideFlags.DontSave };
                        if (t.LoadImage(File.ReadAllBytes(f), false))
                        {
                            t.wrapMode = TextureWrapMode.Clamp;
                            _bases.Add(t);
                        }
                        else UnityEngine.Object.Destroy(t);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("câmera de teste: " + e.Message);
            }
            if (_bases.Count == 0)
            {
                _bases.Add(Desenho(0));
                _bases.Add(Desenho(1));
            }
        }

        /// <summary>A próxima foto é outra criança.</summary>
        public void Proxima() => _atual = (_atual + 1) % _bases.Count;

        /// <summary>A mexida "ao vivo" (como na edição Godot: ±6 pontos para os lados).</summary>
        public float Deslocamento(float t) => Mathf.Sin(t * 2f) * 6f / Mathf.Max(1, Textura.width);

        /// <summary>Um rostinho de teste 320x240 (fundo de cor diferente para cada criança).</summary>
        static Texture2D Desenho(int crianca)
        {
            const int W = 320, H = 240;
            var px = new byte[W * H * 3];
            Color32 fundo = crianca % 2 == 0 ? new Color32(77, 153, 230, 255) : new Color32(120, 200, 110, 255);
            Color32 pele = new Color32(255, 204, 166, 255), cabelo = crianca % 2 == 0 ? new Color32(90, 55, 30, 255) : new Color32(240, 200, 60, 255);
            Color32 escuro = new Color32(40, 30, 40, 255), boca = new Color32(200, 60, 70, 255);
            for (int r = 0; r < H; r++)
            {
                int y = H - 1 - r;   // (a textura da Unity começa pela linha de baixo)
                for (int x = 0; x < W; x++)
                {
                    float fx = x - W * 0.5f, fy = y - H * 0.52f;
                    float rosto = fx * fx / (70f * 70f) + fy * fy / (88f * 88f);
                    float sombra = 0.75f + 0.25f * y / H;
                    Color32 c = new Color32((byte)(fundo.r * sombra), (byte)(fundo.g * sombra), (byte)(fundo.b * sombra), 255);
                    if (((x / 20) + (y / 20)) % 2 == 0 && rosto > 1.8f) c = new Color32((byte)(c.r * 0.85f), (byte)(c.g * 0.85f), (byte)(c.b * 0.85f), 255);
                    if (rosto < 1.25f && fy < -30f) c = cabelo;
                    if (rosto < 1f && !(fy < -48f)) c = pele;
                    float olhoE = (fx + 26f) * (fx + 26f) + (fy + 10f) * (fy + 10f);
                    float olhoD = (fx - 26f) * (fx - 26f) + (fy + 10f) * (fy + 10f);
                    if (rosto < 1f && (olhoE < 64f || olhoD < 64f)) c = escuro;
                    float sorriso = fx * fx + (fy - 18f) * (fy - 18f);
                    if (rosto < 1f && sorriso < 34f * 34f && sorriso > 26f * 26f && fy > 24f) c = boca;
                    if (y < 30 && x < 40) c = new Color32(230, 30, 30, 255);        // marca vermelha: em cima, à esquerda
                    if (y < 30 && x >= W - 40) c = new Color32(250, 220, 30, 255);  // marca amarela: em cima, à direita
                    int i = (r * W + x) * 3;
                    px[i] = c.r;
                    px[i + 1] = c.g;
                    px[i + 2] = c.b;
                }
            }
            var t = new Texture2D(W, H, TextureFormat.RGB24, false)
            {
                name = "Fotos.Teste" + crianca,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            t.SetPixelData(px, 0);
            t.Apply(false, false);
            return t;
        }

        public void Liberar()
        {
            foreach (var t in _bases)
                if (t != null) UnityEngine.Object.Destroy(t);
            _bases.Clear();
        }
    }
}
