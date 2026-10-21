using System;
using System.Collections.Generic;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// EFEITOS 3D: explosão de estrelas, poeira do impacto, "puf", chuva de
    /// confete, fogos (fim de jogo) e os emissores contínuos (brilhos da bola,
    /// chamas da bola de fogo, festa dos bichinhos, bolhas do mar, fumaça do
    /// vulcão, estrelas cadentes).
    ///
    /// Leve para a Mali-450: TODAS as partículas de um material viram UMA
    /// malha (um desenho), refeita a cada quadro na CPU (quadradinhos virados
    /// para a câmera). Nada de criar objeto por efeito.
    /// </summary>
    public class Efeitos : MonoBehaviour
    {
        public static Efeitos I { get; private set; }

        public static readonly Color[] ARCO_IRIS =
        {
            Materiais.Hex("#FF4B5C"), Materiais.Hex("#FF9F2E"), Materiais.Hex("#FFE14D"), Materiais.Hex("#5ED16A"),
            Materiais.Hex("#4FC3F7"), Materiais.Hex("#2E7BFF"), Materiais.Hex("#FFFFFF"),
        };

        public enum Tipo { Estrela, Faisca, Fumaca, Confete, Bolha, Chama, Cadente }

        /// <summary>Como as partículas de um efeito nascem e andam.</summary>
        public class Config
        {
            public Tipo tipo = Tipo.Estrela;
            public float vida = 1f;
            public float tam = 0.1f;
            public Vector3 direcao = Vector3.up;
            public float abertura = 180f;          // graus em volta da direção
            public float vMin = 1f, vMax = 2f;
            public Vector3 gravidade = Vector3.zero;
            public float amorteceMin, amorteceMax;
            public float giroMin, giroMax;          // graus/s
            public float escalaMin = 1f, escalaMax = 1f;
            /// <summary>Tamanho ao longo da vida: pares (t, escala).</summary>
            public Vector2[] curva;
            /// <summary>Cor inicial sorteada entre estas (null = `cor`).</summary>
            public Color[] cores;
            public Color cor = Color.white;
            /// <summary>Cor ao longo da vida (multiplica): 2 ou 3 cores (início, meio, fim).</summary>
            public Color[] rampa;
            // onde nascem
            public float raioEsfera;                // esfera
            public Vector3 caixa;                    // meia-caixa
            public float anelRaio, anelDentro, anelAltura;  // anel deitado (eixo Y)
        }

        class Particula
        {
            public Vector3 pos, vel;
            public float vida, idade, tam, angulo, giro, amortece;
            public Color cor;
            public Config cfg;
        }

        class Lote
        {
            public Material mat;
            public Mesh malha;
            public readonly List<Particula> lista = new List<Particula>();
            public Vector3[] v = new Vector3[0];
            public Color32[] c = new Color32[0];
            public Vector2[] uv = new Vector2[0];
            public int[] tri = new int[0];
            public int capacidade;
            public MeshRenderer mr;
        }

        /// <summary>Emissor contínuo (bola, bichinhos, cenários): ligue/desligue e ele segue `pos`.</summary>
        public class Emissor
        {
            public Config cfg;
            public float porSegundo;
            public bool emitindo;
            public Vector3 pos;
            public float ritmo = 1f;       // velocidade do tempo (replay em câmera lenta)
            internal float acumulado;
            public bool ativo = true;      // false = some de vez
        }

        readonly Dictionary<Tipo, Lote> _lotes = new Dictionary<Tipo, Lote>();
        readonly List<Emissor> _emissores = new List<Emissor>();
        readonly List<Particula> _livres = new List<Particula>();
        readonly List<(float quando, Action acao)> _agenda = new List<(float, Action)>();
        float _t;
        Transform _cam;

        public static Efeitos Criar(Transform pai)
        {
            var go = new GameObject("Efeitos");
            go.transform.SetParent(pai, false);
            I = go.AddComponent<Efeitos>();
            return I;
        }

        void Awake()
        {
            Novo(Tipo.Estrela, Materiais.Particula("estrela"));
            Novo(Tipo.Faisca, Materiais.Particula("faisca", true));
            Novo(Tipo.Fumaca, Materiais.Particula("fumaca"));
            Novo(Tipo.Confete, Materiais.Particula("confete"));
            Novo(Tipo.Bolha, Materiais.Particula("bolha"));
            Novo(Tipo.Chama, Materiais.Particula("fumaca", true));
            Novo(Tipo.Cadente, Materiais.Particula("faisca", true));
        }

        void Novo(Tipo t, Material m)
        {
            var go = new GameObject("particulas_" + t);
            go.transform.SetParent(transform, false);
            var malha = new Mesh { name = "particulas_" + t };
            malha.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.enabled = false;
            _lotes[t] = new Lote { mat = m, malha = malha, mr = mr };
        }

        public static Color DoArcoIris(float u)
        {
            u = Mathf.Repeat(u, 1f) * (ARCO_IRIS.Length - 1);
            int i = Mathf.Min((int)u, ARCO_IRIS.Length - 2);
            return Color.Lerp(ARCO_IRIS[i], ARCO_IRIS[i + 1], u - i);
        }

        // ------------------------------------------------------------ nascer
        void Nascer(Config c, Vector3 origem)
        {
            Particula p;
            if (_livres.Count > 0)
            {
                p = _livres[_livres.Count - 1];
                _livres.RemoveAt(_livres.Count - 1);
            }
            else p = new Particula();
            p.cfg = c;
            Vector3 o = origem;
            if (c.raioEsfera > 0f) o += UnityEngine.Random.insideUnitSphere * c.raioEsfera;
            if (c.caixa != Vector3.zero)
                o += new Vector3(UnityEngine.Random.Range(-c.caixa.x, c.caixa.x), UnityEngine.Random.Range(-c.caixa.y, c.caixa.y),
                    UnityEngine.Random.Range(-c.caixa.z, c.caixa.z));
            if (c.anelRaio > 0f)
            {
                float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float r = Mathf.Sqrt(UnityEngine.Random.Range(c.anelDentro * c.anelDentro, c.anelRaio * c.anelRaio));
                o += new Vector3(Mathf.Cos(a) * r, UnityEngine.Random.Range(-c.anelAltura, c.anelAltura) * 0.5f, Mathf.Sin(a) * r);
            }
            p.pos = o;
            // direção dentro do cone
            Vector3 d = c.direcao.sqrMagnitude > 0f ? c.direcao.normalized : Vector3.up;
            if (c.abertura > 0f)
            {
                float ang = Mathf.Deg2Rad * Mathf.Min(c.abertura, 180f);
                float cosMax = Mathf.Cos(ang);
                float z = UnityEngine.Random.Range(cosMax, 1f);
                float phi = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float s = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                var local = new Vector3(Mathf.Cos(phi) * s, Mathf.Sin(phi) * s, z);
                d = Quaternion.FromToRotation(Vector3.forward, d) * local;
            }
            p.vel = d * UnityEngine.Random.Range(c.vMin, c.vMax);
            p.vida = c.vida * UnityEngine.Random.Range(0.85f, 1f);
            p.idade = 0f;
            p.tam = c.tam * UnityEngine.Random.Range(c.escalaMin, c.escalaMax);
            p.angulo = UnityEngine.Random.Range(0f, 360f);
            p.giro = UnityEngine.Random.Range(c.giroMin, c.giroMax);
            p.amortece = UnityEngine.Random.Range(c.amorteceMin, c.amorteceMax);
            p.cor = c.cores != null && c.cores.Length > 0 ? c.cores[UnityEngine.Random.Range(0, c.cores.Length)] : c.cor;
            _lotes[c.tipo].lista.Add(p);
        }

        public void Explodir(Config c, Vector3 origem, int qtd, float espalhar = 0f)
        {
            for (int i = 0; i < qtd; i++)
            {
                if (espalhar > 0f)
                {
                    float atraso = UnityEngine.Random.Range(0f, espalhar);
                    var cc = c;
                    var oo = origem;
                    _agenda.Add((_t + atraso, () => Nascer(cc, oo)));
                }
                else Nascer(c, origem);
            }
        }

        public Emissor NovoEmissor(Config c, float porSegundo)
        {
            var e = new Emissor { cfg = c, porSegundo = porSegundo };
            _emissores.Add(e);
            return e;
        }

        public void Limpar()
        {
            foreach (var l in _lotes.Values)
            {
                _livres.AddRange(l.lista);
                l.lista.Clear();
            }
            _agenda.Clear();
            foreach (var e in _emissores) e.emitindo = false;
        }

        // ------------------------------------------------------------ efeitos prontos
        static Config _estrelas, _faiscas, _poeira, _puf, _confete, _fogo;

        static Vector2[] Curva(params float[] v)
        {
            var r = new Vector2[v.Length / 2];
            for (int i = 0; i < r.Length; i++) r[i] = new Vector2(v[2 * i], v[2 * i + 1]);
            return r;
        }

        /// <summary>Explosão de estrelas coloridas (strike, spare).</summary>
        public void Estrelas(Vector3 pos, int qtd = 30, float forca = 1f)
        {
            if (_estrelas == null)
                _estrelas = new Config
                {
                    tipo = Tipo.Estrela, vida = 1.3f, tam = 0.16f, abertura = 80f, gravidade = new Vector3(0, -6f, 0),
                    giroMin = -400f, giroMax = 400f, escalaMin = 0.6f, escalaMax = 1.3f, cores = ARCO_IRIS, curva = Curva(0, 1, 0.75f, 1, 1, 0),
                };
            if (_faiscas == null)
                _faiscas = new Config
                {
                    tipo = Tipo.Faisca, vida = 0.6f, tam = 0.22f, abertura = 180f, vMin = 1.5f, vMax = 3.5f, amorteceMin = 3f, amorteceMax = 5f,
                    cor = new Color(1f, 0.95f, 0.7f), curva = Curva(0, 1, 1, 0),
                };
            _estrelas.vMin = 2.5f * forca;
            _estrelas.vMax = 5f * forca;
            Explodir(_estrelas, pos, qtd);
            Explodir(_faiscas, pos, 20);
        }

        /// <summary>Poeirinha no chão (impacto da bola nos pinos, pino caindo).</summary>
        public void Poeira(Vector3 pos, float forca = 1f)
        {
            if (_poeira == null)
                _poeira = new Config
                {
                    tipo = Tipo.Fumaca, vida = 0.8f, tam = 0.35f, abertura = 85f, gravidade = new Vector3(0, 0.3f, 0),
                    amorteceMin = 1.5f, amorteceMax = 2.5f, escalaMin = 0.6f, escalaMax = 1.2f, curva = Curva(0, 0.4f, 0.3f, 1, 1, 1.3f),
                    cor = new Color(1, 1, 1, 0.85f), rampa = new[] { Color.white, new Color(1, 0.95f, 1, 0f) },
                };
            _poeira.vMin = 0.4f;
            _poeira.vMax = Mathf.Max(0.5f, 1.4f * forca);
            Explodir(_poeira, pos, (int)(10 * forca) + 4);
        }

        /// <summary>"Puf" de pino sumindo.</summary>
        public void Puf(Vector3 pos)
        {
            Poeira(pos, 0.8f);
            if (_puf == null)
                _puf = new Config
                {
                    tipo = Tipo.Estrela, vida = 0.7f, tam = 0.1f, abertura = 180f, vMin = 1f, vMax = 2f, gravidade = new Vector3(0, -2f, 0),
                    cores = ARCO_IRIS, curva = Curva(0, 1, 1, 0),
                };
            Explodir(_puf, pos + new Vector3(0, 0.2f, 0), 8);
        }

        /// <summary>Chuva de confete na frente da câmera (posição e largura da área).</summary>
        public void Confete(Vector3 pos, float largura = 3f, int qtd = 120)
        {
            if (_confete == null)
                _confete = new Config
                {
                    tipo = Tipo.Confete, vida = 3.2f, tam = 0.07f, abertura = 60f, vMin = 1.5f, vMax = 4f, gravidade = new Vector3(0, -2.2f, 0),
                    amorteceMin = 0.8f, amorteceMax = 1.6f, giroMin = -720f, giroMax = 720f, escalaMin = 0.8f, escalaMax = 1.4f, cores = ARCO_IRIS,
                };
            var c = new Config();
            CopiarConfig(_confete, c);
            c.caixa = new Vector3(largura, 0.2f, largura * 0.5f);
            Explodir(c, pos, qtd, 3.2f * 0.7f);
        }

        /// <summary>Fogos de artifício (vários estouros).</summary>
        public void Fogos(Vector3 centro, int quantos = 5)
        {
            for (int i = 0; i < quantos; i++)
            {
                var c = centro;
                _agenda.Add((_t + 0.35f * i, () => Estouro(c)));
            }
        }

        void Estouro(Vector3 centro)
        {
            var pos = centro + new Vector3(UnityEngine.Random.Range(-2.5f, 2.5f), UnityEngine.Random.Range(-0.5f, 1.5f), UnityEngine.Random.Range(-1f, 1f));
            if (_fogo == null)
                _fogo = new Config
                {
                    tipo = Tipo.Faisca, vida = 1.4f, tam = 0.18f, abertura = 180f, vMin = 3f, vMax = 3.6f, gravidade = new Vector3(0, -2f, 0),
                    amorteceMin = 1.2f, amorteceMax = 1.6f, curva = Curva(0, 1, 1, 0),
                };
            var c = new Config();
            CopiarConfig(_fogo, c);
            c.cor = ARCO_IRIS[UnityEngine.Random.Range(0, ARCO_IRIS.Length)];
            Explodir(c, pos, 46);
            Sons.Tocar("pop", 0.35f, UnityEngine.Random.Range(0.8f, 1.2f));
        }

        public static void CopiarConfig(Config de, Config para)
        {
            para.tipo = de.tipo; para.vida = de.vida; para.tam = de.tam; para.direcao = de.direcao; para.abertura = de.abertura;
            para.vMin = de.vMin; para.vMax = de.vMax; para.gravidade = de.gravidade; para.amorteceMin = de.amorteceMin;
            para.amorteceMax = de.amorteceMax; para.giroMin = de.giroMin; para.giroMax = de.giroMax; para.escalaMin = de.escalaMin;
            para.escalaMax = de.escalaMax; para.curva = de.curva; para.cores = de.cores; para.cor = de.cor; para.rampa = de.rampa;
            para.raioEsfera = de.raioEsfera; para.caixa = de.caixa; para.anelRaio = de.anelRaio; para.anelDentro = de.anelDentro;
            para.anelAltura = de.anelAltura;
        }

        // ------------------------------------------------------------ a cada quadro
        static float Escala(Vector2[] curva, float u)
        {
            if (curva == null || curva.Length == 0) return 1f;
            if (u <= curva[0].x) return curva[0].y;
            for (int i = 1; i < curva.Length; i++)
                if (u <= curva[i].x)
                    return Mathf.Lerp(curva[i - 1].y, curva[i].y, (u - curva[i - 1].x) / Mathf.Max(curva[i].x - curva[i - 1].x, 1e-4f));
            return curva[curva.Length - 1].y;
        }

        static Color Rampa(Color[] r, float u)
        {
            if (r == null || r.Length == 0) return Color.white;
            if (r.Length == 1) return r[0];
            float x = Mathf.Clamp01(u) * (r.Length - 1);
            int i = Mathf.Min((int)x, r.Length - 2);
            return Color.Lerp(r[i], r[i + 1], x - i);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            _t += dt;
            for (int i = _agenda.Count - 1; i >= 0; i--)
            {
                if (_agenda[i].quando <= _t)
                {
                    var a = _agenda[i].acao;
                    _agenda.RemoveAt(i);
                    a();
                }
            }
            for (int i = _emissores.Count - 1; i >= 0; i--)
            {
                var e = _emissores[i];
                if (!e.ativo) { _emissores.RemoveAt(i); continue; }
                if (!e.emitindo) { e.acumulado = 0f; continue; }
                e.acumulado += dt * e.porSegundo * e.ritmo;
                int n = (int)e.acumulado;
                e.acumulado -= n;
                for (int k = 0; k < n && k < 30; k++) Nascer(e.cfg, e.pos);
            }
            if (_cam == null && Camera.main != null) _cam = Camera.main.transform;
            Vector3 dir = _cam != null ? _cam.right : Vector3.right;
            Vector3 cima = _cam != null ? _cam.up : Vector3.up;
            foreach (var l in _lotes.Values) Desenhar(l, dt, dir, cima);
        }

        void Desenhar(Lote l, float dt, Vector3 dir, Vector3 cima)
        {
            var lista = l.lista;
            for (int i = lista.Count - 1; i >= 0; i--)
            {
                var p = lista[i];
                float ritmo = 1f;
                p.idade += dt * ritmo;
                if (p.idade >= p.vida)
                {
                    lista[i] = lista[lista.Count - 1];
                    lista.RemoveAt(lista.Count - 1);
                    _livres.Add(p);
                    continue;
                }
                p.vel += p.cfg.gravidade * dt;
                if (p.amortece > 0f)
                {
                    float vm = p.vel.magnitude;
                    if (vm > 0f) p.vel *= Mathf.Max(0f, vm - p.amortece * dt) / vm;
                }
                p.pos += p.vel * dt;
                p.angulo += p.giro * dt;
            }
            int n = lista.Count;
            if (n == 0)
            {
                if (l.mr.enabled) l.mr.enabled = false;
                return;
            }
            if (n > l.capacidade)
            {
                l.capacidade = Mathf.NextPowerOfTwo(n);
                l.v = new Vector3[l.capacidade * 4];
                l.c = new Color32[l.capacidade * 4];
                l.uv = new Vector2[l.capacidade * 4];
                l.tri = new int[l.capacidade * 6];
                for (int i = 0; i < l.capacidade; i++)
                {
                    l.uv[i * 4] = new Vector2(0, 0);
                    l.uv[i * 4 + 1] = new Vector2(1, 0);
                    l.uv[i * 4 + 2] = new Vector2(1, 1);
                    l.uv[i * 4 + 3] = new Vector2(0, 1);
                }
                l.malha.Clear();
                l.malha.vertices = l.v;
                l.malha.uv = l.uv;
                l.malha.colors32 = l.c;
            }
            for (int i = 0; i < n; i++)
            {
                var p = lista[i];
                float u = p.idade / p.vida;
                float s = p.tam * Escala(p.cfg.curva, u) * 0.5f;
                Color cor = p.cor;
                if (p.cfg.rampa != null) cor *= Rampa(p.cfg.rampa, u);
                float a = p.angulo * Mathf.Deg2Rad;
                float ca = Mathf.Cos(a) * s, sa = Mathf.Sin(a) * s;
                Vector3 ex = dir * ca + cima * sa;
                Vector3 ey = -dir * sa + cima * ca;
                int k = i * 4;
                l.v[k] = p.pos - ex - ey;
                l.v[k + 1] = p.pos + ex - ey;
                l.v[k + 2] = p.pos + ex + ey;
                l.v[k + 3] = p.pos - ex + ey;
                Color32 c32 = cor;
                l.c[k] = l.c[k + 1] = l.c[k + 2] = l.c[k + 3] = c32;
                int t = i * 6;
                l.tri[t] = k; l.tri[t + 1] = k + 2; l.tri[t + 2] = k + 1;
                l.tri[t + 3] = k; l.tri[t + 4] = k + 3; l.tri[t + 5] = k + 2;
            }
            // o resto da capacidade: triângulos vazios
            for (int i = n * 6; i < l.tri.Length; i++) l.tri[i] = 0;
            l.malha.vertices = l.v;
            l.malha.colors32 = l.c;
            l.malha.triangles = l.tri;
            l.malha.bounds = new Bounds(Vector3.zero, Vector3.one * 2000f);
            if (!l.mr.enabled) l.mr.enabled = true;
        }
    }
}
