using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StrikeKids
{
    /// <summary>
    /// AS PEÇAS DAS TELAS (uGUI montado por código), no jeito das edições Godot:
    /// cada peça tem posição do CANTO DE CIMA À ESQUERDA e tamanho, em unidades
    /// da tela lógica (lado menor = 1080, y para BAIXO); escala e giro são em
    /// volta do centro. Placas com volume (Placa), caixas arredondadas (Caixa),
    /// imagens (Figura), textos com contorno (Rotulo) e estrelinhas 2D.
    /// Tudo desenhado com malha (bordas lisas por degradê de 1 px), sem
    /// texturas por placa: as placas se juntam num desenho só.
    /// </summary>
    public static class Ui
    {
        public static readonly Color MARINHO = Materiais.Hex("#0B1F45");
        public static readonly Color SOMBRA = new Color(0f, 0.05f, 0.15f, 0.45f);

        static Font _fonte, _fonteTitulo;
        public static Font Fonte => _fonte != null ? _fonte : (_fonte = Carregar("fredoka_700"));
        public static Font FonteTitulo => _fonteTitulo != null ? _fonteTitulo : (_fonteTitulo = Carregar("titan_one"));

        static Font Carregar(string nome)
        {
            var f = Resources.Load<Font>("Fontes/" + nome);
            if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 32);
            return f;
        }

        // ------------------------------------------------------------ posição (canto de cima, y para baixo)
        public static RectTransform Novo(string nome, Transform pai)
        {
            var go = new GameObject(nome, typeof(RectTransform));
            go.layer = pai != null ? pai.gameObject.layer : 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(pai, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        public static T Novo<T>(string nome, Transform pai) where T : Component
        {
            var rt = Novo(nome, pai);
            return rt.gameObject.AddComponent<T>();
        }

        public static void Ret(RectTransform rt, float x, float y, float w, float h)
        {
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x + w * 0.5f, -(y + h * 0.5f));
        }

        public static void Ret(Component c, float x, float y, float w, float h) => Ret((RectTransform)c.transform, x, y, w, h);
        public static void Ret(Component c, Vector2 pos, Vector2 tam) => Ret((RectTransform)c.transform, pos.x, pos.y, tam.x, tam.y);

        public static Vector2 Pos(Component c)
        {
            var rt = (RectTransform)c.transform;
            var s = rt.sizeDelta;
            return new Vector2(rt.anchoredPosition.x - s.x * 0.5f, -rt.anchoredPosition.y - s.y * 0.5f);
        }

        public static void Pos(Component c, float x, float y)
        {
            var rt = (RectTransform)c.transform;
            var s = rt.sizeDelta;
            rt.anchoredPosition = new Vector2(x + s.x * 0.5f, -(y + s.y * 0.5f));
        }

        public static void Pos(Component c, Vector2 p) => Pos(c, p.x, p.y);

        public static Vector2 Tam(Component c) => ((RectTransform)c.transform).sizeDelta;

        /// <summary>Muda o tamanho mantendo o canto de cima à esquerda.</summary>
        public static void Tam(Component c, float w, float h)
        {
            var p = Pos(c);
            Ret((RectTransform)c.transform, p.x, p.y, w, h);
        }

        public static void Tam(Component c, Vector2 t) => Tam(c, t.x, t.y);

        public static Vector2 Centro(Component c) => Pos(c) + Tam(c) * 0.5f;

        public static void Escala(Component c, float e) => c.transform.localScale = new Vector3(e, e, 1f);
        public static void Escala(Component c, Vector2 e) => c.transform.localScale = new Vector3(e.x, e.y, 1f);
        public static float Escala(Component c) => c.transform.localScale.x;

        /// <summary>Giro em graus no sentido do relógio (como o rect_rotation do Godot).</summary>
        public static void Giro(Component c, float graus) => c.transform.localEulerAngles = new Vector3(0, 0, -graus);

        public static float Giro(Component c)
        {
            float z = c.transform.localEulerAngles.z;
            if (z > 180f) z -= 360f;
            return -z;
        }

        public static void Ver(Component c, bool v)
        {
            if (c != null && c.gameObject.activeSelf != v) c.gameObject.SetActive(v);
        }

        public static bool Visivel(Component c) => c != null && c.gameObject.activeSelf;

        /// <summary>Transparência da peça e dos filhos (o modulate.a do Godot).</summary>
        public static CanvasGroup Grupo(Component c)
        {
            var g = c.GetComponent<CanvasGroup>();
            if (g == null)
            {
                g = c.gameObject.AddComponent<CanvasGroup>();
                g.blocksRaycasts = false;
                g.interactable = false;
            }
            return g;
        }

        public static void Alfa(Component c, float a) => Grupo(c).alpha = a;
        public static float Alfa(Component c)
        {
            var g = c.GetComponent<CanvasGroup>();
            return g != null ? g.alpha : 1f;
        }

        /// <summary>Uma camada que se redesenha sozinha (mexer nela não refaz a tela toda).</summary>
        public static RectTransform Camada(string nome, Transform pai)
        {
            var rt = Novo(nome, pai);
            var c = rt.gameObject.AddComponent<Canvas>();
            // a 2ª coordenada (o círculo das fotos) também nas camadas
            c.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            return rt;
        }

        public static void Frente(Component c) => c.transform.SetAsLastSibling();

        // ------------------------------------------------------------ peças
        public static Rotulo Rotulo(Transform pai, string texto, float tam, Color cor, float contorno = 0f, Color? corContorno = null, bool titulo = false)
        {
            var r = Novo<Rotulo>("rotulo", pai);
            r.Configurar(texto, tam, cor, contorno, corContorno ?? MARINHO, titulo);
            return r;
        }

        public static Placa Placa(Transform pai, Color cor, float raio = 30f, float espessura = 10f, float borda = 5f)
        {
            var p = Novo<Placa>("placa", pai);
            p.Formato(raio, espessura, borda);
            p.Pintar(cor);
            return p;
        }

        public static Caixa Caixa(Transform pai, Color cor, float raio = 28f, float borda = 0f, Color? corBorda = null, float sombra = 10f)
        {
            var c = Novo<Caixa>("caixa", pai);
            c.Definir(cor, raio, borda, corBorda ?? Color.white, sombra);
            return c;
        }

        public static Figura Figura(Transform pai, Texture tex, bool manterProporcao = true)
        {
            var f = Novo<Figura>("figura", pai);
            f.texture = tex;
            f.manter = manterProporcao;
            f.raycastTarget = false;
            return f;
        }

        public static Image Retangulo(Transform pai, Color cor)
        {
            var i = Novo<Image>("retangulo", pai);
            i.color = cor;
            i.raycastTarget = false;
            return i;
        }

        // ------------------------------------------------------------ materiais das fotos
        static readonly Dictionary<Color, Material> _fotos = new Dictionary<Color, Material>();
        static Material _brilho;

        /// <summary>Foto redonda com aro colorido.</summary>
        public static Material MaterialFoto(Color aro)
        {
            if (_fotos.TryGetValue(aro, out var m) && m != null) return m;
            m = new Material(Materiais.AcharShader("UI/Foto")) { name = "foto_redonda" };
            m.SetColor("_CorAro", aro);
            _fotos[aro] = m;
            return m;
        }

        /// <summary>Um material do brilho que passa pela logo (cada imagem com o seu).</summary>
        public static Material MaterialBrilho()
        {
            var m = new Material(Materiais.AcharShader("UI/Brilho")) { name = "brilho_marca" };
            m.SetFloat("_Pos", -1f);
            return m;
        }

        // ------------------------------------------------------------ geometria (bordas lisas)
        static readonly List<Vector2> _a = new List<Vector2>(64), _b = new List<Vector2>(64), _c = new List<Vector2>(64), _d = new List<Vector2>(64);

        /// <summary>O contorno de um retângulo arredondado (y para cima), `seg` pedaços por canto.</summary>
        static void Contorno(List<Vector2> pts, Rect r, float raio, int seg)
        {
            pts.Clear();
            raio = Mathf.Clamp(raio, 0f, Mathf.Min(r.width, r.height) * 0.5f);
            var centros = new[]
            {
                new Vector2(r.xMax - raio, r.yMin + raio), new Vector2(r.xMax - raio, r.yMax - raio),
                new Vector2(r.xMin + raio, r.yMax - raio), new Vector2(r.xMin + raio, r.yMin + raio),
            };
            for (int k = 0; k < 4; k++)
            {
                float a0 = -90f + 90f * k;
                for (int i = 0; i <= seg; i++)
                {
                    float a = (a0 + 90f * i / seg) * Mathf.Deg2Rad;
                    pts.Add(centros[k] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * raio);
                }
            }
        }

        static Rect Crescer(Rect r, float d) => new Rect(r.xMin - d, r.yMin - d, Mathf.Max(r.width + 2f * d, 0f), Mathf.Max(r.height + 2f * d, 0f));

        public static int Segmentos(float raio) => Mathf.Clamp(Mathf.CeilToInt(raio / 3f), 2, 10);

        static void Vert(VertexHelper vh, Vector2 p, Color32 c)
        {
            vh.AddVert(new Vector3(p.x, p.y, 0f), c, Vector2.zero);
        }

        /// <summary>Retângulo arredondado cheio, com a borda lisa (degradê de `suave` px).</summary>
        public static void Cheio(VertexHelper vh, Rect r, float raio, Color cor, float suave = 1.2f)
        {
            if (r.width <= 0.5f || r.height <= 0.5f || cor.a <= 0.002f) return;
            int seg = Segmentos(raio);
            Contorno(_a, Crescer(r, -suave * 0.5f), raio - suave * 0.5f, seg);
            Contorno(_b, Crescer(r, suave * 0.5f), raio + suave * 0.5f, seg);
            Color32 c1 = cor;
            Color32 c0 = new Color(cor.r, cor.g, cor.b, 0f);
            int b0 = vh.currentVertCount;
            Vert(vh, r.center, c1);
            int n = _a.Count;
            for (int i = 0; i < n; i++) Vert(vh, _a[i], c1);
            for (int i = 0; i < n; i++) Vert(vh, _b[i], c0);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                vh.AddTriangle(b0, b0 + 1 + i, b0 + 1 + j);
                vh.AddTriangle(b0 + 1 + i, b0 + 1 + n + i, b0 + 1 + n + j);
                vh.AddTriangle(b0 + 1 + i, b0 + 1 + n + j, b0 + 1 + j);
            }
        }

        /// <summary>Só o aro (largura `w` para dentro do retângulo), bordas lisas dos dois lados.</summary>
        public static void Aro(VertexHelper vh, Rect r, float raio, float w, Color cor, float suave = 1.2f)
        {
            if (w <= 0.05f || r.width <= 1f || r.height <= 1f || cor.a <= 0.002f) return;
            int seg = Segmentos(raio);
            float h = Mathf.Min(suave * 0.5f, w * 0.5f);
            Contorno(_a, Crescer(r, h), raio + h, seg);                   // fora, transparente
            Contorno(_b, Crescer(r, -h), raio - h, seg);                  // fora, cheio
            Contorno(_c, Crescer(r, -(w - h)), raio - (w - h), seg);      // dentro, cheio
            Contorno(_d, Crescer(r, -(w + h)), raio - (w + h), seg);      // dentro, transparente
            Color32 c1 = cor;
            Color32 c0 = new Color(cor.r, cor.g, cor.b, 0f);
            int b0 = vh.currentVertCount;
            int n = _a.Count;
            for (int i = 0; i < n; i++) Vert(vh, _a[i], c0);
            for (int i = 0; i < n; i++) Vert(vh, _b[i], c1);
            for (int i = 0; i < n; i++) Vert(vh, _c[i], c1);
            for (int i = 0; i < n; i++) Vert(vh, _d[i], c0);
            for (int anel = 0; anel < 3; anel++)
            {
                int o0 = b0 + anel * n, o1 = b0 + (anel + 1) * n;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    vh.AddTriangle(o0 + i, o1 + i, o1 + j);
                    vh.AddTriangle(o0 + i, o1 + j, o0 + j);
                }
            }
        }

        /// <summary>Sombra macia: o retângulo cheio que esmaece até `tam` px para fora.</summary>
        public static void Sombra(VertexHelper vh, Rect r, float raio, float tam, Color cor)
        {
            if (cor.a <= 0.002f) return;
            if (tam <= 1f)
            {
                Cheio(vh, r, raio, cor);
                return;
            }
            int seg = Segmentos(raio + tam);
            Contorno(_a, r, raio, seg);
            Contorno(_b, Crescer(r, tam), raio + tam, seg);
            Color32 c1 = cor;
            Color32 c0 = new Color(cor.r, cor.g, cor.b, 0f);
            int b0 = vh.currentVertCount;
            Vert(vh, r.center, c1);
            int n = _a.Count;
            for (int i = 0; i < n; i++) Vert(vh, _a[i], c1);
            for (int i = 0; i < n; i++) Vert(vh, _b[i], c0);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                vh.AddTriangle(b0, b0 + 1 + i, b0 + 1 + j);
                vh.AddTriangle(b0 + 1 + i, b0 + 1 + n + i, b0 + 1 + n + j);
                vh.AddTriangle(b0 + 1 + i, b0 + 1 + n + j, b0 + 1 + j);
            }
        }

        /// <summary>Um retângulo do jeito Godot (canto de cima, y para baixo) dentro do rect local da peça.</summary>
        public static Rect DeCima(Rect local, float x, float y, float w, float h) => new Rect(local.xMin + x, local.yMax - y - h, w, h);

        public static Color Escurecer(Color c, float k) => new Color(c.r * (1f - k), c.g * (1f - k), c.b * (1f - k), c.a);
        public static Color Clarear(Color c, float k) => new Color(c.r + (1f - c.r) * k, c.g + (1f - c.g) * k, c.b + (1f - c.b) * k, c.a);
    }

    // ================================================================ placa com volume
    /// <summary>
    /// PLACA COM VOLUME (placar, botões, cartões, faixas): frente com brilho de
    /// verniz, lateral mais escura, contorno azul-marinho e sombra — em camadas:
    /// sombra, (halo do escolhido), contorno + lateral, frente, metade de cima
    /// mais clara, verniz, filete claro.
    /// </summary>
    public class Placa : MaskableGraphic
    {
        Color _cor = Materiais.Hex("#1A4FA8");
        Color _corBorda = Ui.MARINHO;
        float _brilho = 0.24f, _raio = 30f, _espessura = 10f, _borda = 5f, _realce, _sombra = 0.4f;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        /// <summary>Cor principal (a frente, a lateral e o brilho saem dela).</summary>
        public Placa Pintar(Color cor, Color? borda = null, float brilho = 0.24f)
        {
            _cor = cor;
            _corBorda = borda ?? Ui.MARINHO;
            _brilho = brilho;
            SetVerticesDirty();
            return this;
        }

        public Color Cor => _cor;

        public Placa Formato(float raio, float espessura, float borda)
        {
            _raio = raio;
            _espessura = espessura;
            _borda = borda;
            SetVerticesDirty();
            return this;
        }

        /// <summary>0..1: halo amarelo de "escolhido".</summary>
        public void Realcar(float v)
        {
            if (Mathf.Abs(v - _realce) < 0.002f) return;
            _realce = v;
            SetVerticesDirty();
        }

        public void Sombra(float forca)
        {
            _sombra = forca;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var L = rectTransform.rect;
            float tx = L.width, ty = L.height;
            if (tx < 2f || ty < 2f) return;
            float r = Mathf.Min(_raio, Mathf.Min(tx, ty) * 0.5f);
            float margem = Mathf.Max(12f, _raio * 0.9f + _espessura);
            // sombra macia embaixo
            if (_sombra > 0f)
                Ui.Sombra(vh, Ui.DeCima(L, margem * 0.1f, margem * 0.35f, tx - margem * 0.2f, ty - margem * 0.1f), r, margem * 0.45f,
                    new Color(0f, 0.03f, 0.10f, _sombra * 0.6f));
            // halo do item escolhido (por fora do contorno)
            if (_realce > 0f)
            {
                var am = new Color(1f, 0.84f, 0.25f);
                float e = margem * 0.3f;
                Ui.Sombra(vh, Ui.DeCima(L, -e, -e, tx + 2f * e, ty + 2f * e), r + margem * 0.35f, margem * 0.6f,
                    new Color(am.r, am.g, am.b, 0.85f * _realce));
            }
            // contorno escuro + lateral (a espessura embaixo)
            Ui.Cheio(vh, L, r, _corBorda);
            Ui.Cheio(vh, Ui.DeCima(L, _borda, _borda, tx - 2f * _borda, ty - 2f * _borda), Mathf.Max(r - _borda, 0f), Ui.Escurecer(_cor, 0.52f));
            // frente (acima da lateral)
            float baseF = Mathf.Max(ty - _borda - _espessura, _borda + 2f * r);
            float fx = _borda, fy = _borda, fw = tx - 2f * _borda, fh = baseF - _borda;
            float rf = Mathf.Max(r - _borda, 1f);
            Ui.Cheio(vh, Ui.DeCima(L, fx, fy, fw, fh), rf, Ui.Escurecer(_cor, 0.08f));
            // metade de cima um pouco mais clara (o degradê da frente)
            Ui.Cheio(vh, Ui.DeCima(L, fx, fy, fw, fh * 0.55f), Mathf.Min(rf, fh * 0.275f), Ui.Clarear(_cor, 0.16f));
            // verniz: brilho branco na parte de cima
            if (_brilho > 0f)
            {
                float mx = r * 0.3f + _borda;
                float vh0 = fh * 0.36f;
                Ui.Cheio(vh, Ui.DeCima(L, fx + mx - _borda, fy + fh * 0.07f, fw - 2f * (mx - _borda), vh0), Mathf.Min(r * 0.75f, vh0 * 0.5f),
                    new Color(1f, 1f, 1f, _brilho * 0.55f));
            }
            // filete claro por dentro do contorno (no escolhido, amarelo)
            var aro = Color.Lerp(new Color(1f, 1f, 1f, 0.35f), new Color(1f, 0.84f, 0.25f, 0.9f), _realce);
            float folga = _borda * 0.35f + 1f;
            Ui.Aro(vh, Ui.DeCima(L, fx + folga, fy + folga, fw - 2f * folga, fh - 2f * folga), Mathf.Max(rf - folga, 1f),
                Mathf.Max(Mathf.Round(_borda * 0.5f), 1f), aro);
            // a cor do Graphic multiplica tudo (modulate)
            if (color != Color.white)
            {
                var v = new UIVertex();
                for (int i = 0; i < vh.currentVertCount; i++)
                {
                    vh.PopulateUIVertex(ref v, i);
                    v.color = (Color32)((Color)v.color * color);
                    vh.SetUIVertex(v, i);
                }
            }
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }
    }

    // ================================================================ caixa arredondada simples
    /// <summary>Caixa arredondada (fundo, borda e sombra), como a StyleBoxFlat do Godot.</summary>
    public class Caixa : MaskableGraphic
    {
        Color _cor = Color.white, _corBorda = Color.white;
        float _raio = 28f, _borda, _sombra = 10f;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public Caixa Definir(Color cor, float raio, float borda = 0f, Color? corBorda = null, float sombra = 10f)
        {
            _cor = cor;
            _raio = raio;
            _borda = borda;
            _corBorda = corBorda ?? Color.white;
            _sombra = sombra;
            SetVerticesDirty();
            return this;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var L = rectTransform.rect;
            float r = Mathf.Min(_raio, Mathf.Min(L.width, L.height) * 0.5f);
            if (_sombra > 0f)
            {
                var s = L;
                s.y -= _sombra * 0.4f;
                Ui.Sombra(vh, s, r, _sombra, new Color(0f, 0.05f, 0.15f, 0.35f * color.a));
            }
            Ui.Cheio(vh, _borda > 0f ? Ui.DeCima(L, _borda * 0.5f, _borda * 0.5f, L.width - _borda, L.height - _borda) : L,
                Mathf.Max(r - _borda * 0.5f, 0f), _cor * new Color(1, 1, 1, color.a));
            if (_borda > 0f) Ui.Aro(vh, L, r, _borda, _corBorda * new Color(1, 1, 1, color.a));
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }
    }

    // ================================================================ imagem
    /// <summary>Imagem esticada (manter = false) ou inteira e centralizada no espaço (manter = true).</summary>
    public class Figura : RawImage
    {
        public bool manter = true;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            var tex = texture;
            vh.Clear();
            if (tex == null) return;
            var r = rectTransform.rect;
            if (manter && tex.width > 0 && tex.height > 0)
            {
                float asp = tex.width * uvRect.width / Mathf.Max(tex.height * uvRect.height, 1e-4f);
                float w = r.width, h = r.height;
                if (w / Mathf.Max(h, 1e-4f) > asp) w = h * asp;
                else h = w / asp;
                r = new Rect(r.center.x - w * 0.5f, r.center.y - h * 0.5f, w, h);
            }
            var uv = uvRect;
            Color32 c = color;
            // 2ª coordenada: 0..1 no quadrado da peça (o círculo das fotos)
            vh.AddVert(new Vector3(r.xMin, r.yMin), c, new Vector2(uv.xMin, uv.yMin), new Vector2(0, 0), Vector3.back, new Vector4(1, 0, 0, -1));
            vh.AddVert(new Vector3(r.xMin, r.yMax), c, new Vector2(uv.xMin, uv.yMax), new Vector2(0, 1), Vector3.back, new Vector4(1, 0, 0, -1));
            vh.AddVert(new Vector3(r.xMax, r.yMax), c, new Vector2(uv.xMax, uv.yMax), new Vector2(1, 1), Vector3.back, new Vector4(1, 0, 0, -1));
            vh.AddVert(new Vector3(r.xMax, r.yMin), c, new Vector2(uv.xMax, uv.yMin), new Vector2(1, 0), Vector3.back, new Vector4(1, 0, 0, -1));
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        /// <summary>Recorta o meio da imagem num quadrado (foto ao vivo da câmera).</summary>
        public void Quadrada()
        {
            var t = mainTexture;
            if (t == null || t.width <= 0 || t.height <= 0)
            {
                uvRect = new Rect(0, 0, 1, 1);
                return;
            }
            float a = (float)t.width / t.height;
            uvRect = a > 1f ? new Rect((1f - 1f / a) * 0.5f, 0f, 1f / a, 1f) : new Rect(0f, (1f - a) * 0.5f, 1f, a);
        }
    }

    // ================================================================ texto
    /// <summary>Texto com contorno grosso (cópias em volta) e sombra, no jeito dos rótulos do jogo.</summary>
    public class Rotulo : Text
    {
        public float contorno;
        public Color corContorno = Ui.MARINHO;
        public Color corSombra = Ui.SOMBRA;
        public float sombraDy;
        public float tamanhoBase;

        static readonly List<UIVertex> _e = new List<UIVertex>(512), _s = new List<UIVertex>(4096);

        public void Configurar(string t, float tam, Color cor, float cont, Color corCont, bool titulo)
        {
            font = titulo ? Ui.FonteTitulo : Ui.Fonte;
            text = t;
            color = cor;
            contorno = cont;
            corContorno = corCont;
            alignment = TextAnchor.MiddleCenter;
            horizontalOverflow = HorizontalWrapMode.Overflow;
            verticalOverflow = VerticalWrapMode.Overflow;
            raycastTarget = false;
            supportRichText = false;
            lineSpacing = 0.92f;
            Tamanho(tam);
        }

        /// <summary>Novo tamanho de letra (e, com `cont` &gt;= 0, o contorno junto). A sombra acompanha.</summary>
        public void Tamanho(float tam, float cont = -1f)
        {
            tamanhoBase = tam;
            fontSize = Mathf.Max(1, Mathf.RoundToInt(tam));
            if (cont >= 0f) contorno = cont;
            sombraDy = tam * 0.06f;
            SetVerticesDirty();
        }

        public void Estilo(float tam = -1f, float cont = -1f, Color? cor = null)
        {
            if (tam > 0f) Tamanho(tam, cont);
            else if (cont >= 0f) contorno = cont;
            if (cor.HasValue) color = cor.Value;
            SetVerticesDirty();
        }

        public void Alinhar(int h)
        {
            alignment = h < 0 ? TextAnchor.MiddleLeft : (h > 0 ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter);
        }

        public bool Quebrar
        {
            set => horizontalOverflow = value ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        }

        /// <summary>Largura do texto no tamanho de agora.</summary>
        public float Largura(string t = null)
        {
            var s = GetGenerationSettings(new Vector2(100000f, 100000f));
            s.horizontalOverflow = HorizontalWrapMode.Overflow;
            return cachedTextGeneratorForLayout.GetPreferredWidth(t ?? text, s) / Mathf.Max(pixelsPerUnit, 0.0001f);
        }

        /// <summary>Diminui a letra até o texto caber na largura (nunca estoura a placa).</summary>
        public void Caber(float tam, float largura)
        {
            Tamanho(tam);
            float porPx = Largura() / Mathf.Max(fontSize, 1);
            float t = tam;
            while (t > 12f && porPx * Mathf.Round(t) + contorno > largura) t *= 0.94f;
            Tamanho(t);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            base.OnPopulateMesh(vh);
            if (contorno <= 0.01f && sombraDy <= 0.01f) return;
            _e.Clear();
            vh.GetUIVertexStream(_e);
            if (_e.Count == 0) return;
            _s.Clear();
            float a = color.a;
            if (contorno > 0.01f)
            {
                float raio = contorno * 0.5f;
                int n = raio <= 2.5f ? 8 : (raio <= 6f ? 12 : 16);
                var cc = corContorno;
                cc.a *= a;
                Color32 c32 = cc;
                for (int k = 0; k < n; k++)
                {
                    float ang = Mathf.PI * 2f * k / n;
                    var d = new Vector3(Mathf.Cos(ang) * raio, Mathf.Sin(ang) * raio, 0f);
                    for (int i = 0; i < _e.Count; i++)
                    {
                        var v = _e[i];
                        v.position += d;
                        v.color = c32;
                        _s.Add(v);
                    }
                }
            }
            else if (sombraDy > 0.01f && corSombra.a > 0f)
            {
                var cs = corSombra;
                cs.a *= a;
                Color32 c32 = cs;
                var d = new Vector3(0f, -sombraDy, 0f);
                for (int i = 0; i < _e.Count; i++)
                {
                    var v = _e[i];
                    v.position += d;
                    v.color = c32;
                    _s.Add(v);
                }
            }
            _s.AddRange(_e);
            vh.Clear();
            vh.AddUIVertexTriangleStream(_s);
        }
    }

    // ================================================================ estrelinhas 2D
    /// <summary>Explosão de estrelinhas por cima das telas (some sozinha).</summary>
    public class Estrelas2D : MaskableGraphic
    {
        struct P
        {
            public Vector2 pos, vel;
            public float giro, ang, tam, vida;
            public Color cor;
        }

        P[] _p;
        float _t;
        Texture _tex;
        static readonly Color[] CORES = { Materiais.Hex("#FFE14D"), Materiais.Hex("#7FE7FF"), Materiais.Hex("#FF9F2E") };

        public override Texture mainTexture => _tex != null ? _tex : s_WhiteTexture;

        public static void Explodir(Transform pai, Vector2 centro, int n, float u = 1f)
        {
            var e = Ui.Novo<Estrelas2D>("estrelas", pai);
            Ui.Ret(e, 0, 0, 10, 10);
            e._tex = Materiais.Textura("estrela");
            e._p = new P[n];
            for (int i = 0; i < n; i++)
            {
                float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float v = UnityEngine.Random.Range(450f, 900f) * u;
                e._p[i] = new P
                {
                    pos = centro + UnityEngine.Random.insideUnitCircle * 60f * u,
                    vel = dir * v,
                    giro = UnityEngine.Random.Range(-360f, 360f),
                    ang = UnityEngine.Random.Range(0f, 360f),
                    tam = UnityEngine.Random.Range(0.12f, 0.3f) * 128f * u,
                    vida = UnityEngine.Random.Range(0.85f, 1f),
                    cor = Color.Lerp(CORES[0], CORES[UnityEngine.Random.Range(0, 3)], UnityEngine.Random.value),
                };
            }
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _t += dt;
            if (_t > 1.6f)
            {
                Destroy(gameObject);
                return;
            }
            for (int i = 0; i < _p.Length; i++)
            {
                _p[i].vel.y += 900f * dt;      // y para baixo: a gravidade puxa para baixo da tela
                _p[i].pos += _p[i].vel * dt;
                _p[i].ang += _p[i].giro * dt;
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_p == null) return;
            var L = rectTransform.rect;
            int k = 0;
            for (int i = 0; i < _p.Length; i++)
            {
                float u = _t / _p[i].vida;
                if (u >= 1f) continue;
                var c = _p[i].cor;
                c.a = 1f - u * u;
                Color32 c32 = c;
                // posição da tela (y para baixo) no rect local desta peça (canto de cima à esquerda do pai)
                var centro = new Vector2(L.xMin + _p[i].pos.x, L.yMax - _p[i].pos.y);
                float s = _p[i].tam * 0.5f;
                float a = _p[i].ang * Mathf.Deg2Rad;
                var ex = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s;
                var ey = new Vector2(-ex.y, ex.x);
                vh.AddVert(centro - ex - ey, c32, new Vector2(0, 0));
                vh.AddVert(centro - ex + ey, c32, new Vector2(0, 1));
                vh.AddVert(centro + ex + ey, c32, new Vector2(1, 1));
                vh.AddVert(centro + ex - ey, c32, new Vector2(1, 0));
                vh.AddTriangle(k, k + 1, k + 2);
                vh.AddTriangle(k + 2, k + 3, k);
                k += 4;
            }
        }
    }

    // ================================================================ animações das telas
    public enum Facil { Linear, Seno, QuadIn, QuadOut, Quad, BackOut, BackIn, ElasticoOut }

    /// <summary>
    /// ANIMAÇÕES DAS TELAS (como os tweens do Godot): passos em sequência, cada
    /// um com trilhas em paralelo; o valor inicial é lido quando o passo começa.
    /// Anda no tempo de verdade (a câmera lenta não segura as telas).
    /// </summary>
    public sealed class Tw
    {
        class Trilha
        {
            public float atraso, dur, t;
            public Action iniciar;
            public Action<float> aplicar;
            public Facil facil;
            public bool comecou;
        }

        class Passo
        {
            public readonly List<Trilha> trilhas = new List<Trilha>();
            public Action chamar;
        }

        readonly List<Passo> _passos = new List<Passo>();
        int _i;
        bool _paralelo, _junto;
        readonly UnityEngine.Object _dono;
        readonly bool _temDono;
        public bool Vivo { get; private set; } = true;

        Tw(UnityEngine.Object dono)
        {
            _dono = dono;
            _temDono = dono != null;
        }

        public static Tw Novo(UnityEngine.Object dono = null)
        {
            var t = new Tw(dono);
            MotorTw.Garantir().Add(t);
            return t;
        }

        /// <summary>Tudo que vier depois anda junto (set_parallel do Godot).</summary>
        public Tw Paralelo()
        {
            _paralelo = true;
            return this;
        }

        /// <summary>O próximo anda junto com o anterior (parallel() do Godot).</summary>
        public Tw Junto()
        {
            _junto = true;
            return this;
        }

        Trilha Nova(float dur, Action iniciar, Action<float> aplicar, Facil f)
        {
            var tr = new Trilha { dur = Mathf.Max(dur, 0f), iniciar = iniciar, aplicar = aplicar, facil = f };
            if ((_paralelo || _junto) && _passos.Count > 0 && _passos[_passos.Count - 1].chamar == null) _passos[_passos.Count - 1].trilhas.Add(tr);
            else
            {
                var p = new Passo();
                p.trilhas.Add(tr);
                _passos.Add(p);
            }
            _junto = false;
            return tr;
        }

        /// <summary>Atraso da última trilha (set_delay do Godot).</summary>
        public Tw Atraso(float s)
        {
            if (_passos.Count > 0)
            {
                var p = _passos[_passos.Count - 1];
                if (p.trilhas.Count > 0) p.trilhas[p.trilhas.Count - 1].atraso = s;
            }
            return this;
        }

        public Tw Valor(Func<float> ler, Action<float> por, float alvo, float dur, Facil f = Facil.Linear)
        {
            float de = 0f;
            Nova(dur, () => de = ler(), u => por(Mathf.LerpUnclamped(de, alvo, u)), f);
            return this;
        }

        public Tw Vetor(Func<Vector2> ler, Action<Vector2> por, Vector2 alvo, float dur, Facil f = Facil.Linear)
        {
            Vector2 de = Vector2.zero;
            Nova(dur, () => de = ler(), u => por(Vector2.LerpUnclamped(de, alvo, u)), f);
            return this;
        }

        /// <summary>Chama `f` com o valor de `de` até `ate` (tween_method do Godot).</summary>
        public Tw Metodo(Action<float> f, float de, float ate, float dur, Facil fa = Facil.Linear)
        {
            Nova(dur, null, u => f(Mathf.LerpUnclamped(de, ate, u)), fa);
            return this;
        }

        public Tw Escala(Component c, float alvo, float dur, Facil f = Facil.Linear) =>
            Vetor(() => (Vector2)c.transform.localScale, v => c.transform.localScale = new Vector3(v.x, v.y, 1f), new Vector2(alvo, alvo), dur, f);

        public Tw Escala(Component c, Vector2 alvo, float dur, Facil f = Facil.Linear) =>
            Vetor(() => (Vector2)c.transform.localScale, v => c.transform.localScale = new Vector3(v.x, v.y, 1f), alvo, dur, f);

        public Tw Giro(Component c, float graus, float dur, Facil f = Facil.Linear) => Valor(() => Ui.Giro(c), v => Ui.Giro(c, v), graus, dur, f);

        public Tw PosX(Component c, float x, float dur, Facil f = Facil.Linear) => Valor(() => Ui.Pos(c).x, v => Ui.Pos(c, v, Ui.Pos(c).y), x, dur, f);

        public Tw PosY(Component c, float y, float dur, Facil f = Facil.Linear) => Valor(() => Ui.Pos(c).y, v => Ui.Pos(c, Ui.Pos(c).x, v), y, dur, f);

        public Tw Pos(Component c, Vector2 p, float dur, Facil f = Facil.Linear) => Vetor(() => Ui.Pos(c), v => Ui.Pos(c, v), p, dur, f);

        public Tw TamY(Component c, float h, float dur, Facil f = Facil.Linear) => Valor(() => Ui.Tam(c).y, v => Ui.Tam(c, Ui.Tam(c).x, v), h, dur, f);

        public Tw Alfa(Component c, float a, float dur, Facil f = Facil.Linear) => Valor(() => Ui.Alfa(c), v => Ui.Alfa(c, v), a, dur, f);

        public Tw CorAlfa(Graphic g, float a, float dur, Facil f = Facil.Linear) =>
            Valor(() => g.color.a, v =>
            {
                var c = g.color;
                c.a = v;
                g.color = c;
            }, a, dur, f);

        public Tw Espera(float s)
        {
            var p = new Passo();
            p.trilhas.Add(new Trilha { dur = Mathf.Max(s, 0f) });
            _passos.Add(p);
            _junto = false;
            return this;
        }

        public Tw Chamar(Action a)
        {
            _passos.Add(new Passo { chamar = a });
            _junto = false;
            return this;
        }

        public void Matar() => Vivo = false;

        public static float Curva(Facil f, float t)
        {
            t = Mathf.Clamp01(t);
            switch (f)
            {
                case Facil.Seno: return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
                case Facil.QuadIn: return t * t;
                case Facil.QuadOut: return 1f - (1f - t) * (1f - t);
                case Facil.Quad: return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
                case Facil.BackOut:
                {
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    float x = t - 1f;
                    return 1f + c3 * x * x * x + c1 * x * x;
                }
                case Facil.BackIn:
                {
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    return c3 * t * t * t - c1 * t * t;
                }
                case Facil.ElasticoOut:
                {
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    const float p = 0.3f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - p / 4f) * (2f * Mathf.PI) / p) + 1f;
                }
                default: return t;
            }
        }

        /// <summary>Anda o tempo; false = acabou (ou o dono sumiu).</summary>
        internal bool Passar(float dt)
        {
            if (!Vivo) return false;
            if (_temDono && _dono == null)
            {
                Vivo = false;
                return false;
            }
            while (_i < _passos.Count)
            {
                var p = _passos[_i];
                if (p.chamar != null)
                {
                    _i++;
                    try { p.chamar(); }
                    catch (Exception e) { Debug.LogException(e); }
                    if (!Vivo) return false;
                    continue;
                }
                bool todas = true;
                foreach (var tr in p.trilhas)
                {
                    if (tr.t >= tr.atraso + tr.dur && tr.comecou) continue;
                    tr.t += dt;
                    if (tr.t < tr.atraso)
                    {
                        todas = false;
                        continue;
                    }
                    if (!tr.comecou)
                    {
                        tr.comecou = true;
                        tr.iniciar?.Invoke();
                    }
                    float u = tr.dur > 0f ? (tr.t - tr.atraso) / tr.dur : 1f;
                    tr.aplicar?.Invoke(Curva(tr.facil, Mathf.Min(u, 1f)));
                    if (u < 1f) todas = false;
                }
                if (!todas) return true;
                // sobra de tempo não passa para o próximo passo (como o Godot num quadro)
                _i++;
                dt = 0f;
            }
            Vivo = false;
            return false;
        }
    }

    /// <summary>Quem anda as animações das telas a cada quadro.</summary>
    public class MotorTw : MonoBehaviour
    {
        static MotorTw _i;
        readonly List<Tw> _lista = new List<Tw>();
        readonly List<Tw> _novos = new List<Tw>();

        public static MotorTw Garantir()
        {
            if (_i == null)
            {
                var go = new GameObject("MotorTw");
                DontDestroyOnLoad(go);
                _i = go.AddComponent<MotorTw>();
            }
            return _i;
        }

        public void Add(Tw t) => _novos.Add(t);

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_novos.Count > 0)
            {
                _lista.AddRange(_novos);
                _novos.Clear();
            }
            for (int i = _lista.Count - 1; i >= 0; i--)
                if (!_lista[i].Passar(dt)) _lista.RemoveAt(i);
        }
    }
}
