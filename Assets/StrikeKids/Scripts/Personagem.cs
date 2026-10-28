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
    /// O MODELO É O DA EDIÇÃO GODOT 7.6 (tools/godot/exportar_palco.gd): as mesmas
    /// juntas e malhas, o toon de personagem (G_toon_personagem: luz de palco por
    /// vértice, borda iluminada, brilho) e o contorno (G_contorno, a casca invertida).
    /// Rosto: a arte de cada expressão numa textura (rosto_&lt;nome&gt;, 4 x 2 células)
    /// projetada de frente na cabeça; trocar de cara (e piscar) é trocar a célula.
    /// Estampa da roupa (estrela, bolinhas, listras) desenhada no shader.
    /// Roupinhas das fases: o pacote "&lt;nome&gt;_&lt;roupa&gt;" tem as MESMAS juntas — só
    /// a malha de cada junta troca (mais a hélice do boné).
    /// NÍVEIS DE DETALHE (com os detalhes extras): cada peça tem três malhas — a de
    /// PERTO ("_hd", ~2,5x), a do MEIO e a de LONGE ("_longe", ~40%). O longe é dele
    /// mesmo (pequenininho na tela); o perto quem dá é o jogo (Principal: só para o
    /// bichinho maior na tela, inteiro nela, e se o quadro tiver folga).
    /// </summary>
    public class Personagem : MonoBehaviour
    {
        public static readonly string[] EXPRESSOES = { "feliz", "piscando", "empolgado", "triste", "concentrado", "surpreso", "risada", "piscadinha" };
        static readonly string[] JUNTAS = { "corpo", "cabeca", "braco_e", "antebraco_e", "braco_d", "antebraco_d",
            "coxa_e", "canela_e", "pe_e", "coxa_d", "canela_d", "pe_d" };
        static readonly string[] PECAS = { "corpo", "cabeca", "rabo", "braco_e", "antebraco_e", "braco_d", "antebraco_d",
            "coxa_e", "canela_e", "pe_e", "coxa_d", "canela_d", "pe_d" };
        static readonly string[] LADOS = { "e", "d" };
        static readonly string[] NIVEIS = { "perto", "meio", "longe" };
        // fração da altura da tela: candidato ao de perto / abaixo, o de longe (com folga)
        const float NIVEL_PERTO = 0.19f, NIVEL_PERTO_VOLTA = 0.17f;
        const float NIVEL_LONGE = 0.09f, NIVEL_LONGE_VOLTA = 0.11f;
        const float ALTURA_BICHO = 0.95f;   // do pé ao topo da cabeça (m)
        const float PASSO_DUR = 0.22f;      // duração de um passinho automático (s)
        const float PASSO_ALTURA = 0.045f;  // quanto o pé sobe no passinho (m)
        const float DIST_PASSO = 0.13f;     // pé travado longe assim do lugar certo = dá um passo
        const float TROCA_PE = 0.14f;       // prender/soltar o pé leva este tempo (s)
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
        readonly List<Material> _mats = new List<Material>();
        Material _matCabeca, _matCorpo, _matMembros, _matRabo, _matContorno;
        Dictionary<string, object> _meta;
        readonly HashSet<string> _pedidos = new HashSet<string>();
        // as malhas de cada nível da roupa de agora (junta -> malha)
        readonly Dictionary<string, Dictionary<string, Mesh>> _malhasNivel = new Dictionary<string, Dictionary<string, Mesh>>();
        string _nivel = "meio";
        float _fracao;
        bool _dentro;
        int _custoPerto;
        int _forcar;
        /// <summary>Os níveis de detalhe ligados (os detalhes extras: Ajustes.Detalhes).</summary>
        public bool niveis;
        /// <summary>O jogo deixou vestir o de perto (o orçamento do quadro: Principal).</summary>
        public bool podePerto { get; private set; }
        /// <summary>Contornos (a casca escura em volta): desligados na qualidade baixa.</summary>
        public static bool contornos = true;
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

        public static Personagem Criar(Transform pai, string nome, bool niveis = false)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(pai, false);
            var p = go.AddComponent<Personagem>();
            p.niveis = niveis;
            p.Montar(nome);
            return p;
        }

        // ------------------------------------------------------------ dados dos bichinhos
        static Dictionary<string, object> _personagens;

        /// <summary>Rosto, mão, tornozelo e estampa de cada pacote (Palco/personagens.json, da
        /// edição Godot: os pontos no espaço de lá).</summary>
        public static Dictionary<string, object> Meta(string pacote)
        {
            if (_personagens == null)
            {
                var ta = Resources.Load<TextAsset>("Palco/personagens");
                _personagens = ta != null ? Json.Dic(Json.Ler(ta.text)) : null;
                if (ta != null) Resources.UnloadAsset(ta);
                if (_personagens == null) _personagens = new Dictionary<string, object>();
            }
            return _personagens.TryGetValue(pacote, out var m) ? Json.Dic(m) : null;
        }

        static Vector4 V(object o) => Palco.Vetor(o);

        /// <summary>O toon de personagem com os parâmetros dos bichinhos (borda iluminada, faixa
        /// da luz, luz mínima na sombra e o brilho) — um material por bichinho (o destaque e a
        /// cara são dele).</summary>
        Material Toon(string nome, bool brilho)
        {
            var m = Materiais.Novo("G/toon_personagem", nomeModelo + "_" + nome);
            m.SetFloat("_borda", 0.22f);
            m.SetFloat("_faixa", 0.15f);
            m.SetFloat("_luz_minima", 0.32f);
            if (brilho)
            {
                m.SetVector("_cor_borda", new Vector4(1f, 0.92f, 1f, 1f));
                m.SetFloat("_brilho_luz", 1.4f);
            }
            _mats.Add(m);
            return m;
        }

        Material[] MateriaisDa(string junta)
        {
            Material m;
            switch (junta)
            {
                case "cabeca": m = _matCabeca; break;
                case "corpo": m = _matCorpo; break;
                case "rabo":
                case "helice": m = _matRabo; break;
                default: m = _matMembros; break;
            }
            return _matContorno != null ? new[] { m, _matContorno } : new[] { m };
        }

        void Montar(string nome)
        {
            nomeModelo = nome;
            _meta = Meta(nome) ?? new Dictionary<string, object>();
            // os materiais (braços, antebraços, coxas, canelas e pés têm os MESMOS parâmetros:
            // um material só para todos)
            _matCabeca = Toon("cabeca", true);
            _matCabeca.SetFloat("_usa_rosto", 1f);
            _matCabeca.SetTexture("_rosto", Palco.Textura("rosto_" + nome));
            if (_meta.TryGetValue("rosto", out var ro) && Json.Dic(ro) is Dictionary<string, object> rosto && rosto.ContainsKey("centro"))
            {
                _matCabeca.SetVector("_rosto_centro", V(rosto["centro"]));
                _matCabeca.SetVector("_rosto_tamanho", V(rosto["tamanho"]));
            }
            _matCorpo = Toon("corpo", true);
            AplicarEstampa(Json.Dic(Json.Pegar(_meta, "estampa")));
            _matMembros = Toon("membros", true);
            _matRabo = Toon("rabo", false);
            if (contornos)
            {
                _matContorno = Materiais.Novo("G/contorno", nome + "_contorno");
                _matContorno.SetFloat("_espessura", 0.0032f);
            }
            var padrao = Malhas.Ler(nome);
            Malhas.Montar(padrao, transform, _nos, p => MateriaisDa(p.nome)[0]);
            foreach (var j in JUNTAS)
            {
                if (!_nos.TryGetValue(j, out var no)) continue;
                juntas[j] = no;
                _posRepouso[j] = no.localPosition;
            }
            foreach (var kv in _nos)
            {
                var mf = kv.Value.GetComponent<MeshFilter>();
                if (mf == null) continue;
                _mf[kv.Key] = mf;
                _mr[kv.Key] = kv.Value.GetComponent<MeshRenderer>();
                _mr[kv.Key].sharedMaterials = MateriaisDa(kv.Key);
            }
            _nos.TryGetValue("rabo", out _rabo);
            _malhasNivel["meio"] = MalhasDe(nome);
            _malhasNivel["perto"] = niveis ? MalhasDe(nome + "_hd") : new Dictionary<string, Mesh>();
            _malhasNivel["longe"] = niveis ? MalhasDe(nome + "_longe") : new Dictionary<string, Mesh>();
            // bola na mão direita (a mão do modelo e a bola logo abaixo dela)
            var bm = Malhas.Objeto("bola_mao", Bola.Malha, Bola.MaterialDe(nome), juntas["antebraco_d"]);
            _bolaMao = bm.transform;
            var mao = Json.Lst(Json.Pegar(_meta, "mao_d"));
            Vector3 maoG = mao != null && mao.Count == 3 ? new Vector3((float)Json.Num(mao[0]), (float)Json.Num(mao[1]), (float)Json.Num(mao[2])) : new Vector3(-0.034f, -0.15f, 0.025f);
            _bolaMao.localPosition = Medidas.G(maoG.x, maoG.y, maoG.z) + new Vector3(0f, -0.118f, 0.028f);
            bm.SetActive(false);
            // pernas: comprimentos e altura do tornozelo
            _tornY = (float)Json.Num(Json.Pegar(_meta, "tornozelo_y", 0.078), 0.078);
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

        /// <summary>junta -> malha das peças de um pacote (vazio se ele não existe).</summary>
        static Dictionary<string, Mesh> MalhasDe(string pacote)
        {
            var d = new Dictionary<string, Mesh>();
            if (!Malhas.Existe(pacote)) return d;
            foreach (var p in Malhas.Ler(pacote).partes)
                if (p.malha != null) d[p.nome] = p.malha;
            return d;
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
            _forcar = 3;
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
            // FORA DO QUADRO: a animação continua contando (eventos, fim do clipe, a pose
            // para as transições), mas juntas, pernas, olhar e rosto só mexem quando ele
            // aparece. Segurando a bola ou logo depois de mudar de lugar/animação, sempre.
            if (_forcar > 0) _forcar--;
            else if (!_bolaMao.gameObject.activeSelf && !NaTela())
            {
                FimDoQuadro(terminou, c);
                return;
            }
            if (niveis) EscolherNivel();
            Aplicar(_ultimaPose, dt);
            Rosto(dt);
            if (_helice != null && _helice.gameObject.activeSelf)
                _helice.Rotate(0, -dt * (7f + 9f * _alegriaSuave) * Mathf.Rad2Deg, 0, Space.Self);
            _festa.pos = transform.position + new Vector3(0, 0.25f, 0);
            FimDoQuadro(terminou, c);
        }

        void FimDoQuadro(bool terminou, Animacoes.Clipe c)
        {
            if (!terminou) return;
            AnimacaoTerminou?.Invoke(_nomeClip);
            // clipe que emenda em outro (ex.: vitória -> festa, aceno -> parado)
            if (_clip == c && !string.IsNullOrEmpty(c.depois)) Tocar(c.depois, 0.35f);
        }

        // ------------------------------------------------------------ na tela e níveis
        static readonly Plane[] _planos = new Plane[6];
        static int _quadroPlanos = -1;
        static Camera _camPlanos;

        /// <summary>A caixa que ele pode ocupar (com braços, pulos e voo) está no quadro?</summary>
        bool NaTela()
        {
            var cam = Camera.main;
            if (cam == null) return true;
            if (_quadroPlanos != Time.frameCount || _camPlanos != cam)
            {
                GeometryUtility.CalculateFrustumPlanes(cam, _planos);
                _quadroPlanos = Time.frameCount;
                _camPlanos = cam;
            }
            var b = new Bounds(transform.position + new Vector3(0f, 1.2f, 0f), new Vector3(2f, 3f, 2f));
            return GeometryUtility.TestPlanesAABB(_planos, b);
        }

        /// <summary>Escolhe o nível pelo tamanho na tela (a altura do bichinho dividida pela da
        /// tela), com folga para não ficar trocando. O de perto só com o meio dele dentro da tela.</summary>
        void EscolherNivel()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var pe = transform.position;
            var a = cam.WorldToScreenPoint(pe);
            var b = cam.WorldToScreenPoint(pe + new Vector3(0f, ALTURA_BICHO, 0f));
            if (a.z <= 0f || b.z <= 0f) return;
            float tela = Mathf.Max(Mathf.Max(cam.pixelWidth, cam.pixelHeight), 1f);
            _fracao = Vector2.Distance(a, b) / tela;
            var meio = (a + b) * 0.5f;
            _dentro = meio.x > 0f && meio.y > 0f && meio.x < cam.pixelWidth && meio.y < cam.pixelHeight;
            string novo = "meio";
            if (podePerto) novo = "perto";
            else if (_fracao < NIVEL_LONGE || (_nivel == "longe" && _fracao < NIVEL_LONGE_VOLTA)) novo = "longe";
            if (novo != _nivel)
            {
                _nivel = novo;
                VestirNivel();
            }
        }

        /// <summary>O jogo deixa (ou não) vestir o de perto. Tirando, volta ao do meio na hora.</summary>
        public void PermitirPerto(bool sim)
        {
            if (sim == podePerto) return;
            podePerto = sim;
            if (!sim && _nivel == "perto")
            {
                _nivel = "meio";
                VestirNivel();
            }
        }

        /// <summary>Pode ganhar o de perto: grande na tela e inteiro dentro dela.</summary>
        public bool CandidatoPerto()
        {
            if (!niveis || !Visivel || _malhasNivel["perto"].Count == 0) return false;
            return _dentro && _fracao > (podePerto ? NIVEL_PERTO_VOLTA : NIVEL_PERTO);
        }

        /// <summary>Quantos índices o de perto desenha a mais que o do meio (com o contorno: x2).</summary>
        public int CustoPerto => _custoPerto;

        public float TamanhoNaTela => _fracao;

        public string Nivel => _nivel;

        static int Indices(Dictionary<string, Mesh> malhas)
        {
            int t = 0;
            foreach (var m in malhas.Values)
                if (m != null) t += (int)m.GetIndexCount(0);
            return t;
        }

        /// <summary>Põe em cada peça a malha do nível atual (a do meio onde o nível não tem).</summary>
        void VestirNivel()
        {
            var meio = _malhasNivel["meio"];
            var perto = _malhasNivel["perto"];
            if (perto.Count > 0) _custoPerto = (_matContorno != null ? 2 : 1) * (Indices(perto) - Indices(meio));
            var outro = _malhasNivel[_nivel];
            foreach (var j in PECAS)
            {
                if (!_mf.TryGetValue(j, out var mf)) continue;
                Mesh m = null;
                if (!outro.TryGetValue(j, out m) || m == null) meio.TryGetValue(j, out m);
                mf.sharedMesh = m;
                _mr[j].enabled = m != null;
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
        static readonly Dictionary<string, float> ALEGRIA = new Dictionary<string, float>
        {
            ["empolgado"] = 1f, ["risada"] = 1f, ["feliz"] = 0.7f, ["piscadinha"] = 0.8f, ["triste"] = 0f, ["concentrado"] = 0.25f, ["surpreso"] = 0.5f,
        };
        static readonly int ID_CELULA = Shader.PropertyToID("_rosto_celula");

        public string ExpressaoAtual => _expressao;

        public void Expressao(string nome)
        {
            _expressao = nome;
            DefinirCelula(nome);
            _alegria = ALEGRIA.TryGetValue(nome, out var a) ? a : 0.5f;
        }

        /// <summary>A célula da expressão na arte do rosto (4 colunas x 2 linhas).</summary>
        void DefinirCelula(string nome)
        {
            int i = Array.IndexOf(EXPRESSOES, nome);
            if (i < 0) i = 0;
            _matCabeca.SetVector(ID_CELULA, new Vector4(i % 4, i / 4, 0f, 0f));
        }

        void Rosto(float dt)
        {
            if (_piscando > 0f)
            {
                _piscando -= dt;
                if (_piscando <= 0f) DefinirCelula(_expressao);
                return;
            }
            _piscarEm -= dt;
            if (_piscarEm <= 0f)
            {
                _piscarEm = UnityEngine.Random.Range(2f, 4.5f);
                if (_expressao == "feliz" || _expressao == "concentrado" || _expressao == "surpreso" || _expressao == "empolgado")
                {
                    _piscando = 0.13f;
                    DefinirCelula("piscando");
                }
            }
        }

        /// <summary>A estampa da roupa (estrela do Bob, bolinhas da Mia, listras do pirata),
        /// desenhada no shader com os dados que o modelador gravou.</summary>
        void AplicarEstampa(Dictionary<string, object> e)
        {
            var m = _matCorpo;
            if (e == null || e.Count == 0)
            {
                m.SetFloat("_estampa", 0f);
                return;
            }
            m.SetVector("_estampa_base", Materiais.Hex(Json.Str(e["base"])));
            m.SetVector("_estampa_cor", Materiais.Hex(Json.Str(e["cor"])));
            m.SetFloat("_estampa_raio", (float)Json.Num(e["raio"]));
            string tipo = Json.Str(e["tipo"]);
            if (tipo == "estrela")
            {
                m.SetFloat("_estampa", 1f);
                m.SetVector("_estampa_centro", V(e["centro"]));
                m.SetFloat("_estampa_dentro", (float)Json.Num(e["dentro"]));
            }
            else if (tipo == "listras")
            {
                // listras de marinheiro (pirata): faixas de 2 x raio a cada "periodo"
                m.SetFloat("_estampa", 3f);
                m.SetFloat("_bolinhas_y0", (float)Json.Num(e["y0"]));
                m.SetFloat("_bolinhas_dy", (float)Json.Num(e["periodo"]));
                m.SetFloat("_bolinhas_y_max", (float)Json.Num(e["y_max"]));
            }
            else
            {
                m.SetFloat("_estampa", 2f);
                m.SetFloat("_bolinhas_y0", (float)Json.Num(e["y0"]));
                m.SetFloat("_bolinhas_dy", (float)Json.Num(e["dy"]));
                m.SetFloat("_bolinhas_fileiras", Json.Int(e["fileiras"]));
                m.SetFloat("_bolinhas_n0", Json.Int(e["n0"]));
                m.SetFloat("_bolinhas_y_max", (float)Json.Num(e["y_max"]));
            }
        }

        // ------------------------------------------------------------ festa
        /// <summary>Estrelinhas subindo em volta (comemorações).</summary>
        public void Jatos(bool ligar) => _festa.emitindo = ligar;

        // ------------------------------------------------------------ roupinhas das fases
        string Pacote(string r) => r == "" ? nomeModelo : nomeModelo + "_" + r;

        IEnumerable<string> Pacotes(string r)
        {
            yield return Pacote(r);
            if (!niveis) yield break;
            yield return Pacote(r) + "_hd";
            yield return Pacote(r) + "_longe";
        }

        /// <summary>Carrega uma roupinha antes de usar (numa hora calma: a troca fica instantânea).</summary>
        public void PedirRoupa(string r)
        {
            if (string.IsNullOrEmpty(r) || r == roupa || _pedidos.Contains(r)) return;
            if (!Malhas.Existe(Pacote(r))) return;
            foreach (var pc in Pacotes(r))
                if (Malhas.Existe(pc)) Malhas.Ler(pc);
            _pedidos.Add(r);
        }

        /// <summary>Roupinhas pedidas que não vão mais ser usadas: saem da memória.</summary>
        public void SoltarPedidos()
        {
            foreach (var r in _pedidos)
                if (r != roupa)
                    foreach (var pc in Pacotes(r)) Malhas.Soltar(pc);
            _pedidos.Clear();
        }

        /// <summary>Veste a roupinha `r` ("" = a de sempre). A roupinha tem as MESMAS juntas:
        /// só a malha de cada junta troca — animações, pernas, rosto e piscadas valem igual.</summary>
        public void TrocarRoupa(string r)
        {
            if (r == null) r = "";
            if (r == roupa) return;
            if (r != "" && !Malhas.Existe(Pacote(r))) r = "";
            if (r == roupa) return;
            string antiga = roupa;
            roupa = r;
            _malhasNivel["meio"] = MalhasDe(Pacote(r));
            _malhasNivel["perto"] = niveis ? MalhasDe(Pacote(r) + "_hd") : new Dictionary<string, Mesh>();
            _malhasNivel["longe"] = niveis ? MalhasDe(Pacote(r) + "_longe") : new Dictionary<string, Mesh>();
            VestirNivel();
            AplicarEstampa(Json.Dic(Json.Pegar(Meta(Pacote(r)) ?? new Dictionary<string, object>(), "estampa")));
            // a hélice do boné (filha da cabeça): gira sozinha
            var ph = Malhas.Ler(Pacote(r)).Achar("helice");
            if (ph != null && ph.malha != null)
            {
                if (_helice == null)
                {
                    var go = Malhas.Objeto("helice", ph.malha, _matRabo, juntas["cabeca"]);
                    go.GetComponent<MeshRenderer>().sharedMaterials = MateriaisDa("helice");
                    _helice = go.transform;
                }
                _helice.GetComponent<MeshFilter>().sharedMesh = ph.malha;
                _helice.localPosition = ph.pos;
                _helice.localRotation = Quaternion.identity;
                _helice.gameObject.SetActive(true);
            }
            else if (_helice != null) _helice.gameObject.SetActive(false);
            _pedidos.Remove(r);
            if (antiga != "")
                foreach (var pc in Pacotes(antiga)) Malhas.Soltar(pc);
        }

        // ------------------------------------------------------------ utilidades
        public void SegurarBola(bool sim) => _bolaMao.gameObject.SetActive(sim);

        public bool SegurandoBola => _bolaMao.gameObject.activeSelf;

        /// <summary>Coloca o personagem num lugar, olhando para `direcaoY` (graus, Unity).</summary>
        public void Posicionar(Vector3 pos, float direcaoY)
        {
            _forcar = 3;
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
            foreach (var m in _mats) m.SetFloat(Materiais.ID_FLASH_G, valor);
        }

        /// <summary>Onde a mão direita fica (para a câmera mirar).</summary>
        public Vector3 PosicaoMao() => _bolaMao.position;
    }
}
