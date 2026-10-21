using System;
using System.Collections.Generic;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// UM PERSONAGEM (Bob, o cachorrinho, ou Mia, a gatinha): modelo articulado
    /// + animação por poses + rosto com expressões + piscar + olhar para a bola
    /// + rabinho que abana + brilhos de festa + bola na mão (solta no momento
    /// certo do arremesso).
    ///
    /// Movimento suave e natural (nada treme, nada encolhe/estica):
    ///   * Entre as poses-chave: curva monotônica (PCHIP) no tempo de verdade de
    ///     cada chave (Animacoes).
    ///   * Troca de animação por INÉRCIA: o movimento que estava acontecendo
    ///     continua e se acomoda na animação nova (polinômio de 5º grau: posição,
    ///     velocidade e aceleração contínuas).
    ///   * IK de duas juntas nas pernas: o pé que apoia fica TRAVADO no chão e o
    ///     joelho dobra sozinho quando o corpo abaixa; prender/soltar o pé é
    ///     sempre gradual; se o corpo anda/gira, o pé dá um passinho.
    ///   * Pé sempre de sola chata no chão; sombra de contato embaixo de cada pé.
    ///   * Pulos com arco de gravidade; o corpo NUNCA muda de tamanho.
    ///   * Olhar e rabinho com mola amortecida (sem trancos).
    /// Rosto: peças sem luz que ligam e desligam (olhos, piscar, ^ ^, bocas).
    /// Roupinhas das fases: o pacote "&lt;nome&gt;_&lt;roupa&gt;" tem as MESMAS juntas — só
    /// a malha de cada junta troca (mais a hélice do boné e o capacete).
    /// </summary>
    public class Personagem : MonoBehaviour
    {
        public static readonly string[] EXPRESSOES = { "feliz", "piscando", "empolgado", "triste", "concentrado", "surpreso", "risada", "piscadinha" };
        static readonly string[] JUNTAS = { "corpo", "cabeca", "braco_e", "antebraco_e", "braco_d", "antebraco_d",
            "coxa_e", "canela_e", "pe_e", "coxa_d", "canela_d", "pe_d" };
        static readonly string[] PECAS = { "corpo", "cabeca", "rabo", "braco_e", "antebraco_e", "braco_d", "antebraco_d",
            "coxa_e", "canela_e", "pe_e", "coxa_d", "canela_d", "pe_d",
            "olho_e", "fechado_e", "feliz_e", "sobrancelha_e", "olho_d", "fechado_d", "feliz_d", "sobrancelha_d",
            "boca_sorriso", "boca_aberta", "boca_triste", "boca_o", "boca_reta" };
        static readonly string[] EXTRAS = { "helice", "capacete" };
        static readonly string[] BOCAS = { "sorriso", "aberta", "triste", "o", "reta" };
        static readonly string[] LADOS = { "e", "d" };
        const float PASSO_DUR = 0.22f;      // duração de um passinho automático (s)
        const float PASSO_ALTURA = 0.045f;  // quanto o pé sobe no passinho (m)
        const float DIST_PASSO = 0.13f;     // pé travado longe assim do lugar certo = dá um passo
        const float TROCA_PE = 0.14f;       // prender/soltar o pé leva este tempo (s)
        // a mão direita no antebraço (modelador: MAO - COTOVELO) e a bola logo abaixo dela
        static readonly Vector3 BOLA_NA_MAO = new Vector3(0.038f, -0.116f, 0.012f) + new Vector3(0f, -0.118f, 0.028f);
        static readonly string[] OCIOSO_CLIPES = { "acenar_uma", "olhar_volta", "danca_curta", "acenar_uma", "danca_curta" };

        public event Action<Vector3> SoltouBola;
        public event Action<string> Som;
        public event Action<string> AnimacaoTerminou;

        public string nomeModelo = "bob";
        public string roupa = "";
        /// <summary>altura do chão onde ele pisa (pódio da escolha)</summary>
        public float chao;
        /// <summary>para onde ele olha (mundo); null = para a frente</summary>
        public Vector3? olharAlvo;

        readonly Dictionary<string, Transform> _nos = new Dictionary<string, Transform>();
        readonly Dictionary<string, Transform> juntas = new Dictionary<string, Transform>();
        readonly Dictionary<string, Vector3> _posRepouso = new Dictionary<string, Vector3>();
        readonly Dictionary<string, MeshFilter> _mf = new Dictionary<string, MeshFilter>();
        readonly Dictionary<string, MeshRenderer> _mr = new Dictionary<string, MeshRenderer>();
        readonly List<MeshRenderer> _acesos = new List<MeshRenderer>();
        readonly Dictionary<string, Vector3> _centro = new Dictionary<string, Vector3>();
        MaterialPropertyBlock _mpb;
        Malhas.Pacote _padrao;
        readonly HashSet<string> _pedidos = new HashSet<string>();
        Transform _bolaMao;
        Transform _rabo, _helice;
        Transform _sombra;
        readonly Dictionary<string, Transform> _sombrasPe = new Dictionary<string, Transform>();
        Efeitos.Emissor _festa;

        // pernas (IK) e pés
        float _tornY = 0.078f;
        readonly Dictionary<string, float> _l1 = new Dictionary<string, float>(), _l2 = new Dictionary<string, float>();

        class Pe
        {
            public bool preso;
            public Vector3 pos, de, ancora;
            public float passo = -1f, pesoLin;
        }

        readonly Dictionary<string, Pe> _pes = new Dictionary<string, Pe>();
        string _modoIk = "trava";
        float _descer;

        // animação
        Animacoes.Clipe _clip;
        string _nomeClip = "";
        float _t;
        readonly HashSet<int> _eventosFeitos = new HashSet<int>();
        Dictionary<string, Vector3> _pose = new Dictionary<string, Vector3>();
        Dictionary<string, Vector3> _ultimaPose = new Dictionary<string, Vector3>();
        Dictionary<string, Vector3> _poseAnt = new Dictionary<string, Vector3>();
        readonly Dictionary<string, Vector3> _tmp0 = new Dictionary<string, Vector3>(), _tmp1 = new Dictionary<string, Vector3>();
        float _dtAnt = 1f / 60f;
        readonly Dictionary<string, float[][]> _inercia = new Dictionary<string, float[][]>();
        float _inerciaT, _inerciaDur;
        Vector3 _basePos;
        float _baseYaw;
        bool _montado;

        // rosto e vida
        string _expressao = "feliz";
        float _piscarEm = 2f, _piscando;
        Vector2 _olhar, _olharV;     // (yaw, pitch) atuais
        float _faseResp, _faseRabo, _alegria = 0.5f, _alegriaSuave = 0.5f;
        bool _ocioso;
        float _ociosoT;

        public static Personagem Criar(Transform pai, string nome)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(pai, false);
            var p = go.AddComponent<Personagem>();
            p.Montar(nome);
            return p;
        }

        static Material MaterialDe(Malhas.Parte p)
        {
            if (p.material == "luz") return Materiais.SemLuz;
            if (p.material == "vidro") return Materiais.Vidro;
            return Materiais.Facetado;
        }

        void Montar(string nome)
        {
            nomeModelo = nome;
            _mpb = new MaterialPropertyBlock();
            _padrao = Malhas.Ler(nome);
            Malhas.Montar(_padrao, transform, _nos, MaterialDe);
            foreach (var j in JUNTAS)
            {
                if (!_nos.TryGetValue(j, out var no)) continue;
                juntas[j] = no;
                _posRepouso[j] = no.localPosition;
            }
            foreach (var kv in _nos)
            {
                var mf = kv.Value.GetComponent<MeshFilter>();
                var mr = kv.Value.GetComponent<MeshRenderer>();
                if (mf == null) continue;
                _mf[kv.Key] = mf;
                _mr[kv.Key] = mr;
                if (mr.sharedMaterial == Materiais.Facetado) _acesos.Add(mr);
                if (mf.sharedMesh != null) _centro[kv.Key] = mf.sharedMesh.bounds.center;
            }
            _nos.TryGetValue("rabo", out _rabo);
            // bola na mão direita
            var bm = Malhas.Objeto("bola_mao", Bola.Malha, Bola.MaterialDe(nome), juntas["antebraco_d"]);
            _bolaMao = bm.transform;
            _bolaMao.localPosition = BOLA_NA_MAO;
            bm.SetActive(false);
            // pernas: comprimentos e altura do tornozelo
            float y = 0f;
            for (var t = juntas["pe_e"]; t != null && t != transform; t = t.parent) y += t.localPosition.y;
            _tornY = y;
            foreach (var lado in LADOS)
            {
                _l1[lado] = _posRepouso["canela_" + lado].magnitude;
                _l2[lado] = _posRepouso["pe_" + lado].magnitude;
                _pes[lado] = new Pe();
                var sp = Malhas.Objeto("sombra_pe_" + lado, Malhas.QuadradoDeitado(0.17f, 0.24f), Materiais.Sombra(0.62f), transform);
                _sombrasPe[lado] = sp.transform;
            }
            _sombra = Malhas.Objeto("sombra", Malhas.QuadradoDeitado(0.75f, 0.75f), Materiais.Sombra(0.38f), transform).transform;
            // brilhos de festa (estrelinhas subindo em volta) para as comemorações
            var cfg = new Efeitos.Config
            {
                tipo = Efeitos.Tipo.Estrela, vida = 0.9f, tam = 0.108f, direcao = Vector3.up, abertura = 25f,
                vMin = 1f, vMax = 2.2f, gravidade = new Vector3(0, -1.5f, 0), giroMin = -300f, giroMax = 300f,
                escalaMin = 0.5f, escalaMax = 1f, curva = new[] { new Vector2(0, 1), new Vector2(1, 0) },
                cores = new[] { Materiais.Hex("#FF4B5C"), Materiais.Hex("#FF9F2E"), Materiais.Hex("#FFE14D"), Materiais.Hex("#5ED16A"),
                    Materiais.Hex("#4FC3F7"), Materiais.Hex("#2E7BFF") },
                anelRaio = 0.42f, anelDentro = 0.25f, anelAltura = 0.1f,
            };
            _festa = Efeitos.I != null ? Efeitos.I.NovoEmissor(cfg, 40f) : new Efeitos.Emissor { cfg = cfg };
            _montado = true;
            _basePos = transform.localPosition;
            _baseYaw = transform.localEulerAngles.y;
            Tocar("parado");
            Expressao("feliz");
            _piscarEm = UnityEngine.Random.Range(1f, 3f);
        }

        public bool Visivel
        {
            get => gameObject.activeSelf;
            set
            {
                if (gameObject.activeSelf == value) return;
                gameObject.SetActive(value);
                if (!value) _festa.emitindo = false;
            }
        }

        // ------------------------------------------------------------ animação
        /// <summary>Troca a animação. `mistura`: segundos de transição suave.</summary>
        public void Tocar(string nome, float mistura = 0.22f, bool reiniciar = false)
        {
            if (nome == _nomeClip && !reiniciar) return;
            // o que está na tela agora e para onde estava indo (velocidade)
            var antes = new Dictionary<string, Vector3>(_ultimaPose);
            var vel = new Dictionary<string, Vector3>();
            if (_poseAnt.Count > 0)
                foreach (var kv in antes)
                    if (_poseAnt.TryGetValue(kv.Key, out var a)) vel[kv.Key] = (kv.Value - a) / Mathf.Max(_dtAnt, 0.001f);
            _clip = Animacoes.Pegar(nome);
            _nomeClip = nome;
            _modoIk = _clip.ik;
            _t = 0f;
            _eventosFeitos.Clear();
            // o quanto ele andou/girou na animação anterior vira o novo ponto de partida
            if (_montado)
            {
                _baseYaw = transform.localEulerAngles.y;
                _basePos = new Vector3(transform.localPosition.x, _basePos.y, transform.localPosition.z);
            }
            antes["frente"] = Vector3.zero;
            antes["giro"] = Vector3.zero;
            // inércia: a diferença entre a pose de agora e o começo da nova some aos poucos,
            // levando junto a velocidade que o gesto tinha
            _inercia.Clear();
            _inerciaT = 0f;
            _inerciaDur = Mathf.Max(mistura, 0.001f);
            if (_ultimaPose.Count == 0) return;
            Animacoes.Amostra(_clip, 0f, _tmp0);
            Animacoes.Amostra(_clip, 1f / 60f, _tmp1);
            var canais = new HashSet<string>(antes.Keys);
            canais.UnionWith(_tmp0.Keys);
            foreach (var k in canais)
            {
                antes.TryGetValue(k, out var va);
                _tmp0.TryGetValue(k, out var n0);
                _tmp1.TryGetValue(k, out var n1);
                vel.TryGetValue(k, out var vv);
                var x0 = va - n0;
                var v0 = vv - (n1 - n0) * 60f;
                if (k == "giro") _inercia[k] = new[] { Coef(x0.x, v0.x, _inerciaDur, 420f, 10f) };
                else if (Animacoes.ESCALARES.Contains(k)) _inercia[k] = new[] { Coef(x0.x, v0.x, _inerciaDur, 0.8f, 0.015f) };
                else
                {
                    float vmax = k == "quadril" ? 0.8f : 420f;
                    float lim = k == "quadril" ? 0.015f : 12f;
                    _inercia[k] = new[] { Coef(x0.x, v0.x, _inerciaDur, vmax, lim), Coef(x0.y, v0.y, _inerciaDur, vmax, lim), Coef(x0.z, v0.z, _inerciaDur, vmax, lim) };
                }
            }
            foreach (var c in _inercia.Values)
                foreach (var e in c)
                    if (e != null) _inerciaDur = Mathf.Max(_inerciaDur, e[5]);
        }

        /// <summary>Transição por inércia de solavanco mínimo: o deslocamento x0 entre a pose de
        /// agora e a nova, com a velocidade v0 que o gesto tinha, vai a zero por um polinômio de
        /// 5º grau que começa SEM arranque e chega parado. O tempo cresce com o tamanho do
        /// movimento (no máximo `vmax` por segundo) e a velocidade herdada é limitada para nunca
        /// passar do ponto nem seguir longe demais (`lim`). [A, B, C, v0, x0, t1, sinal]</summary>
        static float[] Coef(float x0, float v0, float t1, float vmax, float lim)
        {
            if (Mathf.Abs(x0) < 0.00001f && Mathf.Abs(v0) < 0.0001f) return null;
            float s = x0 >= 0f ? 1f : -1f;
            float x = x0 * s, v = v0 * s;
            float t = Mathf.Clamp(Mathf.Max(t1, 1.875f * x / vmax), t1, 0.7f);
            if (v < 0f && -v * t > 2.5f * x)
            {
                t = Mathf.Max(2.5f * x / -v, Mathf.Min(t1, 0.12f));
                v = Mathf.Max(v, -2.5f * x / t);
            }
            else if (v > 0f) v = Mathf.Min(v, Mathf.Max(2f * x, lim / 0.185f) / t);
            float cc = -(6f * v * t + 10f * x) / Mathf.Pow(t, 3f);
            float bb = (8f * v * t + 15f * x) / Mathf.Pow(t, 4f);
            float aa = -(3f * v * t + 6f * x) / Mathf.Pow(t, 5f);
            return new[] { aa, bb, cc, v, x, t, s };
        }

        static float Inercia(float[] c, float t)
        {
            if (c == null || t >= c[5]) return 0f;
            return c[6] * ((((c[0] * t + c[1]) * t + c[2]) * t * t * t) + c[3] * t + c[4]);
        }

        /// <summary>Modo "à vontade" (atração, escolha): fica parado respirando e, de vez em
        /// quando, acena, olha em volta, dança um pouquinho — e volta.</summary>
        public void Ocioso(bool ligar)
        {
            _ocioso = ligar;
            _ociosoT = UnityEngine.Random.Range(1.5f, 4f);
            if (ligar) Tocar("parado", 0.3f);
        }

        void OciosoPasso(float dt)
        {
            if (!_ocioso || _nomeClip != "parado") return;
            _ociosoT -= dt;
            if (_ociosoT <= 0f)
            {
                _ociosoT = UnityEngine.Random.Range(4f, 8.5f);
                Tocar(OCIOSO_CLIPES[UnityEngine.Random.Range(0, OCIOSO_CLIPES.Length)], 0.35f, true);
            }
        }

        public string Animacao => _nomeClip;

        void Update()
        {
            if (!_montado || _clip == null) return;
            float dt = Time.deltaTime;
            var c = _clip;
            float tAntes = _t;
            _t += dt;
            bool terminou = false;
            if (c.loop) _t = Mathf.Repeat(_t, c.dur);
            else if (_t >= c.dur)
            {
                _t = c.dur;
                terminou = tAntes < c.dur;
            }
            OciosoPasso(dt);
            if (c != _clip) return;   // o ócio trocou o clipe: o próximo quadro já usa o novo
            Eventos(tAntes, _t, c.loop && _t < tAntes);
            Animacoes.Amostra(c, _t, _pose);
            if (_inercia.Count > 0)
            {
                _inerciaT += dt;
                if (_inerciaT >= _inerciaDur) _inercia.Clear();
                else
                    foreach (var kv in _inercia)
                    {
                        _pose.TryGetValue(kv.Key, out var v);
                        var e = kv.Value;
                        if (e.Length == 1) v.x += Inercia(e[0], _inerciaT);
                        else v += new Vector3(Inercia(e[0], _inerciaT), Inercia(e[1], _inerciaT), Inercia(e[2], _inerciaT));
                        _pose[kv.Key] = v;
                    }
            }
            // gira os três dicionários (sem lixo para o coletor)
            var livre = _poseAnt;
            _poseAnt = _ultimaPose;
            _ultimaPose = _pose;
            _pose = livre;
            _dtAnt = dt;
            Aplicar(_ultimaPose, dt);
            Rosto(dt);
            if (_helice != null && _helice.gameObject.activeSelf)
                _helice.Rotate(0, -dt * (7f + 9f * _alegriaSuave) * Mathf.Rad2Deg, 0, Space.Self);
            _festa.pos = transform.position + new Vector3(0, 0.25f, 0);
            if (terminou)
            {
                AnimacaoTerminou?.Invoke(_nomeClip);
                // clipe que emenda em outro (ex.: vitória -> festa, aceno -> parado)
                if (_clip == c && !string.IsNullOrEmpty(c.depois)) Tocar(c.depois, 0.35f);
            }
        }

        void Eventos(float de, float ate, bool deuVolta)
        {
            var evs = _clip.eventos;
            for (int i = 0; i < evs.Length; i++)
            {
                var ev = evs[i];
                float t = ev.t;
                bool passou = (t > de && t <= ate) || (de == 0f && t == 0f && !_eventosFeitos.Contains(i)) || (deuVolta && (t > de || t <= ate));
                if (!passou || _eventosFeitos.Contains(i)) continue;
                _eventosFeitos.Add(i);
                switch (ev.tipo)
                {
                    case "soltar":
                        if (_bolaMao.gameObject.activeSelf)
                        {
                            var p = _bolaMao.position;
                            _bolaMao.gameObject.SetActive(false);
                            SoltouBola?.Invoke(p);
                        }
                        break;
                    case "jatos":
                        Jatos(ev.valor == "1");
                        break;
                    case "expressao":
                        Expressao(ev.valor);
                        break;
                    case "som":
                        Som?.Invoke(ev.valor);
                        break;
                }
            }
            if (deuVolta) _eventosFeitos.Clear();
        }

        static Vector3 Pegar(Dictionary<string, Vector3> d, string k)
        {
            d.TryGetValue(k, out var v);
            return v;
        }

        void Aplicar(Dictionary<string, Vector3> pose, float dt)
        {
            var corpoRot = Pegar(pose, "corpo");
            // respiração (bem de leve)
            _faseResp = Mathf.Repeat(_faseResp + dt * 2.4f, Mathf.PI * 2f);
            float resp = Mathf.Sin(_faseResp) * 0.004f;
            float altura = Pegar(pose, "altura").x + resp;
            var quadril = Pegar(pose, "quadril");
            float voo = Mathf.Max(Pegar(pose, "voo").x, 0f);
            float frente = Pegar(pose, "frente").x;
            float giro = Pegar(pose, "giro").x;
            foreach (var kv in juntas)
            {
                var r = Pegar(pose, kv.Key);
                if (kv.Key.StartsWith("coxa", StringComparison.Ordinal)) r.x -= corpoRot.x;   // pernas em relação ao chão
                if (kv.Key == "cabeca") r += new Vector3(_olhar.y, _olhar.x, 0);
                // rotação nova a cada quadro (rotação pura, escala 1: nada acumula)
                kv.Value.localRotation = Quaternion.Euler(r);
            }
            juntas["corpo"].localPosition = _posRepouso["corpo"] + new Vector3(quadril.x, altura, quadril.z);
            // deslocamento da corrida (para a frente do personagem) e voo
            var frenteMundo = Quaternion.Euler(0, _baseYaw, 0) * Vector3.forward * frente;
            transform.localPosition = _basePos + frenteMundo + new Vector3(0, voo, 0);
            transform.localRotation = Quaternion.Euler(0, _baseYaw + giro, 0);
            IkPernas(dt, voo);
            Sombras(voo);
            OlharPasso(dt);
            // rabinho abanando (mais rápido quando está feliz; a fase anda sozinha)
            if (_rabo != null)
            {
                _alegriaSuave = Mathf.Lerp(_alegriaSuave, _alegria, 1f - Mathf.Exp(-dt * 3f));
                _faseRabo = Mathf.Repeat(_faseRabo + dt * (7f + _alegriaSuave * 9f), Mathf.PI * 4f);
                _rabo.localRotation = Quaternion.Euler(Mathf.Sin(_faseRabo * 0.5f) * 0.12f * Mathf.Rad2Deg,
                    -Mathf.Sin(_faseRabo) * (0.35f + _alegriaSuave * 0.35f) * Mathf.Rad2Deg, 0);
            }
        }

        /// <summary>Olha para a bola com uma mola amortecida (sem trancos). Só olha para o que
        /// está na frente: atrás, o pescoço ficava virando de um lado para o outro.</summary>
        void OlharPasso(float dt)
        {
            var alvo = Vector2.zero;
            if (olharAlvo.HasValue)
            {
                var local = juntas["corpo"].InverseTransformPoint(olharAlvo.Value);
                float horiz = new Vector2(local.x, local.z).magnitude;
                float frente = Mathf.Clamp01((local.z - 0.05f) / 0.3f) * Mathf.Clamp01(horiz / 0.35f);
                if (frente > 0f)
                {
                    float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                    float pitch = -Mathf.Atan2(local.y - 0.55f, horiz) * Mathf.Rad2Deg;
                    alvo = new Vector2(Mathf.Clamp(yaw, -55f, 55f), Mathf.Clamp(pitch, -25f, 30f)) * frente;
                }
            }
            const float w = 9f;
            float resto = dt;
            while (resto > 0f)
            {
                float h = Mathf.Min(resto, 1f / 60f);
                _olharV += ((alvo - _olhar) * w * w - _olharV * 2f * w) * h;
                _olhar += _olharV * h;
                resto -= h;
            }
        }

        // ------------------------------------------------------------ pernas (IK)
        /// <summary>IK das pernas com pé travado no chão. Cada pé tem um "peso" de apoio que vai
        /// de 0 (segue a animação) a 1 (preso no chão) aos poucos.</summary>
        void IkPernas(float dt, float voo)
        {
            var alvos = new Vector3[2];
            bool passando = false;
            foreach (var lado in LADOS)
                if (_pes[lado].passo >= 0f) passando = true;
            var corpo = juntas["corpo"];
            float ty = chao + _tornY;
            for (int k = 0; k < 2; k++)
            {
                string lado = LADOS[k];
                var pe = juntas["pe_" + lado];
                var st = _pes[lado];
                var fk = pe.position;
                float alturaPe = fk.y - ty;
                var livre = new Vector3(fk.x, Mathf.Max(fk.y, ty), fk.z);
                if (_modoIk != "trava" || voo > 0.01f)
                {
                    st.preso = false;
                    st.passo = -1f;
                }
                else if (st.passo >= 0f)
                {
                    // passinho em andamento: vai do lugar antigo ao novo, subindo um pouco
                    st.passo = Mathf.Min(1f, st.passo + dt / PASSO_DUR);
                    float u = st.passo;
                    var para = new Vector3(fk.x, ty, fk.z);
                    float e = u * u * u * (u * (u * 6f - 15f) + 10f);
                    st.ancora = Vector3.Lerp(st.de, para, e) + new Vector3(0, PASSO_ALTURA * Mathf.Sin(Mathf.PI * u), 0);
                    if (u >= 1f)
                    {
                        st.passo = -1f;
                        st.preso = true;
                        st.pos = para;
                        st.ancora = para;
                    }
                }
                else if (st.preso)
                {
                    float longe = new Vector2(fk.x - st.pos.x, fk.z - st.pos.z).magnitude;
                    if (alturaPe > 0.05f) st.preso = false;       // a perna levantou de propósito
                    else if (longe > DIST_PASSO && !passando)
                    {
                        st.preso = false;                            // ficou para trás: um passinho
                        st.passo = 0f;
                        st.de = st.pos;
                        passando = true;
                    }
                    st.ancora = st.pos;
                }
                else if (alturaPe < 0.02f)
                {
                    st.preso = true;
                    st.pos = new Vector3(fk.x, ty, fk.z);
                    st.ancora = st.pos;
                }
                // peso do apoio: sobe/desce em TROCA_PE segundos (com curva suave)
                float quer = st.preso || st.passo >= 0f ? 1f : 0f;
                st.pesoLin = Mathf.MoveTowards(st.pesoLin, quer, dt / TROCA_PE);
                float wgt = st.pesoLin * st.pesoLin * (3f - 2f * st.pesoLin);
                alvos[k] = wgt > 0f ? Vector3.Lerp(livre, st.ancora, wgt) : livre;
            }
            // o quadril desce (suave) se um pé apoiado não alcançaria o chão
            float descer = 0f;
            for (int k = 0; k < 2; k++)
            {
                string lado = LADOS[k];
                var q = juntas["coxa_" + lado].position;
                float lmax = (_l1[lado] + _l2[lado]) * 0.985f;
                float dv = q.y - alvos[k].y;
                float dh = new Vector2(q.x - alvos[k].x, q.z - alvos[k].z).magnitude;
                if (dh < lmax) descer = Mathf.Max(descer, dv - Mathf.Sqrt(lmax * lmax - dh * dh));
            }
            descer = Mathf.Clamp(descer, 0f, 0.12f);
            float rapidez = descer > _descer ? 40f : 12f;
            _descer = Mathf.Lerp(_descer, descer, 1f - Mathf.Exp(-dt * rapidez));
            if (_descer > 0.0005f)
            {
                var p = corpo.localPosition;
                p.y -= _descer;
                corpo.localPosition = p;
            }
            for (int k = 0; k < 2; k++) ResolverPerna(LADOS[k], alvos[k], _pes[LADOS[k]].pesoLin > 0.5f);
        }

        void ResolverPerna(string lado, Vector3 alvo, bool apoiado)
        {
            var coxa = juntas["coxa_" + lado];
            var canela = juntas["canela_" + lado];
            var pe = juntas["pe_" + lado];
            float l1 = _l1[lado], l2 = _l2[lado];
            var q = coxa.position;
            var jFk = canela.position;
            var v = alvo - q;
            float d = Mathf.Clamp(v.magnitude, Mathf.Abs(l1 - l2) + 0.001f, l1 + l2 - 0.0008f);
            var dir = v.normalized;
            float a = (l1 * l1 - l2 * l2 + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(l1 * l1 - a * a, 0f));
            // o joelho dobra para onde a pose mandou (e sempre para a frente)
            var frente = transform.forward;
            var polo = (jFk - q) - dir * Vector3.Dot(jFk - q, dir) + frente * 0.02f;
            polo -= dir * Vector3.Dot(polo, dir);
            if (polo.sqrMagnitude < 1e-8f) polo = frente;
            polo.Normalize();
            var j = q + dir * a + polo * h;
            var de1 = (jFk - q).normalized;
            var para1 = (j - q).normalized;
            if (Vector3.Dot(de1, para1) < 0.99999f) coxa.rotation = Quaternion.FromToRotation(de1, para1) * coxa.rotation;
            var aAgora = pe.position;
            var de2 = (aAgora - j).normalized;
            var para2 = (q + dir * d - j).normalized;
            if (Vector3.Dot(de2, para2) < 0.99999f) canela.rotation = Quaternion.FromToRotation(de2, para2) * canela.rotation;
            // pé de sola chata quando está no chão (ou perto dele)
            float peso = apoiado ? 1f : Mathf.Clamp01(1f - (alvo.y - chao - _tornY) / 0.08f);
            if (peso > 0f)
            {
                var plano = Quaternion.Inverse(canela.rotation) * Quaternion.Euler(0, transform.eulerAngles.y, 0);
                pe.localRotation = Quaternion.Slerp(pe.localRotation, plano, peso);
            }
        }

        void Sombras(float voo)
        {
            // sombra do corpo: fica no chão, menor quando voa
            var g = transform.position;
            _sombra.SetPositionAndRotation(new Vector3(g.x, chao + 0.006f, g.z), Quaternion.identity);
            float esc = Mathf.Clamp(1f - voo * 0.9f, 0.35f, 1f);
            _sombra.localScale = new Vector3(esc, 1f, esc);
            // sombra de contato de cada pé (some quando o pé sobe)
            var giro = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            foreach (var lado in LADOS)
            {
                var pe = juntas["pe_" + lado];
                var p = pe.position + pe.forward * 0.025f;
                float alto = Mathf.Max(0f, p.y - chao - _tornY);
                float e = Mathf.Clamp(1f - alto / 0.25f, 0.25f, 1f);
                _sombrasPe[lado].SetPositionAndRotation(new Vector3(p.x, chao + 0.008f, p.z), giro);
                _sombrasPe[lado].localScale = new Vector3(e, 1f, e);
            }
        }

        // ------------------------------------------------------------ rosto
        struct Cara
        {
            public string olhos;     // "olho" | "feliz" | "piscadinha"
            public string boca;
            public float sobe;       // sobrancelhas para cima (m)
            public float inclina;    // graus: + = ponta de dentro para cima (tristinho), - = franzida
            public float olhoEsc;
            public float alegria;
        }

        static readonly Dictionary<string, Cara> CARAS = new Dictionary<string, Cara>
        {
            ["feliz"] = new Cara { olhos = "olho", boca = "sorriso", sobe = 0f, inclina = 0f, olhoEsc = 1f, alegria = 0.7f },
            ["empolgado"] = new Cara { olhos = "olho", boca = "aberta", sobe = 0.012f, inclina = 0f, olhoEsc = 1.08f, alegria = 1f },
            ["triste"] = new Cara { olhos = "olho", boca = "triste", sobe = 0.004f, inclina = 14f, olhoEsc = 0.95f, alegria = 0f },
            ["concentrado"] = new Cara { olhos = "olho", boca = "reta", sobe = -0.006f, inclina = -10f, olhoEsc = 1f, alegria = 0.25f },
            ["surpreso"] = new Cara { olhos = "olho", boca = "o", sobe = 0.016f, inclina = 0f, olhoEsc = 1.15f, alegria = 0.5f },
            ["risada"] = new Cara { olhos = "feliz", boca = "aberta", sobe = 0.01f, inclina = 0f, olhoEsc = 1f, alegria = 1f },
            ["piscadinha"] = new Cara { olhos = "piscadinha", boca = "sorriso", sobe = 0.006f, inclina = 0f, olhoEsc = 1f, alegria = 0.8f },
            ["piscando"] = new Cara { olhos = "fechado", boca = "sorriso", sobe = 0f, inclina = 0f, olhoEsc = 1f, alegria = 0.5f },
        };

        public string ExpressaoAtual => _expressao;

        public void Expressao(string nome)
        {
            if (!CARAS.ContainsKey(nome)) nome = "feliz";
            _expressao = nome;
            _alegria = CARAS[nome].alegria;
            MostrarCara(nome, false);
        }

        void Ligar(string parte, bool sim)
        {
            if (_nos.TryGetValue(parte, out var t) && t.gameObject.activeSelf != sim) t.gameObject.SetActive(sim);
        }

        /// <summary>Liga as peças do rosto da expressão. piscar = só os olhos fechados.</summary>
        void MostrarCara(string nome, bool piscar)
        {
            var c = CARAS[nome];
            string olhos = piscar ? "fechado" : c.olhos;
            Ligar("olho_e", olhos == "olho" || olhos == "piscadinha");
            Ligar("olho_d", olhos == "olho");
            Ligar("fechado_e", olhos == "fechado");
            Ligar("fechado_d", olhos == "fechado");
            Ligar("feliz_e", olhos == "feliz");
            Ligar("feliz_d", olhos == "feliz" || olhos == "piscadinha");
            if (piscar) return;
            foreach (var b in BOCAS) Ligar("boca_" + b, b == c.boca);
            // sobrancelhas: sobem / inclinam em volta do próprio centro
            for (int k = 0; k < 2; k++)
            {
                string nm = "sobrancelha_" + LADOS[k];
                if (!_nos.TryGetValue(nm, out var t) || !_centro.TryGetValue(nm, out var centro)) continue;
                float s = k == 0 ? 1f : -1f;   // a esquerda fica em -X: ponta de dentro em +X
                var r = Quaternion.Euler(0, 0, s * c.inclina);
                t.localRotation = r;
                t.localPosition = centro + new Vector3(0, c.sobe, 0) - r * centro;
            }
            // olhos arregalados (surpresa) ou apertadinhos
            foreach (var nm in new[] { "olho_e", "olho_d" })
            {
                if (!_nos.TryGetValue(nm, out var t) || !_centro.TryGetValue(nm, out var centro)) continue;
                t.localScale = Vector3.one * c.olhoEsc;
                t.localPosition = centro - centro * c.olhoEsc;
            }
        }

        void Rosto(float dt)
        {
            if (_piscando > 0f)
            {
                _piscando -= dt;
                if (_piscando <= 0f) MostrarCara(_expressao, false);
                return;
            }
            _piscarEm -= dt;
            if (_piscarEm <= 0f)
            {
                _piscarEm = UnityEngine.Random.Range(2f, 4.5f);
                if (_expressao == "feliz" || _expressao == "concentrado" || _expressao == "surpreso" || _expressao == "empolgado")
                {
                    _piscando = 0.13f;
                    MostrarCara(_expressao, true);
                }
            }
        }

        // ------------------------------------------------------------ festa
        /// <summary>Estrelinhas subindo em volta (comemorações).</summary>
        public void Jatos(bool ligar) => _festa.emitindo = ligar;

        // ------------------------------------------------------------ roupinhas das fases
        string Pacote(string r) => nomeModelo + "_" + r;

        /// <summary>Carrega uma roupinha antes de usar (numa hora calma: a troca fica instantânea).</summary>
        public void PedirRoupa(string r)
        {
            if (string.IsNullOrEmpty(r) || r == roupa || _pedidos.Contains(r)) return;
            if (!Malhas.Existe(Pacote(r))) return;
            Malhas.Ler(Pacote(r));
            _pedidos.Add(r);
        }

        /// <summary>Roupinhas pedidas que não vão mais ser usadas: saem da memória.</summary>
        public void SoltarPedidos()
        {
            foreach (var r in _pedidos)
                if (r != roupa) Malhas.Soltar(Pacote(r));
            _pedidos.Clear();
        }

        /// <summary>Veste a roupinha `r` ("" = a de sempre).</summary>
        public void TrocarRoupa(string r)
        {
            if (r == null) r = "";
            if (r == roupa) return;
            Malhas.Pacote pc = _padrao;
            if (r != "")
            {
                if (Malhas.Existe(Pacote(r))) pc = Malhas.Ler(Pacote(r));
                else r = "";
            }
            if (r == roupa) return;
            string antiga = roupa;
            roupa = r;
            foreach (var j in PECAS)
            {
                if (!_mf.TryGetValue(j, out var mf)) continue;
                var p = pc.Achar(j);
                var m = p != null ? p.malha : null;
                if (m == null && pc != _padrao) m = _padrao.Malha(j);
                mf.sharedMesh = m;
                _mr[j].enabled = m != null;
            }
            // extras (hélice do boné, capacete do astronauta): filhos da cabeça
            foreach (var nm in EXTRAS)
            {
                var p = pc.Achar(nm);
                _nos.TryGetValue(nm, out var t);
                if (p == null || p.malha == null)
                {
                    if (t != null) t.gameObject.SetActive(false);
                    continue;
                }
                if (t == null)
                {
                    var go = Malhas.Objeto(nm, p.malha, MaterialDe(p), juntas["cabeca"]);
                    t = go.transform;
                    _nos[nm] = t;
                    if (nm == "helice") _helice = t;
                    var mr = go.GetComponent<MeshRenderer>();
                    if (mr.sharedMaterial == Materiais.Facetado) _acesos.Add(mr);
                }
                t.GetComponent<MeshFilter>().sharedMesh = p.malha;
                t.localPosition = p.pos;
                t.localRotation = Quaternion.identity;
                t.gameObject.SetActive(true);
            }
            _pedidos.Remove(r);
            if (antiga != "") Malhas.Soltar(Pacote(antiga));
            Destaque(_flash);
        }

        // ------------------------------------------------------------ utilidades
        public void SegurarBola(bool sim) => _bolaMao.gameObject.SetActive(sim);

        public bool SegurandoBola => _bolaMao.gameObject.activeSelf;

        /// <summary>Coloca o personagem num lugar, olhando para `direcaoY` (graus, Unity).</summary>
        public void Posicionar(Vector3 pos, float direcaoY)
        {
            transform.localPosition = pos;
            chao = pos.y;
            transform.localRotation = Quaternion.Euler(0, direcaoY, 0);
            _basePos = pos;
            _baseYaw = direcaoY;
            foreach (var st in _pes.Values)
            {
                st.preso = false;
                st.passo = -1f;
                st.pesoLin = 0f;
            }
            _descer = 0f;
        }

        /// <summary>Muda só o X da base (o passo para o lado antes do arremesso).</summary>
        public float BaseX
        {
            get => _basePos.x;
            set => _basePos.x = value;
        }

        public Vector3 BasePos => _basePos;

        float _flash;

        /// <summary>Brilho de destaque (personagem da vez fica "aceso").</summary>
        public void Destaque(float valor)
        {
            _flash = valor;
            _mpb.SetFloat(Materiais.ID_FLASH, valor);
            foreach (var mr in _acesos) mr.SetPropertyBlock(_mpb);
        }

        /// <summary>Onde a mão direita fica (para a câmera mirar).</summary>
        public Vector3 PosicaoMao() => _bolaMao.position;
    }
}
