using System.Collections.Generic;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// MATERIAIS E LUZ. Sem luzes da Unity: a luz de cada fase (sol, céu, chão,
    /// contraluz, neblina) vai para os shaders como variáveis globais
    /// (Resources/Shaders/StrikeLuz.cginc). Os materiais comuns são um só
    /// para todas as peças (menos trocas de estado na placa de vídeo).
    /// </summary>
    public static class Materiais
    {
        static readonly Dictionary<string, Material> _comuns = new Dictionary<string, Material>();
        static readonly Dictionary<string, Texture2D> _texturas = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Shader> _shaders = new Dictionary<string, Shader>();

        static readonly int ID_LUZ_DIR = Shader.PropertyToID("_SkLuzDir");
        static readonly int ID_LUZ_COR = Shader.PropertyToID("_SkLuzCor");
        static readonly int ID_CEU = Shader.PropertyToID("_SkCeu");
        static readonly int ID_CHAO = Shader.PropertyToID("_SkChao");
        static readonly int ID_BORDA = Shader.PropertyToID("_SkBorda");
        static readonly int ID_NEBLINA = Shader.PropertyToID("_SkNeblina");
        public static readonly int ID_TINTA = Shader.PropertyToID("_Tinta");
        public static readonly int ID_FLASH = Shader.PropertyToID("_Flash");
        /// <summary>o destaque nos shaders da edição Godot (Shaders/Palco/G_*)</summary>
        public static readonly int ID_FLASH_G = Shader.PropertyToID("_flash");
        public static readonly int ID_ESCURO = Shader.PropertyToID("_Escuro");
        public static readonly int ID_TEX = Shader.PropertyToID("_MainTex");
        static readonly int ID_MATCAP = Shader.PropertyToID("_SkMatcap");
        static Texture2D _matcap;

        public static Shader AcharShader(string nome)
        {
            if (_shaders.TryGetValue(nome, out var s) && s != null) return s;
            s = UnityEngine.Shader.Find("StrikeKids/" + nome);
            if (s == null)
            {
                Debug.LogWarning("shader que falta: StrikeKids/" + nome);
                s = UnityEngine.Shader.Find("Unlit/Color") ?? UnityEngine.Shader.Find("Hidden/InternalErrorShader");
            }
            _shaders[nome] = s;
            return s;
        }

        public static Material Novo(string shader, string nome = null)
        {
            var m = new Material(AcharShader(shader)) { name = nome ?? shader };
            return m;
        }

        static Material Comum(string chave, string shader)
        {
            if (_comuns.TryGetValue(chave, out var m) && m != null) return m;
            m = Novo(shader, chave);
            _comuns[chave] = m;
            return m;
        }

        public static Material SemLuz => Comum("semluz", "SemLuz");
        public static Material Lampadas => Comum("lampadas", "Lampadas");

        public static Material TexturaComum(string nome, string shader = "Textura")
        {
            string chave = shader + ":" + nome;
            if (_comuns.TryGetValue(chave, out var m) && m != null) return m;
            m = Comum(chave, shader);
            m.SetTexture(ID_TEX, Textura(nome));
            return m;
        }

        public static Material Particula(string textura, bool somar = false)
        {
            return TexturaComum(textura, somar ? "ParticulaSoma" : "Particula");
        }

        public static Material Sombra(float forca)
        {
            string chave = "sombra:" + forca.ToString("0.00");
            if (_comuns.TryGetValue(chave, out var m) && m != null) return m;
            m = Comum(chave, "Sombra");
            m.SetTexture(ID_TEX, Textura("sombra"));
            m.SetFloat("_Forca", forca);
            return m;
        }

        /// <summary>O material de uma parte de pacote sem material próprio (as peças da edição
        /// Godot vêm com o material de cada uma: Palco, Personagem, Temas).</summary>
        public static Material DoPacote(string material)
        {
            if (material.StartsWith("textura:")) return TexturaComum(material.Substring(8));
            return SemLuz;
        }

        // ------------------------------------------------------------ texturas
        public static Texture2D Textura(string nome, bool repetir = false)
        {
            if (_texturas.TryGetValue(nome, out var t) && t != null) return t;
            t = Resources.Load<Texture2D>("Texturas/" + nome);
            if (t == null)
            {
                Debug.LogWarning("textura que falta: " + nome);
                t = Texture2D.whiteTexture;
            }
            else if (repetir) t.wrapMode = TextureWrapMode.Repeat;
            _texturas[nome] = t;
            return t;
        }

        public static Texture2D UI(string nome)
        {
            string chave = "ui:" + nome;
            if (_texturas.TryGetValue(chave, out var t) && t != null) return t;
            t = Resources.Load<Texture2D>("UI/" + nome);
            _texturas[chave] = t;
            return t;
        }

        /// <summary>Solta uma imagem das telas (as grandes, usadas de vez em quando).</summary>
        public static void SoltarUI(string nome)
        {
            string chave = "ui:" + nome;
            if (_texturas.TryGetValue(chave, out var t) && t != null) Resources.UnloadAsset(t);
            _texturas.Remove(chave);
        }

        // ------------------------------------------------------------ luz
        /// <summary>A luz da fase para todos os shaders. dirSol: direção PARA o sol.</summary>
        public static void DefinirLuz(Vector3 dirSol, Color sol, Color ceu, Color chao, Color borda, Color neblina, float densidade)
        {
            var d = dirSol.normalized;
            UnityEngine.Shader.SetGlobalVector(ID_LUZ_DIR, new Vector4(d.x, d.y, d.z, 0));
            UnityEngine.Shader.SetGlobalColor(ID_LUZ_COR, sol);
            UnityEngine.Shader.SetGlobalColor(ID_CEU, ceu);
            UnityEngine.Shader.SetGlobalColor(ID_CHAO, chao);
            UnityEngine.Shader.SetGlobalColor(ID_BORDA, borda);
            UnityEngine.Shader.SetGlobalColor(ID_NEBLINA, new Color(neblina.r, neblina.g, neblina.b, densidade));
        }

        /// <summary>
        /// O MATCAP das peças lustrosas (pinos e bola): o que a superfície reflete
        /// conforme a direção dela vista da câmera — o céu da fase em cima, a pista
        /// embaixo, o neon da fase na borda (contraluz) — e, no alfa, os pontos de
        /// brilho das luzes do teto. Feito aqui (128 px), a cada troca de fase:
        /// na placa de vídeo é uma leitura de textura por pixel, nada mais.
        /// </summary>
        public static void DefinirMatcap(Color ceu, Color horizonte, Color piso, Color neon)
        {
            const int T = 128;
            if (_matcap == null)
                _matcap = new Texture2D(T, T, TextureFormat.RGBA32, false)
                {
                    name = "matcap",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave,
                };
            var px = new Color32[T * T];
            for (int y = 0; y < T; y++)
            {
                for (int x = 0; x < T; x++)
                {
                    float nx = (x + 0.5f) / T * 2f - 1f, ny = (y + 0.5f) / T * 2f - 1f;
                    float q = nx * nx + ny * ny;
                    if (q > 1f)
                    {
                        float k = 1f / Mathf.Sqrt(q);
                        nx *= k;
                        ny *= k;
                        q = 1f;
                    }
                    float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - q));
                    // o raio da câmera refletido na superfície (y para cima, z para a câmera)
                    float ry = 2f * nz * ny, rz = 2f * nz * nz - 1f;
                    Color c = ry >= 0f
                        ? Color.Lerp(horizonte, ceu, Mathf.Pow(ry, 0.6f))
                        : Color.Lerp(horizonte * 0.85f, piso, Mathf.Clamp01(-ry * 3f));
                    float faixa = Mathf.Clamp01(1f - Mathf.Abs(ry) / 0.1f) * Mathf.Clamp01(-rz);
                    c = Color.Lerp(c, neon, faixa * 0.8f);
                    float a = Mathf.Clamp01(Mancha(nx, ny, -0.36f, 0.44f, 0.22f, 0.12f, 0.35f) +
                                            0.6f * Mancha(nx, ny, 0.5f, 0.3f, 0.09f, 0.07f, 0f));
                    c = Color.Lerp(c, Color.white, a * 0.6f);
                    px[y * T + x] = new Color32(B(c.r), B(c.g), B(c.b), B(a));
                }
            }
            _matcap.SetPixels32(px);
            _matcap.Apply(false, false);
            UnityEngine.Shader.SetGlobalTexture(ID_MATCAP, _matcap);
        }

        static byte B(float v) => (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);

        /// <summary>Mancha de luz (elipse girada, borda macia) no disco do matcap.</summary>
        static float Mancha(float x, float y, float cx, float cy, float rx, float ry, float giro)
        {
            float co = Mathf.Cos(giro), si = Mathf.Sin(giro);
            float dx = x - cx, dy = y - cy;
            float u = (dx * co + dy * si) / rx, v = (-dx * si + dy * co) / ry;
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, Mathf.Sqrt(u * u + v * v)));
        }

        public static Color Hex(string h)
        {
            ColorUtility.TryParseHtmlString(h.StartsWith("#") ? h : "#" + h, out var c);
            return c;
        }

        public static Color Vezes(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, 1f);
    }
}
