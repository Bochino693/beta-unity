using System;
using System.Collections.Generic;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// OS 4 CENÁRIOS — um por rodada (e a atração vai passando por todos), como
    /// na edição Godot 7.6 (strike-kids-gles2, a que roda na TV Box):
    ///   parque — Parque de Diversões: roda-gigante, carrossel, tendas, balões
    ///   espaco — Viagem ao Espaço: Saturno, foguetes, disco voador, satélite
    ///   mar    — Fundo do Mar: navio pirata, baú do tesouro, corais, peixinhos
    ///   dinos  — Selva dos Dinos: vulcão, dinos, tricerátops, pterossauros
    ///
    /// O PLANO (onde fica cada peça, o nível de detalhe, o que se mexe, as
    /// vozes e as partículas) é o PlanoCenario.cs — a cópia do temas.gd, nas
    /// coordenadas do Godot, conferida peça por peça contra ele
    /// (tools/godot/conferir_cenario.sh). Aqui ele vira Unity: as malhas
    /// exportadas da edição Godot (Malhas/cenario, 3 níveis cada), o atlas de
    /// cada fase (Texturas/cenario/atlas_*), os shaders CenarioAtlas*, a luz
    /// do cenário, o X espelhado (Medidas.G), as animações a cada quadro, os
    /// sons de ambiente e as partículas (Efeitos).
    ///
    /// O que fica parado em cada fase vira poucas malhas (lote estático da
    /// Unity); as peças paradas com versão mais leve têm as duas, uma no nó
    /// "alto" e a gêmea no "leve" (o nível do cenário: trocar é ligar um e
    /// desligar o outro); as que se mexem trocam de malha.
    /// </summary>
    public class Temas : MonoBehaviour
    {
        /// <summary>a Selva dos Dinos é sempre a PRIMEIRA fase (e o começo da atração)</summary>
        public static readonly string[] ORDEM = PlanoCenario.ORDEM;

        public class Config
        {
            public Color[] ceu;            // topo, meio, horizonte, chão
            // a luz do PALCO desta edição (Facetado: pista, pinos, bichinhos)
            public Color ambiente; public float ambienteForca;
            public Color neblina; public float neblinaDensidade;
            public Color sol; public float solForca;
            public Color corSol = new Color(1f, 0.95f, 0.75f); public float forcaSol = 1f;
            public float estrelas, raios;
            public Color nebulosa = new Color(0.9f, 0.4f, 1f);
            public Color neon, bumper1, bumper2;
            public Color[] chao;           // as cores do chão (Terreno.shader)
            public int chaoModo;           // 0 gramado, 1 lua, 2 areia, 3 selva
            public Color[] portal;
            // a luz da DECORAÇÃO e do chão: a da edição Godot 7.6 (temas.gd CONFIG)
            public float ambienteForcaCenario, solForcaCenario, neblinaDensidadeCenario;
        }

        static Color H(string h) => Materiais.Hex(h);

        public static readonly Dictionary<string, Config> CONFIG = new Dictionary<string, Config>
        {
            ["parque"] = new Config
            {
                ceu = new[] { H("#2F7FFF"), H("#6CBBFF"), H("#E4F4FF"), H("#8CD18C") },
                ambiente = new Color(0.62f, 0.7f, 0.86f), ambienteForca = 0.62f,
                neblina = new Color(0.84f, 0.92f, 1.0f), neblinaDensidade = 0.0045f,
                sol = new Color(1.0f, 0.95f, 0.86f), solForca = 1.05f,
                neon = H("#FF4FC8"), bumper1 = H("#FFFFFF"), bumper2 = H("#FF3B50"),
                chao = new[] { H("#6BCB5D"), H("#5BB54E"), H("#FFFFFF"), H("#FFE873") }, chaoModo = 0,
                portal = new[] { H("#FFC21A"), H("#E8384F") },
                ambienteForcaCenario = 0.68f, solForcaCenario = 0.9f, neblinaDensidadeCenario = 0.0035f,
            },
            ["espaco"] = new Config
            {
                ceu = new[] { H("#040A26"), H("#0E2560"), H("#2C6AB8"), H("#14244E") },
                estrelas = 1f, nebulosa = H("#2FC8FF"), corSol = H("#CFE0FF"), forcaSol = 0.35f,
                ambiente = new Color(0.6f, 0.7f, 1.0f), ambienteForca = 0.78f,
                neblina = new Color(0.14f, 0.24f, 0.5f), neblinaDensidade = 0.006f,
                sol = new Color(0.9f, 0.94f, 1.0f), solForca = 1.05f,
                neon = H("#4FE3FF"), bumper1 = H("#FFFFFF"), bumper2 = H("#3B8BFF"),
                chao = new[] { H("#9AA8C4"), H("#8494B3"), H("#C3CEE2"), H("#E2F5FF") }, chaoModo = 1,
                portal = new[] { H("#4FD8FF"), H("#3B2FA8") },
                ambienteForcaCenario = 0.78f, solForcaCenario = 0.92f, neblinaDensidadeCenario = 0.0045f,
            },
            ["mar"] = new Config
            {
                ceu = new[] { H("#0A4F86"), H("#1A94C0"), H("#86E6DE"), H("#E8D29A") },
                raios = 1f, corSol = H("#E6FFF8"), forcaSol = 0.5f,
                ambiente = new Color(0.5f, 0.82f, 0.92f), ambienteForca = 0.72f,
                neblina = new Color(0.32f, 0.72f, 0.84f), neblinaDensidade = 0.014f,
                sol = new Color(0.88f, 1.0f, 0.96f), solForca = 1.0f,
                neon = H("#5CFFD8"), bumper1 = H("#FFFFFF"), bumper2 = H("#FF7A5C"),
                chao = new[] { H("#F0C47C"), H("#E0AC62"), H("#FF9A86"), H("#7CD2E0") }, chaoModo = 2,
                portal = new[] { H("#2EE6C8"), H("#FF7A5C") },
                ambienteForcaCenario = 0.72f, solForcaCenario = 0.86f, neblinaDensidadeCenario = 0.0105f,
            },
            ["dinos"] = new Config
            {
                ceu = new[] { H("#3FA7E8"), H("#9BD8F0"), H("#FFE4AE"), H("#6BBF59") },
                corSol = H("#FFE7B0"),
                ambiente = new Color(0.72f, 0.72f, 0.62f), ambienteForca = 0.62f,
                neblina = new Color(0.92f, 0.9f, 0.78f), neblinaDensidade = 0.006f,
                sol = new Color(1.0f, 0.93f, 0.8f), solForca = 1.1f,
                neon = H("#B6FF3B"), bumper1 = H("#FFE65A"), bumper2 = H("#2E9A3E"),
                chao = new[] { H("#46A44E"), H("#57B948"), H("#FF9F33"), H("#FFE65A") }, chaoModo = 3,
                portal = new[] { H("#9BE04A"), H("#FF8A1E") },
                ambienteForcaCenario = 0.68f, solForcaCenario = 0.92f, neblinaDensidadeCenario = 0.0045f,
            },
        };

        public static string Proximo(string nome) => PlanoCenario.Proximo(nome);

        /// <summary>A altura do chão (com os patamares), em coordenadas da Unity.</summary>
        public static float AlturaTerreno(float x, float z) => (float)PlanoCenario.AlturaTerreno(-x, z);

        // ------------------------------------------------------------ estado
        public string atual = "";
        /// <summary>Os sons de ambiente só quando o principal deixa (na espera da jogada, na
        /// abertura e nas telas; não durante a bola, o resultado e o replay).</summary>
        public bool somAmbiente;
        /// <summary>O nível do cenário: alto (as versões caprichadas) ou leve (um passo mais
        /// leve em cada peça).</summary>
        public bool cenarioLeve { get; private set; }
        public bool detalhes { get; private set; } = true;

        Mundo _mundo;
        PlanoCenario _plano;
        readonly Dictionary<string, GameObject> _grupos = new Dictionary<string, GameObject>();
        readonly Dictionary<string, GameObject> _alto = new Dictionary<string, GameObject>();
        readonly Dictionary<string, GameObject> _leve = new Dictionary<string, GameObject>();
        readonly Dictionary<string, List<No>> _mexem = new Dictionary<string, List<No>>();
        readonly List<No> _trocam = new List<No>();
        readonly Dictionary<string, List<Efeitos.Emissor>> _emissores = new Dictionary<string, List<Efeitos.Emissor>>();
        float _t, _relogio, _proxAmb = 4f;

        const float AMB_FOLGA_MIN = 3.0f, AMB_FOLGA_MAX = 5.5f;
        const float AMB_VOLUME = 0.6f;

        // ------------------------------------------------------------ malhas
        /// <summary>As malhas do cenário para o plano: as caixas e os pontos nas coordenadas do Godot.</summary>
        class Modelos : IModelosCenario
        {
            readonly Dictionary<string, bool> _existe = new Dictionary<string, bool>();

            public static Mesh Malha(string nome)
            {
                var pc = Malhas.Ler("cenario/" + nome);
                foreach (var p in pc.partes) if (p.malha != null) return p.malha;
                return null;
            }

            public bool Existe(string nome)
            {
                if (!_existe.TryGetValue(nome, out bool e))
                {
                    var ta = Resources.Load<TextAsset>("Malhas/cenario/" + nome);
                    e = ta != null;
                    if (ta != null) Resources.UnloadAsset(ta);
                    _existe[nome] = e;
                }
                return e;
            }

            public CaixaG CaixaDe(string nome)
            {
                var pc = Malhas.Ler("cenario/" + nome);
                bool primeira = true;
                var b = new Bounds();
                foreach (var p in pc.partes)
                {
                    if (p.malha == null) continue;
                    if (primeira) { b = p.malha.bounds; primeira = false; }
                    else b.Encapsulate(p.malha.bounds);
                }
                if (primeira) return new CaixaG();
                // (a malha da Unity tem o X espelhado)
                var min = b.min;
                var max = b.max;
                return new CaixaG(new V3(-max.x, min.y, min.z), new V3(max.x - min.x, max.y - min.y, max.z - min.z));
            }

            public V3[] Pontos(string nome)
            {
                var m = Malha(nome);
                if (m == null) return null;
                var vs = m.vertices;
                var r = new V3[vs.Length];
                for (int i = 0; i < vs.Length; i++) r[i] = new V3(-vs[i].x, vs[i].y, vs[i].z);
                return r;
            }
        }

        readonly Dictionary<string, Mesh> _malhasVento = new Dictionary<string, Mesh>();
        readonly HashSet<Mesh> _usadas = new HashSet<Mesh>();

        /// <summary>A malha de uma peça (com o balanço do vento no UV3, se ela balança).</summary>
        Mesh MalhaDa(No n, string nome)
        {
            var m = Modelos.Malha(nome);
            if (m == null) return null;
            if (n.vento != null) m = ComVento(m, nome, n.vento);
            _usadas.Add(m);
            return m;
        }

        /// <summary>VENTO NAS PLANTAS: nada no pé, crescendo com o quadrado da altura até
        /// `cfg[0]` metros no topo; y = a rapidez. Uma cópia por malha.</summary>
        Mesh ComVento(Mesh m, string nome, float[] cfg)
        {
            if (_malhasVento.TryGetValue(nome, out var feita)) return feita;
            var nova = Instantiate(m);
            nova.name = m.name + "_vento";
            var v = nova.vertices;
            var b = nova.bounds;
            float alt = Mathf.Max(b.size.y, 0.001f);
            var uv = new List<Vector2>(v.Length);
            for (int i = 0; i < v.Length; i++)
            {
                float w = Mathf.Clamp01((v[i].y - b.min.y) / alt);
                uv.Add(new Vector2(cfg[0] * w * w, cfg[1]));
            }
            nova.SetUVs(2, uv);
            nova.UploadMeshData(false);
            _malhasVento[nome] = nova;
            return nova;
        }

        // ------------------------------------------------------------ materiais
        /// <summary>Os parâmetros de luz de um material do atlas (para as contas da CPU).</summary>
        class Luzes
        {
            public Color tinta = Color.white;
            public float luzMinima;
            public Color corBorda = new Color(1f, 0.95f, 0.9f);
            public float borda = 0.16f;
            public float brilhoLuz = 1.4f;
        }

        readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();
        readonly Dictionary<Material, Luzes> _luzes = new Dictionary<Material, Luzes>();
        readonly Dictionary<string, Texture2D> _atlas = new Dictionary<string, Texture2D>();
        Material _matNuvem, _matFio, _matBorboleta;
        static readonly int ID_FSOL = Shader.PropertyToID("_FSol");
        static readonly int ID_FSOMBRA = Shader.PropertyToID("_FSombra");
        static readonly int ID_FBORDA_SOL = Shader.PropertyToID("_FBordaSol");
        static readonly int ID_FBORDA_SOMBRA = Shader.PropertyToID("_FBordaSombra");
        static readonly int ID_FACESA = Shader.PropertyToID("_FAcesa");
        static readonly int ID_ASA_TEMPO = Shader.PropertyToID("_AsaTempo");
        static readonly int ID_ASA_AMPLITUDE = Shader.PropertyToID("_AsaAmplitude");
        static readonly int ID_CN_SOL = Shader.PropertyToID("_CnSolDir");
        static readonly int ID_CN_NEBLINA = Shader.PropertyToID("_CnNeblina");
        static readonly int ID_CN_NEBLINA_TELA = Shader.PropertyToID("_CnNeblinaTela");

        Texture2D Atlas(string tema)
        {
            if (!_atlas.TryGetValue(tema, out var t))
            {
                t = Resources.Load<Texture2D>("Texturas/cenario/atlas_" + tema);
                if (t == null) Debug.LogWarning("textura que falta: Texturas/cenario/atlas_" + tema);
                _atlas[tema] = t;
            }
            return t;
        }

        Material NovoAtlas(string shader, string tema, Luzes l, string nome)
        {
            var m = Materiais.Novo(shader, nome);
            var t = Atlas(tema);
            if (t != null) m.SetTexture(Materiais.ID_TEX, t);
            _luzes[m] = l;
            if (_luzAtual) Fatores(m, l);
            return m;
        }

        /// <summary>O material de uma peça do atlas: um para todas as peças iguais (o lote junta
        /// as paradas num desenho por região).</summary>
        Material MaterialAtlas(No n)
        {
            var l = new Luzes { tinta = new Color(n.tinta.r, n.tinta.g, n.tinta.b, n.tinta.a), luzMinima = n.luzMinima, borda = n.borda };
            string shader = n.vento != null ? "CenarioAtlasVento" : "CenarioAtlas";
            if (n.materialProprio) return NovoAtlas(shader, n.temaAtlas, l, "atlas_" + n.temaAtlas + "_proprio");
            string chave = n.temaAtlas + "|" + shader + "|" + n.tinta.Html() + "|" + n.borda.ToString("0.###") + "|" + n.luzMinima.ToString("0.###");
            if (!_mats.TryGetValue(chave, out var m))
            {
                m = NovoAtlas(shader, n.temaAtlas, l, "atlas_" + n.temaAtlas);
                _mats[chave] = m;
            }
            return m;
        }

        /// <summary>O material do pterodáctilo que bate as asas: um para cada bicho (cada um no seu ritmo).</summary>
        public Material MatAsas(string tema) => NovoAtlas("CenarioAtlasAsas", tema, new Luzes(), "asas_" + tema);

        // NUVENS BRANCAS (7.5): luz própria de céu — sol branco e a sombra azul-clara —
        // igual em todas as fases, sem neblina
        static readonly Vector4 NUVEM_SOL = new Vector4(0.66f, 0.66f, 0.66f, 0f);
        static readonly Vector4 NUVEM_AMBIENTE = new Vector4(0.44f, 0.47f, 0.55f, 0f);

        Material MatNuvem()
        {
            if (_matNuvem == null)
            {
                _matNuvem = Materiais.Novo("CenarioCor", "nuvem");
                _matNuvem.SetFloat("_Borda", 0.6f);
                _matNuvem.SetFloat("_LuzMinima", 0.5f);
                _matNuvem.SetColor("_CorBorda", new Color(0.9f, 0.96f, 1f));
                _matNuvem.SetVector("_SolCor", NUVEM_SOL);
                _matNuvem.SetVector("_Ambiente", NUVEM_AMBIENTE);
                _matNuvem.SetFloat("_NeblinaDensidade", 0f);
            }
            return _matNuvem;
        }

        Material MatFio()
        {
            if (_matFio == null)
            {
                _matFio = Materiais.Novo("CenarioCor", "fio");
                _matFio.SetFloat("_Borda", 0f);
            }
            return _matFio;
        }

        // ------------------------------------------------------------ a luz
        bool _luzAtual;
        Vector3 _solCor, _ambiente;

        static Vector3 S2L(Vector3 c) => Vector3.Scale(c, Vector3.Scale(c, c * 0.305306011f + Vector3.one * 0.682171111f) + Vector3.one * 0.012522878f);

        /// <summary>A cor linear como sai na tela (a CnTela dos shaders), aqui na CPU.</summary>
        public static Vector3 Tela(Vector3 c)
        {
            var x = new Vector3(Mathf.Clamp(c.x, 0f, 1.25f), Mathf.Clamp(c.y, 0f, 1.25f), Mathf.Clamp(c.z, 0f, 1.25f));
            x = Vector3.Scale(x, Vector3.one * 1.2094f - Vector3.Scale(x, Vector3.one * 0.015f + x * 0.25f));
            float lum = Vector3.Dot(x, new Vector3(0.299f, 0.587f, 0.114f));
            float sat = Mathf.Max(x.x, Mathf.Max(x.y, x.z)) - Mathf.Min(x.x, Mathf.Min(x.y, x.z));
            x = Vector3.one * lum + (x - Vector3.one * lum) * (1.14f + 0.34f * (1f - sat));
            x = x * 1.08f - Vector3.one * 0.045f;
            return new Vector3(Mathf.Clamp01(x.x), Mathf.Clamp01(x.y), Mathf.Clamp01(x.z));
        }

        static Vector3 V(Color c) => new Vector3(c.r, c.g, c.b);
        static Vector4 V4(Vector3 v) => new Vector4(v.x, v.y, v.z, 0f);

        const float MEIO = 0.25f;
        const float TELA_MEIO = 0.276307f;

        /// <summary>A LUZ DO MATERIAL JÁ COMO SAI NA TELA: no sol, na sombra, o quanto a borda
        /// clara soma em cada uma e a das partes que brilham.</summary>
        void Fatores(Material m, Luzes l)
        {
            var tin = S2L(V(l.tinta)) * MEIO;
            var b = S2L(V(l.corBorda)) * l.borda;
            var noSol = _ambiente + _solCor;
            var naSombra = _ambiente + _solCor * l.luzMinima;
            var fSol = Tela(Vector3.Scale(tin, noSol)) / TELA_MEIO;
            var fSombra = Tela(Vector3.Scale(tin, naSombra)) / TELA_MEIO;
            m.SetVector(ID_FSOL, V4(fSol));
            m.SetVector(ID_FSOMBRA, V4(fSombra));
            m.SetVector(ID_FBORDA_SOL, V4(Tela(Vector3.Scale(tin, noSol + b)) / TELA_MEIO - fSol));
            m.SetVector(ID_FBORDA_SOMBRA, V4(Tela(Vector3.Scale(tin, naSombra + b)) / TELA_MEIO - fSombra));
            m.SetVector(ID_FACESA, V4(Tela(Vector3.Scale(tin, noSol + Vector3.one * l.brilhoLuz)) / TELA_MEIO));
        }

        /// <summary>A força da luz própria (as partes acesas) de um material do atlas.</summary>
        void Acender(Material m, float brilho)
        {
            if (!_luzAtual || m == null || !_luzes.TryGetValue(m, out var l)) return;
            var tin = S2L(V(l.tinta)) * MEIO;
            m.SetVector(ID_FACESA, V4(Tela(Vector3.Scale(tin, _ambiente + _solCor + Vector3.one * brilho)) / TELA_MEIO));
        }

        /// <summary>A luz da cena para a decoração (Mundo.AplicarTema): para onde está o sol
        /// (coordenadas da Unity), cor x força do sol e do ambiente e a neblina (lineares).</summary>
        public void Luz(Vector3 solDir, Vector3 solCor, Vector3 ambiente, Vector3 neblinaCor, float densidade)
        {
            _luzAtual = true;
            _solCor = solCor;
            _ambiente = ambiente;
            var d = solDir.normalized;
            Shader.SetGlobalVector(ID_CN_SOL, new Vector4(d.x, d.y, d.z, 0f));
            Shader.SetGlobalVector(ID_CN_NEBLINA, new Vector4(neblinaCor.x, neblinaCor.y, neblinaCor.z, densidade));
            Shader.SetGlobalVector(ID_CN_NEBLINA_TELA, V4(Tela(neblinaCor)));
            foreach (var k in _luzes) Fatores(k.Key, k.Value);
            var fio = MatFio();
            fio.SetVector("_SolCor", V4(solCor));
            fio.SetVector("_Ambiente", V4(ambiente));
            fio.SetVector("_NeblinaCor", V4(neblinaCor));
            fio.SetFloat("_NeblinaDensidade", densidade);
        }

        // ------------------------------------------------------------ montagem
        public void Construir(Mundo mundo, bool leve, bool detalhes_, bool cenario3dLeve)
        {
            _mundo = mundo;
            detalhes = detalhes_;
            cenarioLeve = cenario3dLeve;
            float t0 = Time.realtimeSinceStartup;
            _plano = new PlanoCenario(new Modelos(), leve, detalhes);
            _plano.Construir();
            foreach (var w in _plano.avisos) Debug.LogWarning(w);
            foreach (var nome in ORDEM)
            {
                var g = new GameObject(nome);
                g.transform.SetParent(transform, false);
                _grupos[nome] = g;
                var alto = new GameObject("alto");
                alto.transform.SetParent(g.transform, false);
                _alto[nome] = alto;
                var lev = new GameObject("leve");
                lev.transform.SetParent(g.transform, false);
                _leve[nome] = lev;
                _mexem[nome] = new List<No>();
                _emissores[nome] = new List<Efeitos.Emissor>();
                var lote = new List<GameObject>();
                Montar(nome, _plano.grupos[nome], g.transform, false, lote);
                foreach (var v in _plano.vozes[nome]) v.prox = UnityEngine.Random.Range(2f, v.cadaMin);
                // o que fica parado vira poucas malhas (o lote só junta o que está ATIVO)
                g.SetActive(true);
                alto.SetActive(true);
                lev.SetActive(true);
                if (lote.Count > 0) StaticBatchingUtility.Combine(lote.ToArray(), g);
                g.SetActive(false);
            }
            // (as que trocam de malha já com as duas prontas: a do vento sai de uma cópia legível)
            foreach (var n in _trocam)
            {
                MalhaDa(n, n.malha);
                MalhaDa(n, n.malhaLeve);
            }
            NivelCenario(cenarioLeve);
            // as cópias das malhas na memória do processador não fazem mais falta
            // (o lote já foi feito; a placa de vídeo tem as dela)
            foreach (var m in _usadas) if (m != null) m.UploadMeshData(true);
            Debug.Log(string.Format("Cenário: 4 fases montadas em {0:0} ms", (Time.realtimeSinceStartup - t0) * 1000f));
        }

        static void Por(Transform t, No n)
        {
            n.Partes(out var p, out var r, out var e);
            t.localPosition = new Vector3(-p.x, p.y, p.z);
            t.localRotation = Quaternion.Euler(r.x * Mathf.Rad2Deg, -r.y * Mathf.Rad2Deg, -r.z * Mathf.Rad2Deg);
            t.localScale = new Vector3(e.x, e.y, e.z);
        }

        static Vector3 U(V3 v) => new Vector3(-v.x, v.y, v.z);

        void Montar(string tema, No no, Transform pai, bool paiMexe, List<GameObject> lote)
        {
            foreach (var c in no.filhos)
            {
                var go = new GameObject(c.nome);
                Transform t = go.transform;
                Transform onde = c.nivel == 1 ? _alto[tema].transform : (c.nivel == 2 ? _leve[tema].transform : pai);
                t.SetParent(onde, false);
                Por(t, c);
                c.vivo = t;
                bool mexe = paiMexe || c.mexe;
                if (c.mexe) _mexem[tema].Add(c);
                switch (c.material)
                {
                    case "atlas":
                    case "asas":
                    case "nuvem":
                        if (c.malha == null) break;
                        var m = MalhaDa(c, c.malha);
                        if (m == null) break;
                        var mat = c.material == "nuvem" ? MatNuvem() : (c.material == "asas" ? MatAsas(c.temaAtlas) : MaterialAtlas(c));
                        Desenho(go, m, mat);
                        if (c.troca) _trocam.Add(c);
                        else if (!mexe && c.material == "atlas" && !c.materialProprio) lote.Add(go);
                        break;
                    case "borboleta":
                        Desenho(go, MalhaBorboleta(c.variante), MatBorboleta());
                        break;
                    case "fio":
                        Desenho(go, MalhaFio((FioCenario)c.dados), MatFio());
                        break;
                    case "lampadas":
                        var lp = (LampadasCenario)c.dados;
                        var pts = new List<Vector3>();
                        foreach (var p in lp.pontos) pts.Add(U(p));
                        Desenho(go, Mundo.MalhaLampadas(pts, lp.fase0), Materiais.Lampadas);
                        break;
                    case "particulas":
                        var e = NovoEmissor((ParticulasCenario)c.dados, U(c.Global.o));
                        if (e != null) _emissores[tema].Add(e);
                        break;
                }
                Montar(tema, c, t, mexe, lote);
            }
        }

        static void Desenho(GameObject go, Mesh m, Material mat)
        {
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        /// <summary>O NÍVEL DO CENÁRIO: alto (as versões caprichadas) ou leve (um passo mais leve
        /// em cada peça). Trocar é só mostrar um nó e esconder o outro.</summary>
        public void NivelCenario(bool leve)
        {
            cenarioLeve = leve;
            foreach (var nome in ORDEM)
            {
                _alto[nome].SetActive(!leve);
                _leve[nome].SetActive(leve);
            }
            foreach (var n in _trocam)
            {
                var t = n.vivo as Transform;
                if (t == null) continue;
                var m = MalhaDa(n, leve ? n.malhaLeve : n.malha);
                if (m != null) t.GetComponent<MeshFilter>().sharedMesh = m;
            }
        }

        public void Mostrar(string nome)
        {
            atual = nome;
            foreach (var k in _grupos)
            {
                k.Value.SetActive(k.Key == nome);
                foreach (var e in _emissores[k.Key]) e.emitindo = k.Key == nome;
            }
        }

        // ------------------------------------------------------------ peças feitas aqui
        readonly Dictionary<int, Mesh> _borboletas = new Dictionary<int, Mesh>();

        Material MatBorboleta()
        {
            if (_matBorboleta == null) _matBorboleta = Materiais.Novo("Borboleta", "borboleta");
            return _matBorboleta;
        }

        Mesh MalhaBorboleta(int qual)
        {
            if (_borboletas.TryGetValue(qual, out var feita)) return feita;
            var par = PlanoCenario.CORES_BORBOLETA[qual % PlanoCenario.CORES_BORBOLETA.Length];
            var pts = new List<V3>();
            var cores = new List<Cor>();
            PlanoCenario.MalhaBorboleta(par[0], par[1], pts, cores);
            var V = new Vector3[pts.Count];
            var C = new Color32[pts.Count];
            var N = new Vector3[pts.Count];
            var T = new int[pts.Count];
            for (int i = 0; i < pts.Count; i++)
            {
                V[i] = U(pts[i]);
                C[i] = new Color(cores[i].r, cores[i].g, cores[i].b, 1f);
                N[i] = Vector3.up;
                T[i] = i;
            }
            var m = new Mesh { name = "borboleta" };
            m.vertices = V;
            m.normals = N;
            m.colors32 = C;
            m.triangles = T;
            m.RecalculateBounds();
            // (as asas sobem no shader: a caixa cresce para não sumir da tela)
            m.bounds = new Bounds(m.bounds.center, m.bounds.size + Vector3.one * 0.2f);
            _borboletas[qual] = m;
            return m;
        }

        static Mesh _fio;

        /// <summary>O fio do balão: um cilindro fino, branco.</summary>
        static Mesh MalhaFio(FioCenario f)
        {
            if (_fio != null) return _fio;
            var V = new List<Vector3>();
            var N = new List<Vector3>();
            var C = new List<Color32>();
            var T = new List<int>();
            for (int i = 0; i <= f.gomos; i++)
            {
                float a = 2f * Mathf.PI * i / f.gomos;
                var n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                V.Add(n * f.raio + Vector3.up * (f.altura * 0.5f));
                V.Add(n * f.raio - Vector3.up * (f.altura * 0.5f));
                N.Add(n);
                N.Add(n);
                C.Add(new Color32(255, 255, 255, 255));
                C.Add(new Color32(255, 255, 255, 255));
            }
            for (int i = 0; i < f.gomos; i++)
            {
                int a = 2 * i, b = a + 1, c = a + 2, d = a + 3;
                T.Add(a); T.Add(c); T.Add(b);
                T.Add(b); T.Add(c); T.Add(d);
            }
            _fio = new Mesh { name = "fio" };
            _fio.SetVertices(V);
            _fio.SetNormals(N);
            _fio.SetColors(C);
            _fio.SetTriangles(T, 0);
            _fio.RecalculateBounds();
            return _fio;
        }

        /// <summary>As partículas de um ponto do cenário (CPUParticles do Godot -> Efeitos).</summary>
        Efeitos.Emissor NovoEmissor(ParticulasCenario p, Vector3 pos)
        {
            if (Efeitos.I == null) return null;
            var c = Configuracao(p);
            if (p.umaVez)
            {
                p.vivo = c;
                return null;
            }
            var e = Efeitos.I.NovoEmissor(c, p.qtd / Mathf.Max(p.vida, 0.01f));
            e.pos = pos;
            p.vivo = e;
            return e;
        }

        static Efeitos.Config Configuracao(ParticulasCenario p)
        {
            Efeitos.Tipo tipo;
            switch (p.textura)
            {
                case "fumaca": tipo = p.somar ? Efeitos.Tipo.Chama : Efeitos.Tipo.Fumaca; break;
                case "bolha": tipo = Efeitos.Tipo.Bolha; break;
                default: tipo = p.umaVez ? Efeitos.Tipo.Faisca : Efeitos.Tipo.Cadente; break;
            }
            Vector2[] curva = null;
            if (p.curva != null)
            {
                curva = new Vector2[p.curva.Length / 2];
                for (int i = 0; i < curva.Length; i++) curva[i] = new Vector2(p.curva[2 * i], p.curva[2 * i + 1]);
            }
            Color[] rampa = null;
            if (p.rampa != null)
            {
                rampa = new Color[p.rampa.Length];
                for (int i = 0; i < rampa.Length; i++) rampa[i] = new Color(p.rampa[i].r, p.rampa[i].g, p.rampa[i].b, p.rampa[i].a);
            }
            return new Efeitos.Config
            {
                tipo = tipo, vida = p.vida, tam = p.tam, direcao = U(p.direcao), abertura = p.abertura,
                vMin = p.vMin, vMax = p.vMax, gravidade = U(p.gravidade), escalaMin = p.escMin, escalaMax = p.escMax,
                curva = curva, rampa = rampa, cor = new Color(p.cor.r, p.cor.g, p.cor.b, p.cor.a),
                raioEsfera = p.esfera, caixa = new Vector3(p.caixa.x, p.caixa.y, p.caixa.z),
            };
        }

        // ================================================================ animação
        void Update()
        {
            if (atual == "" || _plano == null) return;
            float dt = Time.deltaTime;
            _t = Mathf.Repeat(_t + dt, 3600f);
            SonsAmbiente(dt);
            foreach (var item in _plano.anim[atual]) Animar(item, dt);
            foreach (var n in _mexem[atual])
                if (n.vivo is Transform t) Por(t, n);
        }

        static Material MaterialDe(No n)
        {
            if (!(n.vivo is Transform t)) return null;
            var mr = t.GetComponent<MeshRenderer>();
            return mr != null ? mr.sharedMaterial : null;
        }

        float EscalaBase(AnimCenario item)
        {
            if (!item.temEscala)
            {
                item.escala = item.no.Scale.x;
                item.temEscala = true;
            }
            return item.escala;
        }

        static float Fposmod(float a, float b)
        {
            float r = a % b;
            return r < 0f ? r + b : r;
        }

        void Animar(AnimCenario item, float dt)
        {
            var no = item.no;
            switch (item.tipo)
            {
                case "girar":
                {
                    float vel = item.vel;
                    if (item.musica > 0f)
                    {
                        // com a musiquinha o carrossel gira um pouco mais depressa
                        item.musica -= dt;
                        vel *= 1f + 0.6f * Mathf.Clamp01(item.musica);
                    }
                    no.RotateY(vel * dt);
                    break;
                }
                case "nuvem":
                {
                    var p = no.Translation;
                    p.x += item.vel * dt;
                    if (p.x > 30f) p.x = -30f;
                    no.Translation = p;
                    break;
                }
                case "boiar":
                {
                    float f = item.fase, v = item.vel;
                    var amp = item.amp;
                    no.Translation = item.basePos + new V3(Mathf.Sin(_t * 0.5f * v + f) * amp.x, Mathf.Sin(_t * 0.9f * v + f * 2f) * amp.y, 0f);
                    if (item.temBalanco) no.RotZ(Mathf.Sin(_t * 0.7f * v + f) * item.balanco);
                    if (item.temGirar) no.RotY(no.Rotation.y + item.girar * dt);
                    if (item.temVira) no.RotY(item.olhar + Mathf.Sin(_t * 0.6f * v + f) * item.vira);
                    if (item.pirueta > 0f)
                    {
                        // o brilhinho: uma pirueta inteira
                        item.pirueta -= dt;
                        float u = 1f - Mathf.Clamp01(item.pirueta);
                        no.RotY(no.Rotation.y + Mathf.PI * 2f * (u * u * (3f - 2f * u)));
                    }
                    break;
                }
                case "girar_proprio":
                    no.RotateObjectLocal(item.eixo, item.vel * dt);
                    break;
                case "passeio":
                {
                    // uma volta deitada e achatada com um sobe e desce em "8"; o disco gira
                    // no próprio eixo e inclina para o lado da curva
                    float a = _t * item.vel;
                    var r = item.raio3;
                    no.Translation = item.centro + new V3(Mathf.Sin(a) * r.x, Mathf.Sin(2f * a) * r.y, Mathf.Cos(a) * r.z);
                    item.ang = Fposmod(item.ang + item.girar * dt, Mathf.PI * 2f);
                    float balanco = 0f;
                    if (item.balancar > 0f)
                    {
                        // "uuu-uuu": o disco balança de um lado para o outro
                        item.balancar -= dt;
                        balanco = Mathf.Sin(_t * 14f) * 0.18f * Mathf.Clamp01(item.balancar);
                    }
                    var tr = no.Transform;
                    tr.b = Base.Giro(new V3(0f, 0f, 1f), -Mathf.Cos(a) * 0.2f + balanco) * Base.Giro(V3.Cima, item.ang);
                    no.Transform = tr;
                    break;
                }
                case "bau":
                {
                    // abre com um pulinho, mostra o tesouro e fecha de uma vez
                    int dir = item.dir;
                    float k = item.k;
                    if (dir > 0)
                    {
                        if (k < 1f)
                        {
                            k = Mathf.Min(k + dt / PlanoCenario.BAU_ABRINDO, 1f);
                            if (k >= 1f && item.faiscas != null && item.faiscas.vivo is Efeitos.Config fc && Efeitos.I != null)
                                Efeitos.I.Explodir(fc, U(item.corpo.Transform.Xform(new V3(0f, 0.7f, 0f))), item.faiscas.qtd,
                                    item.faiscas.vida * (1f - item.faiscas.explosividade));
                        }
                        else
                        {
                            item.mostra -= dt;
                            if (item.mostra <= 0f) item.dir = -1;
                        }
                    }
                    else if (dir < 0)
                    {
                        k = Mathf.Max(k - dt / PlanoCenario.BAU_FECHANDO, 0f);
                        if (k <= 0f) item.dir = 0;
                    }
                    item.k = k;
                    float giro;
                    if (dir >= 0)
                    {
                        float u = k - 1f;
                        giro = k > 0f ? PlanoCenario.BAU_ABERTO * (1f + 2.70158f * u * u * u + 1.70158f * u * u) : 0f;
                    }
                    else giro = PlanoCenario.BAU_ABERTO * k * k;
                    no.Transform = item.corpo.Transform * new Pose(Base.Giro(V3.Direita, giro), PlanoCenario.BAU_DOBRADICA);
                    break;
                }
                case "roda":
                {
                    var roda = item.roda;
                    roda.RotZ(roda.Rotation.z + item.vel * dt);
                    var cab = item.cabines;
                    for (int i = 0; i < cab.Count; i++)
                    {
                        float a = roda.Rotation.z + Mathf.PI * 2f * i / cab.Count;
                        cab[i].Translation = new V3(Mathf.Cos(a), Mathf.Sin(a), 0f) * item.raio;
                        cab[i].RotZ(Mathf.Sin(_t * 1.3f + i) * 0.06f);
                    }
                    break;
                }
                case "orbita":
                {
                    float a = _t * item.vel + item.fase;
                    var r = item.raio2;
                    no.Translation = item.centro + new V3(Mathf.Cos(a) * r.x, Mathf.Sin(_t * 1.1f) * item.sobe, Mathf.Sin(a) * r.y);
                    no.RotY(no.Rotation.y + item.girar * dt);
                    no.RotZ(Mathf.Sin(_t * 0.9f) * 0.08f);
                    break;
                }
                case "alga":
                {
                    float f = item.fase;
                    var b = item.baseRot;
                    no.Rotation = b + new V3(Mathf.Sin(_t * 0.8f + f) * 0.07f, 0f, Mathf.Sin(_t * 1.1f + f * 1.7f) * 0.09f);
                    break;
                }
                case "peixe":
                {
                    float v = item.vel;
                    float a = _t * v - item.fase * Mathf.Sign(v);
                    var r = item.raio2;
                    no.Translation = item.centro + new V3(Mathf.Cos(a) * r.x, Mathf.Sin(_t * 1.7f + item.nado) * 0.12f, Mathf.Sin(a) * r.y);
                    // olha para onde está nadando (+Z do modelo) e rebola o rabinho
                    var d = new V3(-Mathf.Sin(a) * r.x, 0f, Mathf.Cos(a) * r.y) * Mathf.Sign(v);
                    no.RotY(Mathf.Atan2(d.x, d.z) + Mathf.Sin(_t * 9f + item.nado) * 0.12f);
                    break;
                }
                case "agua_viva":
                {
                    float f = item.fase, v = item.vel;
                    float pulso = Mathf.Sin(_t * 2.2f * v + f);
                    no.Translation = item.basePos + new V3(0f, Mathf.Sin(_t * 0.6f * v + f) * 0.5f, 0f);
                    no.Scale = new V3(1f + pulso * 0.06f, 1f - pulso * 0.08f, 1f + pulso * 0.06f) * EscalaBase(item);
                    break;
                }
                case "atravessar":
                {
                    var p = no.Translation;
                    p.x += item.vel * dt;
                    if (p.x > item.limite) p.x = -item.limite;
                    else if (p.x < -item.limite) p.x = item.limite;
                    p.y += Mathf.Sin(_t * 0.8f) * 0.003f;
                    no.Translation = p;
                    break;
                }
                case "respirar":
                {
                    float f = item.fase;
                    no.RotY(item.giro + Mathf.Sin(_t * 0.35f + f) * 0.12f);
                    no.RotZ(Mathf.Sin(_t * 0.9f + f) * 0.025f);
                    if (!item.temBaseY)
                    {
                        item.temBaseY = true;
                        item.baseY = no.Translation.y;
                        string malha = cenarioLeve && no.troca ? no.malhaLeve : no.malha;
                        item.cauda = malha != null ? -new Modelos().CaixaDe(malha).pos.z : 0f;
                    }
                    if (item.rugir > 0f)
                    {
                        // RAWR: empina, estica o pescoço e volta — apoiado no RABO
                        item.rugir -= dt;
                        float u = 1f - Mathf.Clamp01(item.rugir / 1.5f);
                        float k = Mathf.Sin(Mathf.PI * u);
                        var r = no.Rotation;
                        r.x = -0.12f * k;
                        no.Rotation = r;
                        no.Scale = new V3(1f, 1f + 0.05f * k, 1f) * EscalaBase(item);
                        var p = no.Translation;
                        p.y = item.baseY + item.cauda * Mathf.Sin(0.12f * k) * EscalaBase(item);
                        no.Translation = p;
                    }
                    else if (no.Rotation.x != 0f)
                    {
                        var r = no.Rotation;
                        r.x = 0f;
                        no.Rotation = r;
                        no.Scale = V3.Um * EscalaBase(item);
                        var p = no.Translation;
                        p.y = item.baseY;
                        no.Translation = p;
                    }
                    break;
                }
                case "vulcao":
                {
                    // o ronco: treme um pouquinho e a fumaça sai mais depressa
                    var fum = item.fumaca != null ? item.fumaca.vivo as Efeitos.Emissor : null;
                    if (item.tremer > 0f)
                    {
                        item.tremer -= dt;
                        float k = Mathf.Clamp01(item.tremer);
                        no.Translation = item.basePos + new V3(Mathf.Sin(_t * 41f), 0f, Mathf.Cos(_t * 37f)) * (0.05f * k);
                        if (fum != null) fum.ritmo = 1f + 1.6f * k;
                    }
                    else if (fum != null && fum.ritmo != 1f)
                    {
                        no.Translation = item.basePos;
                        fum.ritmo = 1f;
                    }
                    break;
                }
                case "voo":
                {
                    float v = item.vel;
                    float a = _t * v + item.fase;
                    float r = item.raio;
                    // as asas batem (o shader) e o corpo sobe quando elas descem; de vez
                    // em quando ele plana (as asas abertas, quase paradas)
                    float batida = Fposmod(_t * item.asas + item.fase, Mathf.PI * 2f);
                    float planar = Mathf.Clamp01(Mathf.Sin(_t * 0.37f + item.fase) * 3f - 1.5f);
                    if (item.mat == null) item.mat = MaterialDe(no);
                    if (item.mat is Material ma)
                    {
                        ma.SetFloat(ID_ASA_TEMPO, batida);
                        ma.SetFloat(ID_ASA_AMPLITUDE, 0.55f * (1f - 0.75f * planar));
                    }
                    float sobe = -Mathf.Cos(batida) * 0.06f * (1f - planar);
                    no.Translation = item.centro + new V3(Mathf.Cos(a) * r, Mathf.Sin(_t * 1.4f + item.fase) * 0.4f + sobe, Mathf.Sin(a) * r);
                    var d = new V3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * Mathf.Sign(v);
                    no.Rotation = new V3(0f, Mathf.Atan2(d.x, d.z), -Mathf.Sign(v) * 0.35f + Mathf.Sin(_t * 3f) * 0.05f);
                    break;
                }
                case "pular":
                {
                    // de tempos em tempos um pulinho: estica subindo e amassa ao cair
                    float e = EscalaBase(item);
                    float ciclo = Fposmod(_t + item.fase, item.cada);
                    if (ciclo < 0.5f)
                    {
                        float k = Mathf.Sin(Mathf.PI * ciclo / 0.5f);
                        no.Translation = item.basePos + new V3(0f, k * item.altura, 0f);
                        no.Scale = new V3(1f - 0.05f * k, 1f + 0.08f * k, 1f - 0.05f * k) * e;
                    }
                    else if (ciclo < 0.85f)
                    {
                        float u = (ciclo - 0.5f) / 0.35f;
                        float k = Mathf.Sin(Mathf.PI * u) * (1f - u);
                        no.Translation = item.basePos;
                        no.Scale = new V3(1f + 0.12f * k, 1f - 0.16f * k, 1f + 0.12f * k) * e;
                    }
                    else if (ciclo < 0.85f + dt * 2f) no.Scale = V3.Um * e;
                    break;
                }
                case "chocar":
                {
                    // o ovo treme de vez em quando (balança e para)
                    float ciclo = Fposmod(_t + item.fase, item.cada);
                    if (ciclo < 1.1f)
                    {
                        float u = ciclo / 1.1f;
                        float k = (1f - u) * Mathf.Sin(u * Mathf.PI * 2f * 3.5f);
                        no.Rotation = new V3(k * 0.05f, item.giro, k * 0.12f);
                    }
                    else if (ciclo < 1.1f + dt * 2f) no.Rotation = new V3(0f, item.giro, 0f);
                    break;
                }
                case "levitar":
                    no.Translation = item.basePos + new V3(0f, 0.08f + Mathf.Sin(_t * item.vel + item.fase) * 0.07f, 0f);
                    no.RotY(no.Rotation.y + 0.25f * item.vel * dt);
                    break;
                case "brilhar":
                {
                    // o cristal fica parado no chão; só a luz dele pulsa devagar
                    if (item.mat == null) item.mat = MaterialDe(no);
                    float k = 0.5f + 0.5f * Mathf.Sin(_t * 1.6f + item.fase * 2.3f);
                    Acender(item.mat as Material, 0.8f + 1.8f * k * k);
                    break;
                }
                case "olhar":
                    no.RotY(item.giro + Mathf.Sin(_t * item.vel + item.fase) * 0.4f);
                    break;
                case "pulsar":
                {
                    float k = Mathf.Sin(_t * 1.6f + item.fase);
                    no.Scale = new V3(1f + 0.03f * k, 1f - 0.05f * k, 1f + 0.03f * k) * EscalaBase(item);
                    break;
                }
                case "navio":
                    no.Rotation = item.baseRot + new V3(Mathf.Sin(_t * 0.37f) * 0.015f, 0f, Mathf.Sin(_t * 0.5f) * 0.025f);
                    break;
                case "borboleta":
                {
                    // um voo sem pressa, em laços, sempre olhando para onde vai
                    float f = item.fase;
                    var r = item.raio3;
                    float t = _t * item.vel + f;
                    no.Translation = item.centro + new V3(Mathf.Sin(t) * r.x,
                        Mathf.Sin(t * 2.3f + f) * r.y * 0.5f + Mathf.Sin(_t * 4.1f + f) * 0.03f, Mathf.Sin(t * 0.7f + f * 0.5f) * r.z);
                    float dx = Mathf.Cos(t) * r.x;
                    float dz = 0.7f * Mathf.Cos(t * 0.7f + f * 0.5f) * r.z;
                    no.Rotation = new V3(0f, Mathf.Atan2(dx, dz), 0f);
                    break;
                }
            }
        }

        // ================================================================ sons de ambiente
        /// <summary>O ponto está na tela (na frente da câmera, dentro do quadro com folga)?</summary>
        static bool NaTela(No no)
        {
            var cam = Camera.main;
            if (cam == null || !(no.vivo is Transform t) || !t.gameObject.activeInHierarchy) return false;
            var v = cam.WorldToViewportPoint(t.position);
            return v.z > 0f && v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
        }

        /// <summary>O MAESTRO DOS SONS DE AMBIENTE: na vez (folga desde o último), toca a voz mais
        /// atrasada entre as que estão na tela, com a ação dela.</summary>
        void SonsAmbiente(float dt)
        {
            _relogio += dt;
            if (!somAmbiente || !detalhes || _relogio < _proxAmb) return;
            VozCenario escolhida = null;
            foreach (var v in _plano.vozes[atual])
                if (_relogio >= v.prox && (escolhida == null || v.prox < escolhida.prox) && NaTela(v.no))
                    escolhida = v;
            if (escolhida == null) return;
            var e = escolhida;
            if (e.no.vivo is Transform t)
                Sons.TocarEm(t.position, e.som, AMB_VOLUME * e.vol, e.tom * UnityEngine.Random.Range(0.96f, 1.04f));
            switch (e.acao)
            {
                case "rugir": e.item.rugir = 1.5f; break;
                case "tremer": e.item.tremer = 2.0f; break;
                case "musica": e.item.musica = 5.0f; break;
                case "balancar": e.item.balancar = 2.2f; break;
                case "piruetar": e.item.pirueta = 1.0f; break;
                case "abrir":
                    if (e.item.dir == 0)
                    {
                        e.item.dir = 1;
                        e.item.mostra = PlanoCenario.BAU_MOSTRA;
                    }
                    break;
                case "baleia":
                    if (_mundo != null) _mundo.BaleiaCantar(3.5f);
                    break;
            }
            e.prox = _relogio + UnityEngine.Random.Range(e.cadaMin, e.cadaMax);
            _proxAmb = _relogio + UnityEngine.Random.Range(AMB_FOLGA_MIN, AMB_FOLGA_MAX);
        }
    }
}
