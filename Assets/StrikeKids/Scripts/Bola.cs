using System;
using System.Collections.Generic;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// A BOLA: corpo físico, textura do personagem (patinhas do Bob, estrelas
    /// da Mia), rastro de arco-íris no chão e brilhinhos. Com 2 ou mais
    /// strikes seguidos ela vira BOLA DE FOGO (rastro e chamas).
    /// </summary>
    public class Bola : MonoBehaviour
    {
        public const float RAIO = 0.14f;
        public const float MASSA = 6.5f;
        static readonly Color[] FOGO =
        {
            Materiais.Hex("#FFF3B0"), Materiais.Hex("#FFD43B"), Materiais.Hex("#FF9F2E"), Materiais.Hex("#FF5A1E"), Materiais.Hex("#E0301E"),
        };
        static readonly Color[] ARCO =
        {
            Materiais.Hex("#FF4B5C"), Materiais.Hex("#FF9F2E"), Materiais.Hex("#FFE14D"), Materiais.Hex("#5ED16A"),
            Materiais.Hex("#4FC3F7"), Materiais.Hex("#2E7BFF"),
        };
        const int PONTOS = 28;

        /// <summary>bateu em alguma coisa (colisor, velocidade)</summary>
        public event Action<Collider, float> Bateu;
        /// <summary>entrou na caçapa das bolas (no fundo do fosso)</summary>
        public event Action Recolhida;

        public Rigidbody rb;
        /// <summary>força lateral (efeito "gancho")</summary>
        public float curva;
        public bool rolando;
        /// <summary>no replay a bola só é "posta" nos lugares gravados (sem física)</summary>
        public bool emReplay;

        MeshRenderer _mr;
        Material _mat;
        Transform _sombra;
        MeshRenderer _sombraMr;
        Mesh _rastro;
        MeshRenderer _rastroMr;
        readonly List<Vector3> _pontos = new List<Vector3>();
        readonly Vector3[] _rv = new Vector3[PONTOS * 2];
        readonly Color32[] _rc = new Color32[PONTOS * 2];
        int[] _rt;
        Efeitos.Emissor _brilhos, _chamas;
        Efeitos.Config _cfgBrilhos;
        bool _fogo;
        static readonly int ID_ACESO = Shader.PropertyToID("_aceso");
        static readonly int ID_TEXTURA = Shader.PropertyToID("_textura");
        static readonly Dictionary<string, Material> _materiais = new Dictionary<string, Material>();
        static Mesh _malha;

        public static Mesh Malha
        {
            get
            {
                if (_malha == null)
                {
                    // a esfera da edição Godot (32 x 16 gomos, a textura dá a volta nela)
                    var pc = Malhas.Ler("bola_esfera");
                    if (pc.partes.Count > 0) _malha = pc.partes[0].malha;
                }
                return _malha;
            }
        }

        /// <summary>O material da bola de um personagem (a da mão e a da pista).</summary>
        public static Material MaterialDe(string nome)
        {
            if (_materiais.TryGetValue(nome, out var m) && m != null) return m;
            // a bola da edição Godot (G_bola): a textura do bichinho, a luz de palco em
            // faixas, o brilho de verniz e a borda iluminada
            m = Materiais.Novo("G/bola", "bola_" + nome);
            var t = Palco.Textura("bola_" + nome);
            t.wrapMode = TextureWrapMode.Repeat;
            m.SetTexture(ID_TEXTURA, t);
            _materiais[nome] = m;
            return m;
        }

        public static Bola Criar(Transform pai)
        {
            var go = new GameObject("Bola");
            go.layer = Medidas.CAMADA_BOLA;
            go.transform.SetParent(pai, false);
            var b = go.AddComponent<Bola>();
            b.Montar();
            return b;
        }

        void Montar()
        {
            rb = gameObject.AddComponent<Rigidbody>();
            var sc = gameObject.AddComponent<SphereCollider>();
            sc.radius = RAIO;
            sc.sharedMaterial = Medidas.MaterialBola();
            rb.mass = MASSA;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.sleepThreshold = 0f;
            rb.maxAngularVelocity = 100f;
            Parar();
            var m = Malhas.Objeto("malha", Malha, MaterialDe("bob"), transform);
            _mr = m.GetComponent<MeshRenderer>();
            // sombra: adesivo deitado, solto no mundo
            var s = Malhas.Objeto("sombra_bola", Malhas.QuadradoDeitado(0.42f, 0.42f), Materiais.Sombra(0.6f), transform.parent);
            _sombra = s.transform;
            _sombraMr = s.GetComponent<MeshRenderer>();
            // rastro de arco-íris (uma faixa refeita a cada quadro)
            _rastro = new Mesh { name = "rastro" };
            _rastro.MarkDynamic();
            _rt = new int[(PONTOS - 1) * 6];
            var mr = Materiais.Novo("ParticulaSoma", "rastro");
            mr.SetTexture(Materiais.ID_TEX, Texture2D.whiteTexture);
            var r = Malhas.Objeto("rastro", _rastro, mr, transform.parent);
            _rastroMr = r.GetComponent<MeshRenderer>();
            _rastroMr.enabled = false;
            // brilhinhos e chamas (os emissores do sistema de efeitos)
            _cfgBrilhos = new Efeitos.Config
            {
                tipo = Efeitos.Tipo.Faisca, vida = 0.7f, tam = 0.09f, direcao = Vector3.up, abertura = 70f,
                vMin = 0.3f, vMax = 0.9f, gravidade = new Vector3(0, -0.6f, 0), escalaMin = 0.5f, escalaMax = 1f,
                curva = new[] { new Vector2(0, 1), new Vector2(1, 0) }, cores = ARCO, raioEsfera = RAIO,
            };
            var cfgChamas = new Efeitos.Config
            {
                tipo = Efeitos.Tipo.Chama, vida = 0.45f, tam = 0.2f, direcao = Vector3.up, abertura = 25f,
                vMin = 0.6f, vMax = 1.4f, gravidade = new Vector3(0, 1.5f, 0), escalaMin = 0.7f, escalaMax = 1.3f,
                curva = new[] { new Vector2(0, 0.6f), new Vector2(0.3f, 1f), new Vector2(1, 0) }, raioEsfera = RAIO * 0.8f,
                rampa = new[] { new Color(1f, 0.95f, 0.6f, 1f), new Color(1f, 0.55f, 0.12f, 0.9f), new Color(0.9f, 0.15f, 0.05f, 0f) },
            };
            // (na calibração não há efeitos: emissores soltos, que ninguém desenha)
            _brilhos = Efeitos.I != null ? Efeitos.I.NovoEmissor(_cfgBrilhos, 57f) : new Efeitos.Emissor { cfg = _cfgBrilhos };
            _chamas = Efeitos.I != null ? Efeitos.I.NovoEmissor(cfgChamas, 100f) : new Efeitos.Emissor { cfg = cfgChamas };
            Guardar();
        }

        public void Vestir(string nome)
        {
            _mat = MaterialDe(nome);
            _mr.sharedMaterial = _mat;
        }

        public void Lancar(Vector3 origem, Vector3 velocidade, float efeito = 0f)
        {
            Mostrar(true);
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.position = origem;
            rb.rotation = Quaternion.identity;
            transform.SetPositionAndRotation(origem, Quaternion.identity);
            rb.velocity = velocidade;
            rb.angularVelocity = Vector3.Cross(Vector3.up, velocidade) / RAIO;
            _interpEm = 2;
            curva = efeito;
            rolando = true;
            emReplay = false;
            _pontos.Clear();
            Ritmo(1f);
            _brilhos.emitindo = true;
            _chamas.emitindo = _fogo;
        }

        int _interpEm;
        bool _triProntos;

        public void Guardar()
        {
            rolando = false;
            emReplay = false;
            Parar();
            rb.interpolation = RigidbodyInterpolation.None;
            var longe = new Vector3(0, -5f, 20f);
            rb.position = longe;
            transform.position = longe;
            Mostrar(false);
            _pontos.Clear();
            _rastroMr.enabled = false;
            _brilhos.emitindo = false;
            _chamas.emitindo = false;
        }

        void Mostrar(bool sim)
        {
            _mr.enabled = sim;
            _sombraMr.enabled = sim;
        }

        /// <summary>Sem física (guardada, no replay): parada e sem CCD (a PhysX não aceita CCD em
        /// corpo cinemático).</summary>
        void Parar()
        {
            if (!rb.isKinematic)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.isKinematic = true;
        }

        public bool Visivel => _mr.enabled;
        /// <summary>O desenho da bola (para o reflexo na pista).</summary>
        public MeshRenderer Corpo => _mr;

        /// <summary>Mostra/esconde a bola (o replay põe como estava em cada instante).</summary>
        public void Ver(bool sim) => Mostrar(sim);

        /// <summary>Bola de fogo (sequência de strikes): rastro de fogo e chamas.</summary>
        public void Fogo(bool ligar)
        {
            _fogo = ligar;
            _cfgBrilhos.cores = ligar ? FOGO : ARCO;
            if (!ligar) _chamas.emitindo = false;
        }

        public bool EmFogo => _fogo;

        void FixedUpdate()
        {
            if (_interpEm > 0 && --_interpEm == 0 && rolando) rb.interpolation = RigidbodyInterpolation.Interpolate;
            PassoFisica();
        }

        /// <summary>As forças da bola num passo da física (a calibração chama direto).</summary>
        public void PassoFisica()
        {
            if (!rolando || rb.isKinematic) return;
            var p = rb.position;
            if (curva != 0f && p.z > Medidas.Z_PINO1 + 1.4f && p.y > -0.02f)
                rb.AddForce(new Vector3(curva * MASSA, 0, 0), ForceMode.Force);
            // no fosso: o chão "puxa" a bola para a caçapa (como a esteira de verdade)
            if (p.z < Medidas.Z_FIM_DECK && p.y < 0f && rb.velocity.z > -1.6f)
                rb.AddForce(new Vector3(-p.x * 3f, 0, -4f) * MASSA, ForceMode.Force);
            // entrou no túnel da caçapa: some (e volta no retorno de bolas)
            if (p.z < Medidas.Z_FUNDO - 0.35f)
            {
                rolando = false;
                _brilhos.emitindo = false;
                _chamas.emitindo = false;
                Mostrar(false);
                Parar();
                Recolhida?.Invoke();
            }
        }

        void LateUpdate()
        {
            if (!_mr.enabled)
            {
                if (_rastroMr.enabled && _pontos.Count == 0) _rastroMr.enabled = false;
                return;
            }
            // onde a bola APARECE neste quadro
            var g = transform.position;
            float alt = Mathf.Clamp(1f - (g.y - RAIO) * 1.5f, 0.3f, 1f);
            // a sombra é um adesivo reto: só no chão reto (pista, área de quem joga, fundo do
            // fosso). Indo para a canaleta ela some — dentro da calha ela atravessaria a parede.
            if (g.z > Medidas.Z_FIM_DECK && g.z < 0.12f)
                alt *= Mathf.Clamp01((Medidas.MEIA_LARGURA - Mathf.Abs(g.x)) / 0.2f);
            _sombra.position = new Vector3(g.x, Mathf.Max(g.y - RAIO, -0.3f) + 0.008f, g.z);
            _sombra.localScale = new Vector3(Mathf.Max(alt, 0.001f), 1f, Mathf.Max(alt, 0.001f));
            if (rolando) MarcarRastro(g);
            if ((rolando || emReplay) && g.z < Medidas.Z_PINO1 + 0.4f)
            {
                _brilhos.emitindo = false;
                _chamas.emitindo = false;
            }
            _brilhos.pos = g;
            _chamas.pos = g;
            DesenharRastro();
        }

        /// <summary>Mais um ponto do rastro de arco-íris (o replay chama nos mesmos instantes).</summary>
        public void MarcarRastro(Vector3 p)
        {
            // o rastro é desenhado NA PISTA: saindo dela (canaleta, fosso) ele só vai sumindo
            if (Mathf.Abs(p.x) > Medidas.MEIA_LARGURA - 0.03f || p.y - RAIO < -0.03f || p.z < Medidas.Z_FIM_DECK)
            {
                if (_pontos.Count > 0) _pontos.RemoveAt(_pontos.Count - 1);
                return;
            }
            _pontos.Insert(0, new Vector3(p.x, Mathf.Max(p.y - RAIO, 0f) + 0.012f, p.z));
            if (_pontos.Count > PONTOS) _pontos.RemoveAt(_pontos.Count - 1);
        }

        /// <summary>REPLAY: rastro recomeça e os brilhinhos voltam.</summary>
        public void Replay(bool ligar)
        {
            emReplay = ligar;
            if (ligar)
            {
                Parar();
                rb.interpolation = RigidbodyInterpolation.None;
                Mostrar(true);
            }
            _pontos.Clear();
            _brilhos.emitindo = ligar;
            _chamas.emitindo = ligar && _fogo;
            Ritmo(1f);
        }

        /// <summary>No replay: a bola é posta no lugar gravado.</summary>
        public void Pose(Vector3 pos, Quaternion rot)
        {
            rb.position = pos;
            rb.rotation = rot;
            transform.SetPositionAndRotation(pos, rot);
        }

        /// <summary>Os brilhinhos andam no ritmo pedido (1 = normal).</summary>
        public void Ritmo(float v)
        {
            _brilhos.ritmo = v;
            _chamas.ritmo = v;
        }

        static Color Gradiente(Color[] cores, float u)
        {
            u = Mathf.Clamp01(u) * (cores.Length - 1);
            int i = Mathf.Min((int)u, cores.Length - 2);
            return Color.Lerp(cores[i], cores[i + 1], u - i);
        }

        void DesenharRastro()
        {
            int n = _pontos.Count;
            if (n < 2)
            {
                _rastroMr.enabled = false;
                return;
            }
            for (int i = 0; i < n; i++)
            {
                var p = _pontos[i];
                Vector3 d = i < n - 1 ? p - _pontos[i + 1] : _pontos[i - 1] - p;
                d.y = 0f;
                if (d.sqrMagnitude < 1e-8f) d = new Vector3(0, 0, -1);
                var lado = Vector3.Cross(d.normalized, Vector3.up);
                float f = 1f - (float)i / (n - 1);
                float w = RAIO * (_fogo ? 1.2f : 0.95f) * (0.35f + 0.65f * f);
                // nunca passa da borda da pista
                w = Mathf.Clamp(Mathf.Min(w, Medidas.MEIA_LARGURA - Mathf.Abs(p.x)), 0f, w);
                Color c = _fogo ? Gradiente(FOGO, (float)i / (n - 1)) : Gradiente(ARCO, Mathf.Repeat(i / 10f, 1f));
                c.a = f * 0.75f;
                _rv[i * 2] = p + lado * w;
                _rv[i * 2 + 1] = p - lado * w;
                _rc[i * 2] = _rc[i * 2 + 1] = c;
            }
            // o resto: vértices parados no último ponto (triângulos vazios)
            for (int i = n * 2; i < _rv.Length; i++)
            {
                _rv[i] = _rv[n * 2 - 1];
                _rc[i] = new Color32(0, 0, 0, 0);
            }
            for (int i = 0; i < PONTOS - 1; i++)
            {
                int k = i * 2, t = i * 6;
                _rt[t] = k; _rt[t + 1] = k + 1; _rt[t + 2] = k + 2;
                _rt[t + 3] = k + 1; _rt[t + 4] = k + 3; _rt[t + 5] = k + 2;
            }
            _rastro.vertices = _rv;
            _rastro.colors32 = _rc;
            if (!_triProntos)
            {
                _rastro.triangles = _rt;
                _triProntos = true;
            }
            _rastro.bounds = new Bounds(new Vector3(0, 0, -5f), new Vector3(4f, 2f, 30f));
            _rastroMr.enabled = true;
        }

        public float Velocidade() => rolando && !rb.isKinematic ? rb.velocity.magnitude : 0f;

        void OnCollisionEnter(Collision c)
        {
            Bateu?.Invoke(c.collider, rb.velocity.magnitude);
        }

        /// <summary>O brilho na bola (aceso no strike / replay).</summary>
        public void Acender(float v)
        {
            if (_mat != null) _mat.SetFloat(ID_ACESO, v);
        }
    }
}
