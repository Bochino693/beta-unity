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
        public static readonly int ID_ESCURO = Shader.PropertyToID("_Escuro");
        public static readonly int ID_TEX = Shader.PropertyToID("_MainTex");

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

        public static Material Facetado => Comum("facetado", "Facetado");
        public static Material SemLuz => Comum("semluz", "SemLuz");
        public static Material Vidro => Comum("vidro", "Vidro");
        public static Material Chao => Comum("chao", "Chao");
        public static Material Lampadas => Comum("lampadas", "Lampadas");

        public static Material Madeira
        {
            get
            {
                if (_comuns.TryGetValue("madeira", out var m) && m != null) return m;
                m = Comum("madeira", "Textura");
                m.SetTexture(ID_TEX, Textura("madeira", true));
                m.SetFloat("_Brilho", 0.25f);
                return m;
            }
        }

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

        /// <summary>O material de uma parte de pacote (pelo nome gravado pelo modelador).</summary>
        public static Material DoPacote(string material)
        {
            switch (material)
            {
                case "": return Facetado;
                case "luz": return SemLuz;
                case "vidro": return Vidro;
                case "chao": return Chao;
                case "madeira": return Madeira;
                case "brilho": return Facetado;
                default:
                    if (material.StartsWith("textura:")) return TexturaComum(material.Substring(8));
                    return Facetado;
            }
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

        public static Color Hex(string h)
        {
            ColorUtility.TryParseHtmlString(h.StartsWith("#") ? h : "#" + h, out var c);
            return c;
        }

        public static Color Vezes(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, 1f);
    }
}
