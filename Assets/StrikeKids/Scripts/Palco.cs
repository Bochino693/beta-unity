using System.Collections.Generic;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// O PALCO DA EDIÇÃO GODOT (strike-kids-gles2 7.6), trazido pelo
    /// tools/godot/exportar_palco.gd: a pista, a madeira da aproximação, as
    /// canaletas com o desenho de cada fase, os para-choques listrados, o poço
    /// do alçapão, o gabinete, a cortina da bola, o fundo estrelado, o arco de
    /// raios, as lâmpadas, as logos, os pódios, o símbolo de cada fase, a
    /// máquina de pinos e o sol, a lua e a baleia do céu — os mesmos
    /// triângulos, os mesmos shaders (Shaders/Palco/G_*) e as mesmas cores.
    ///
    ///   Resources/Palco/palco.json  as peças (pai, lugar já na Unity, malha,
    ///       material, "papel" = o nome da variável no mundo.gd/maquina.gd), os
    ///       materiais (shader e parâmetros do Godot), o que muda em cada fase
    ///       (parâmetros, peças que aparecem, a malha do astro) e a luz de palco
    ///       de cada fase
    ///   Resources/Malhas/palco.bytes  as malhas (m0, m1...)
    ///
    /// Os parâmetros ficam no espaço do Godot (os shaders fazem a conta do X);
    /// cada uniform "nome" do Godot é a propriedade "_nome" do material. A LUZ
    /// DE PALCO (luz_dir, luz_cor, ambiente: pinos, bichinhos, bola, gabinete)
    /// é global (Shader.SetGlobalVector), como o Materiais.definir_luz de lá.
    /// </summary>
    public class Palco
    {
        public readonly Transform raiz;
        readonly List<Transform> _nos = new List<Transform>();
        readonly List<Material> _mats = new List<Material>();
        readonly Dictionary<string, Transform> _noPorPapel = new Dictionary<string, Transform>();
        readonly Dictionary<string, Material> _matPorPapel = new Dictionary<string, Material>();
        readonly Dictionary<string, Dictionary<string, object>> _fases = new Dictionary<string, Dictionary<string, object>>();
        readonly Dictionary<string, Dictionary<string, object>> _luz = new Dictionary<string, Dictionary<string, object>>();
        readonly Dictionary<string, int> _astros = new Dictionary<string, int>();
        readonly List<Mesh> _malhas = new List<Mesh>();
        static readonly Dictionary<string, Texture2D> _texturas = new Dictionary<string, Texture2D>();

        static readonly int ID_LUZ_DIR = Shader.PropertyToID("_luz_dir");
        static readonly int ID_LUZ_COR = Shader.PropertyToID("_luz_cor");
        static readonly int ID_AMBIENTE = Shader.PropertyToID("_ambiente");

        /// <summary>A luz de palco de agora (no espaço do Godot), para quem faz material depois.</summary>
        public static Vector3 LuzDir = new Vector3(0.35f, 0.8f, 0.5f).normalized;

        Palco(Transform pai)
        {
            raiz = new GameObject("palco").transform;
            raiz.SetParent(pai, false);
        }

        public static Palco Montar(Transform pai)
        {
            var p = new Palco(pai);
            p.Ler();
            return p;
        }

        // ================================================================ montagem
        void Ler()
        {
            var ta = Resources.Load<TextAsset>("Palco/palco");
            if (ta == null)
            {
                Debug.LogWarning("falta Resources/Palco/palco.json (tools/godot/exportar_palco.gd)");
                return;
            }
            var d = Json.Dic(Json.Ler(ta.text));
            Resources.UnloadAsset(ta);
            var pc = Malhas.Ler("palco");
            foreach (var parte in pc.partes) _malhas.Add(parte.malha);
            // os astros se movem no shader (longe, na direção do sol): caixa enorme
            foreach (var kv in Json.Dic(d["astros"]))
            {
                int i = Json.Int(kv.Value, -1);
                _astros[kv.Key] = i;
                if (i >= 0 && i < _malhas.Count && _malhas[i] != null)
                    _malhas[i].bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            }
            var mats = Json.Lst(d["materiais"]);
            for (int i = 0; i < mats.Count; i++) _mats.Add(null);
            for (int i = 0; i < mats.Count; i++) Material(mats, i);
            foreach (var no in Json.Lst(d["nos"])) No(Json.Dic(no));
            foreach (var kv in Json.Dic(d["temas"])) _fases[kv.Key] = Json.Dic(kv.Value);
            foreach (var kv in Json.Dic(d["luz"])) _luz[kv.Key] = Json.Dic(kv.Value);
        }

        Material Material(List<object> lista, int i)
        {
            if (i < 0 || i >= _mats.Count) return null;
            if (_mats[i] != null) return _mats[i];
            var j = Json.Dic(lista[i]);
            string s = Json.Str(j["s"]);
            // (os do cenário — o pterossauro do símbolo — o Mundo põe os do Temas)
            if (s.StartsWith("toon_leve")) return null;
            var m = new Material(Materiais.AcharShader("G/" + s)) { name = "g_" + s + "_" + i };
            if (s == "spatial") Spatial(m, j);
            else if (j.TryGetValue("p", out var p)) Aplicar(m, Json.Dic(p));
            int prio = Json.Int(Json.Pegar(j, "prio", 0));
            if (prio != 0) m.renderQueue = m.shader.renderQueue + prio;
            _mats[i] = m;
            string papel = Json.Str(Json.Pegar(j, "papel", ""));
            if (papel != "" && !_matPorPapel.ContainsKey(papel)) _matPorPapel[papel] = m;
            // o next_pass do Godot (o contorno): desenhado como 2º material da peça
            if (j.TryGetValue("np", out var np))
            {
                int k = Json.Int(np, -1);
                if (Material(lista, k) != null) _np[i] = k;
            }
            return m;
        }

        /// <summary>O SpatialMaterial sem luz do Godot no G_spatial: cor, textura, transparente
        /// (mistura ou soma), corte e os lados.</summary>
        static void Spatial(Material m, Dictionary<string, object> j)
        {
            m.SetVector("_Cor", Vetor(j["cor"]));
            string tex = Json.Str(Json.Pegar(j, "tex", ""));
            if (tex != "") m.SetTexture("_MainTex", Textura(tex));
            float corte = (float)Json.Num(Json.Pegar(j, "corte", -1.0), -1.0);
            m.SetFloat("_Corte", corte);
            m.SetFloat("_CorVertice", Json.Bool(Json.Pegar(j, "cor_vertice", false)) ? 1f : 0f);
            bool transp = Json.Bool(Json.Pegar(j, "transparente", false));
            bool soma = Json.Bool(Json.Pegar(j, "soma", false));
            if (transp || soma)
            {
                m.SetFloat("_Origem", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_Destino", (float)(soma ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
                m.SetFloat("_Grava", 0f);
                m.renderQueue = 3000;
            }
            else if (corte >= 0f) m.renderQueue = 2450;
            // cull do Godot: 0 atrás (a frente aparece), 1 frente, 2 nenhum
            int cull = Json.Int(Json.Pegar(j, "cull", 0));
            m.SetFloat("_Lados", cull == 2 ? 0f : cull == 1 ? 1f : 2f);
        }

        void No(Dictionary<string, object> j)
        {
            var go = new GameObject(Json.Str(j["n"]));
            int pai = Json.Int(j["p"], -1);
            var t = go.transform;
            t.SetParent(pai >= 0 && pai < _nos.Count ? _nos[pai] : raiz, false);
            var tr = Json.Lst(j["t"]);
            t.localPosition = new Vector3(F(tr[0]), F(tr[1]), F(tr[2]));
            t.localRotation = new Quaternion(F(tr[3]), F(tr[4]), F(tr[5]), F(tr[6]));
            t.localScale = new Vector3(F(tr[7]), F(tr[8]), F(tr[9]));
            int mi = Json.Int(j["m"], -1);
            if (mi >= 0 && mi < _malhas.Count && _malhas[mi] != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = _malhas[mi];
                var mr = go.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                Vestir(mr, Json.Int(j["mat"], -1));
            }
            go.SetActive(Json.Bool(j["v"], true));
            _nos.Add(t);
            string papel = Json.Str(Json.Pegar(j, "papel", ""));
            if (papel != "" && !_noPorPapel.ContainsKey(papel)) _noPorPapel[papel] = t;
        }

        /// <summary>O material (e o passo seguinte dele, o contorno, como 2º material).</summary>
        void Vestir(MeshRenderer mr, int mat)
        {
            var m = mat >= 0 && mat < _mats.Count ? _mats[mat] : null;
            if (m == null)
            {
                mr.sharedMaterial = Materiais.SemLuz;
                return;
            }
            mr.sharedMaterials = _np.TryGetValue(mat, out var k) ? new[] { m, _mats[k] } : new[] { m };
        }

        readonly Dictionary<int, int> _np = new Dictionary<int, int>();

        // ================================================================ parâmetros
        /// <summary>Os parâmetros do Godot no material: número -> float, lista -> vetor,
        /// {"tex": nome} -> textura (Texturas/palco ou Texturas/cenario).</summary>
        public static void Aplicar(Material m, Dictionary<string, object> p)
        {
            if (m == null || p == null) return;
            foreach (var kv in p)
            {
                string nome = "_" + kv.Key;
                switch (kv.Value)
                {
                    case double v:
                        m.SetFloat(nome, (float)v);
                        break;
                    case bool b:
                        m.SetFloat(nome, b ? 1f : 0f);
                        break;
                    case List<object> l:
                        m.SetVector(nome, Vetor(l));
                        break;
                    case Dictionary<string, object> d when d.ContainsKey("tex"):
                        m.SetTexture(nome, Textura(Json.Str(d["tex"])));
                        break;
                }
            }
        }

        public static Vector4 Vetor(object o)
        {
            var l = Json.Lst(o);
            if (l == null) return Vector4.zero;
            var v = new Vector4(0, 0, 0, l.Count == 4 ? 1f : 0f);
            for (int i = 0; i < l.Count && i < 4; i++) v[i] = F(l[i]);
            return v;
        }

        static float F(object o) => (float)Json.Num(o);

        /// <summary>Uma textura do palco (Texturas/palco) ou do cenário (Texturas/cenario).</summary>
        public static Texture2D Textura(string nome)
        {
            if (_texturas.TryGetValue(nome, out var t) && t != null) return t;
            t = Resources.Load<Texture2D>("Texturas/palco/" + nome) ?? Resources.Load<Texture2D>("Texturas/cenario/" + nome);
            if (t == null)
            {
                Debug.LogWarning("textura do palco que falta: " + nome);
                t = Texture2D.whiteTexture;
            }
            _texturas[nome] = t;
            return t;
        }

        // ================================================================ consulta
        /// <summary>O nó pelo papel (o nome da variável no Godot: "_tiras", "_podios[0]",
        /// "_marcas.parque", "maquina.mesa"...) ou, se não tiver, pelo nome.</summary>
        public Transform No(string papelOuNome)
        {
            if (_noPorPapel.TryGetValue(papelOuNome, out var t)) return t;
            foreach (var n in _nos)
                if (n.name == papelOuNome) return n;
            return null;
        }

        public Material Mat(string papel) => _matPorPapel.TryGetValue(papel, out var m) ? m : null;

        public Mesh Malha(int i) => i >= 0 && i < _malhas.Count ? _malhas[i] : null;

        public Mesh Astro(string nome) => _astros.TryGetValue(nome, out var i) ? Malha(i) : null;

        // ================================================================ fases
        /// <summary>A fase no palco: os parâmetros que mudam, as peças que aparecem ou somem,
        /// a malha do astro e a luz de palco (global).</summary>
        public void AplicarFase(string fase)
        {
            if (_fases.TryGetValue(fase, out var f))
            {
                foreach (var kv in Json.Dic(f["mats"]))
                {
                    int i = int.Parse(kv.Key);
                    if (i >= 0 && i < _mats.Count) Aplicar(_mats[i], Json.Dic(kv.Value));
                }
                foreach (var kv in Json.Dic(f["nos"]))
                {
                    int i = int.Parse(kv.Key);
                    if (i < 0 || i >= _nos.Count) continue;
                    var l = Json.Lst(kv.Value);
                    _nos[i].gameObject.SetActive(Json.Bool(l[0], true));
                    int mi = Json.Int(l[1], -1);
                    var mf = _nos[i].GetComponent<MeshFilter>();
                    if (mf != null && mi >= 0) mf.sharedMesh = Malha(mi);
                }
            }
            if (_luz.TryGetValue(fase, out var luz)) DefinirLuz(Vetor(luz["luz_dir"]), Vetor(luz["luz_cor"]), Vetor(luz["ambiente"]));
        }

        /// <summary>A luz de palco (no espaço do Godot): direção PARA o sol, cor e ambiente
        /// lineares, já com a força.</summary>
        public static void DefinirLuz(Vector3 dir, Vector3 cor, Vector3 ambiente)
        {
            LuzDir = dir.normalized;
            Shader.SetGlobalVector(ID_LUZ_DIR, LuzDir);
            Shader.SetGlobalVector(ID_LUZ_COR, cor);
            Shader.SetGlobalVector(ID_AMBIENTE, ambiente);
        }

        /// <summary>A luz de palco padrão (a do Materiais.gd antes de qualquer fase): os
        /// shaders nunca leem zero (o normalize daria NaN).</summary>
        public static void LuzPadrao() => DefinirLuz(new Vector3(0.35f, 0.8f, 0.5f), new Vector3(1.05f, 0.935f, 0.745f), new Vector3(0.212f, 0.278f, 0.439f));
    }
}
