using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// O MUNDO 3D: céu, terreno com morros, o PALCO (a pista, as canaletas, os
    /// para-choques, o fundo dos pinos com a cortina, o letreiro com lâmpadas e
    /// o arco de raios, o gabinete, a área de quem joga, os pódios, o símbolo
    /// de cada fase e o sol, a lua e a baleia do céu) e os 4 CENÁRIOS (Temas).
    ///
    /// Linha de falta em z = 0; os pinos em z = -10,5; a pista vai para -Z.
    /// O palco é o da edição Godot 7.6, peça por peça (Palco.cs,
    /// tools/godot/exportar_palco.gd), com as mesmas animações do mundo.gd; a
    /// física (colisores) é montada aqui com as mesmas medidas.
    /// </summary>
    public class Mundo : MonoBehaviour
    {
        public static Mundo I { get; private set; }

        public string tema = "";
        public bool leve;
        public Temas temas;
        public Transform bola;          // a bola do jogo (as tiras da caçapa abrem quando ela passa)
        public float bolaZ = 100f;      // distância da bola à linha de falta (o brilho da pista)

        /// <summary>O palco da edição Godot (as peças, os materiais e as cores de cada fase).</summary>
        public Palco palco;
        Material _matPista, _matPiso, _matCeu, _matAstro, _matLampadas, _matFundo, _matPortal, _matCortina, _matAsasMarca;
        readonly List<Material> _matsCanaleta = new List<Material>();
        readonly Dictionary<string, Texture2D> _fundos = new Dictionary<string, Texture2D>();
        readonly List<GameObject> _bumpers = new List<GameObject>();
        readonly List<Collider> _colBumpers = new List<Collider>();
        readonly float[] _aberturaTiras = new float[Medidas.N_TIRAS];
        bool _tirasPostas;
        GameObject _bolaVolta;
        Coroutine _retorno;
        readonly List<Transform> _podios = new List<Transform>();
        readonly List<Material> _matsPodio = new List<Material>();
        int _podioDestaque = -1;
        readonly Dictionary<string, Transform> _marcas = new Dictionary<string, Transform>();
        readonly Dictionary<string, Vector3> _escalaMarca = new Dictionary<string, Vector3>();
        readonly List<Transform> _bolhinhas = new List<Transform>();
        Transform _pteroMarca, _vooMarca;
        public Transform balao;
        float _t, _luz, _luzAlvo;
        string _astro = "sol";
        float _canto;
        Transform _fisica;

        static readonly int ID_LUZ = Shader.PropertyToID("_luz");
        static readonly int ID_FASE = Shader.PropertyToID("_fase");
        static readonly int ID_FASE_LENTA = Shader.PropertyToID("_fase_lenta");
        static readonly int ID_PULSO = Shader.PropertyToID("_pulso");
        static readonly int ID_BOLA_Z = Shader.PropertyToID("_bola_z");
        static readonly int ID_MODO = Shader.PropertyToID("_modo");
        static readonly int ID_ONDA1 = Shader.PropertyToID("_onda1");
        static readonly int ID_ONDA2 = Shader.PropertyToID("_onda2");
        static readonly int ID_BOLHAS = Shader.PropertyToID("_bolhas");
        static readonly int ID_EMISSAO = Shader.PropertyToID("_emissao");
        static readonly int ID_FASE_CENARIO = Shader.PropertyToID("_Fase");
        static readonly int ID_MODO_CENARIO = Shader.PropertyToID("_Modo");
        static readonly int[] ID_TIRA = new int[Medidas.N_TIRAS];

        /// <summary>a baleia: quanto vai para cada lado (em tamanhos dela) e o tempo da volta (s)</summary>
        const float BALEIA_ALCANCE = 1.6f;
        const float BALEIA_VOLTA = 46f;
        /// <summary>as batidas das asas do pterossauro do símbolo (rad/s)</summary>
        const float ASAS_MARCA = 6.2f;
        /// <summary>Para onde está o sol (direção da luz) — o mesmo das edições Godot.</summary>
        public static readonly Vector3 DIR_SOL = Medidas.G(0.289f, 0.788f, 0.544f).normalized;
        /// <summary>A madeira da pista vista no reflexo da bola e dos pinos.</summary>
        static readonly Color PISO_REFLETIDO = new Color(0.42f, 0.26f, 0.14f);

        public static Mundo Criar(Transform pai)
        {
            var go = new GameObject("Mundo");
            go.transform.SetParent(pai, false);
            I = go.AddComponent<Mundo>();
            return I;
        }

        // ================================================================ montagem
        public void Construir(bool cenarioLeve)
        {
            leve = cenarioLeve;
            _fisica = new GameObject("fisica").transform;
            _fisica.SetParent(transform, false);
            Ceu();
            Estrutura();
            Fisica();
            var tg = new GameObject("Temas");
            tg.transform.SetParent(transform, false);
            temas = tg.AddComponent<Temas>();
            temas.Construir(this, leve, Ajustes.Detalhes(), Ajustes.Cenario3dLeve());
            // o pterossauro do símbolo da selva: o mesmo material dos do cenário (as asas
            // batem no shader); no nível leve, a malha do meio
            if (_pteroMarca != null)
            {
                var mr = _pteroMarca.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    _matAsasMarca = temas.MatAsas("dinos");
                    mr.sharedMaterial = _matAsasMarca;
                    // (o governador do quadro pode mudar o nível no meio do jogo)
                    var mf = mr.GetComponent<MeshFilter>();
                    var alto = mf.sharedMesh;
                    var pm = Malhas.Ler("cenario/ptero_meio").partes;
                    var meio = pm.Count > 0 ? pm[0].malha : null;
                    void Vestir(bool l) => mf.sharedMesh = l && meio != null ? meio : alto;
                    Vestir(temas.cenarioLeve);
                    temas.NivelMudou += Vestir;
                }
            }
            AplicarTema(Temas.ORDEM[0]);
            DefinirProtecao(Ajustes.ProtecaoCanaleta());
        }

        /// <summary>Só a física (a calibração das jogadas, no editor): as mesmas paredes, canaletas,
        /// pisos e para-choques do jogo, sem nada para desenhar.</summary>
        public void ConstruirFisica(bool protecao)
        {
            _fisica = new GameObject("fisica").transform;
            _fisica.SetParent(transform, false);
            Fisica();
            DefinirProtecao(protecao);
        }

        /// <summary>O PALCO da edição Godot (Palco.cs) e os materiais que mexem a cada quadro.</summary>
        void Estrutura()
        {
            for (int i = 0; i < ID_TIRA.Length; i++) ID_TIRA[i] = Shader.PropertyToID("_a" + i);
            Palco.LuzPadrao();
            palco = Palco.Montar(transform);
            _matPista = palco.Mat("mat_pista");
            // a pista vem antes do resto: o reflexo dos pinos e da bola (Reflexos.cs, fila
            // 1995) só pinta onde fica embaixo do verniz; o resto da cena vem depois e cobre
            if (_matPista != null) _matPista.renderQueue = Reflexos.FILA_PISTA;
            _matPiso = palco.Mat("mat_piso");
            for (int i = 0; i < 2; i++)
            {
                var mc = palco.Mat("mats_canaleta[" + i + "]");
                if (mc != null) _matsCanaleta.Add(mc);
            }
            _matLampadas = palco.Mat("mat_lampadas");
            _matFundo = palco.Mat("mat_fundo");
            _matPortal = palco.Mat("mat_portal");
            _matCortina = palco.Mat("mat_cortina");
            _matAstro = palco.Mat("_mat_astro");
            for (int i = 0; i < 2; i++)
            {
                var b = palco.No("_bumpers[" + i + "]");
                if (b != null) _bumpers.Add(b.gameObject);
                var g = palco.No("_podios[" + i + "]");
                if (g != null) _podios.Add(g);
                _matsPodio.Add(palco.Mat("_mats_podio[" + i + "]"));
            }
            // o símbolo Lazer & Sport de cada fase (o balão, o planetinha, a bolha, o pterossauro)
            string[,] marcas = { { "parque", "balao_marca" }, { "espaco", "planeta_marca" }, { "mar", "bolha_marca" }, { "dinos", "ptero_marca" } };
            for (int i = 0; i < marcas.GetLength(0); i++)
            {
                var m = palco.No(marcas[i, 1]);
                if (m == null) continue;
                _marcas[marcas[i, 0]] = m;
                _escalaMarca[marcas[i, 0]] = m.localScale;
            }
            for (int i = 0; i < 4; i++)
            {
                var b = palco.No("_bolhinhas[" + i + "]");
                if (b != null) _bolhinhas.Add(b);
            }
            if (_marcas.TryGetValue("dinos", out var pm)) _vooMarca = pm.Find("voo");
            _pteroMarca = palco.No("_ptero_simbolo");
            balao = _marcas.TryGetValue("parque", out var bp) ? bp : null;
        }

        // ================================================================ terreno
        /// <summary>metros de um ladrilho da textura de detalhes do chão de cada modo
        /// (Texturas/cenario/chao_&lt;modo&gt;)</summary>
        static readonly float[] LADRILHO_CHAO = { 25.2f, 22.4f, 25.3f, 25.0f };
        Material _matTerreno;
        readonly Texture2D[] _texturasChao = new Texture2D[4];

        /// <summary>O CHÃO DOS CENÁRIOS: o relevo da edição Godot (Malhas/terreno: os morros e os
        /// PATAMARES das peças grandes, Temas.AlturaTerreno) e o desenho de cada fase no shader
        /// (Terreno.shader). As 4 texturas de detalhe já carregadas (trocar de fase não lê do disco).</summary>
        void Terreno()
        {
            _matTerreno = Materiais.Novo("Terreno", "terreno");
            for (int i = 0; i < _texturasChao.Length; i++)
            {
                _texturasChao[i] = Resources.Load<Texture2D>("Texturas/cenario/chao_" + i);
                if (_texturasChao[i] != null) _texturasChao[i].wrapMode = TextureWrapMode.Repeat;
            }
            var pc = Malhas.Ler("terreno");
            foreach (var p in pc.partes)
                if (p.malha != null) Malhas.Objeto("terreno", p.malha, _matTerreno, transform);
        }

        // ================================================================ céu e sol
        void Ceu()
        {
            _matCeu = Materiais.Novo("Ceu", "ceu");
            _matCeu.SetTexture(Materiais.ID_TEX, Materiais.Textura("estrelas_ceu"));
            // os fundos pintados das 4 fases já carregados (trocar de fase não lê do disco)
            foreach (var f in Temas.ORDEM)
            {
                var t = Resources.Load<Texture2D>("Texturas/cenario/fundo_" + f);
                if (t != null) _fundos[f] = t;
            }
            var m = new Mesh { name = "ceu" };
            // (o degradê é por pixel; por vértice só a nebulosa e os raios do mar, que
            // mudam devagar: 48 x 24 basta)
            int gomos = 48, aneis = 24;
            var V = new List<Vector3>();
            var U = new List<Vector2>();
            var T = new List<int>();
            for (int j = 0; j <= aneis; j++)
            {
                float b = Mathf.PI * j / aneis;
                for (int i = 0; i <= gomos; i++)
                {
                    float a = 2f * Mathf.PI * i / gomos;
                    V.Add(new Vector3(Mathf.Cos(a) * Mathf.Sin(b), Mathf.Cos(b), Mathf.Sin(a) * Mathf.Sin(b)));
                    U.Add(new Vector2((float)i / gomos * 2f, 1f - (float)j / aneis));
                }
            }
            for (int j = 0; j < aneis; j++)
                for (int i = 0; i < gomos; i++)
                {
                    int a = j * (gomos + 1) + i, b = a + 1, c = a + gomos + 1, d = c + 1;
                    T.Add(a); T.Add(b); T.Add(c);
                    T.Add(b); T.Add(d); T.Add(c);
                }
            m.SetVertices(V);
            m.SetUVs(0, U);
            m.SetTriangles(T, 0);
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            var go = Malhas.Objeto("ceu", m, _matCeu, transform);
            go.GetComponent<MeshRenderer>().sortingOrder = -100;
        }

        // ================================================================ física
        BoxCollider Caixa(Vector3 tam, Vector3 centro, float atrito, float quique, string quem = "todos", int camada = Medidas.CAMADA_MUNDO, string nome = "caixa")
        {
            var go = new GameObject(nome);
            go.layer = camada;
            go.transform.SetParent(_fisica, false);
            go.transform.localPosition = centro;
            var b = go.AddComponent<BoxCollider>();
            b.size = tam;
            b.sharedMaterial = Medidas.Material(atrito, quique, quem);
            return b;
        }

        /// <summary>A meia-cana da canaleta (o mesmo perfil do modelador), puxada inteira de z0 a z1
        /// (sem emendas no caminho: a bola desliza sem tranco).</summary>
        static Mesh MalhaCanaleta(float z0, float z1)
        {
            float r = Medidas.CANALETA / 2f, borda = 0.018f, fundo = 0.05f;
            int segs = 14;
            var p = new List<Vector2>();
            float yc = -borda, xe = r + 2f * borda;
            p.Add(new Vector2(xe, yc - r - fundo));
            for (int i = 0; i < 5; i++) { float a = Mathf.PI * i / 4f; p.Add(new Vector2(r + borda + Mathf.Cos(a) * borda, yc + Mathf.Sin(a) * borda)); }
            for (int i = 1; i < segs; i++) { float a = -Mathf.PI * i / segs; p.Add(new Vector2(Mathf.Cos(a) * r, yc + Mathf.Sin(a) * r)); }
            for (int i = 0; i < 5; i++) { float a = Mathf.PI * i / 4f; p.Add(new Vector2(-r - borda + Mathf.Cos(a) * borda, yc + Mathf.Sin(a) * borda)); }
            p.Add(new Vector2(-xe, yc - r - fundo));
            var V = new List<Vector3>();
            var T = new List<int>();
            int n = p.Count;
            for (int i = 0; i < n; i++)
            {
                V.Add(new Vector3(p[i].x, p[i].y, z0));
                V.Add(new Vector3(p[i].x, p[i].y, z1));
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                int a = 2 * i, b = 2 * j, c = 2 * i + 1, d = 2 * j + 1;
                // os dois lados (a PhysX só colide com a frente de cada triângulo de malha parada)
                T.Add(a); T.Add(c); T.Add(b); T.Add(b); T.Add(c); T.Add(d);
                T.Add(a); T.Add(b); T.Add(c); T.Add(b); T.Add(d); T.Add(c);
            }
            var m = new Mesh { name = "canaleta_colisao" };
            m.SetVertices(V);
            m.SetTriangles(T, 0);
            m.RecalculateBounds();
            return m;
        }

        void Fisica()
        {
            const float ML = Medidas.MEIA_LARGURA;
            const float Z_FIM = Medidas.Z_FIM_DECK;
            const float Z_FUNDO = Medidas.Z_FUNDO;
            // chão de segurança (nada cai para sempre)
            Caixa(new Vector3(400f, 1f, 400f), new Vector3(0, Medidas.POCO_FUNDO - 0.85f - 0.5f, 0), 0.8f, 0.1f, "todos", Medidas.CAMADA_MUNDO, "seguranca");
            // a pista: duas peças INTEIRAS (sem emendas): a da bola é lisa (óleo);
            // a dos pinos tem atrito de madeira seca. Cada uma só existe para quem pisa.
            float cb = 1.2f - Z_FIM;
            Caixa(new Vector3(ML * 2f, 0.2f, cb), new Vector3(0, -0.1f, 1.2f - cb / 2f), 0.05f, 0.05f, "bola", Medidas.CAMADA_SO_BOLA, "piso_bola");
            Caixa(new Vector3(ML * 2f, 0.2f, cb), new Vector3(0, -0.1f, 1.2f - cb / 2f), 0.42f, 0.1f, "pinos", Medidas.CAMADA_SO_PINOS, "piso_pinos");
            // área de aproximação (onde o personagem corre)
            Caixa(new Vector3(6.6f, 0.2f, 6.2f), new Vector3(0, -0.101f, 3.2f), 0.8f, 0f, "bola", Medidas.CAMADA_MUNDO, "aproximacao");
            // canaletas: a mesma meia-cana do desenho (bola e pinos rolam e param dentro dela)
            var mc = MalhaCanaleta(Z_FIM, 0.1f);
            foreach (float s in new[] { -1f, 1f })
            {
                var go = new GameObject("canaleta");
                go.layer = Medidas.CAMADA_MUNDO;
                go.transform.SetParent(_fisica, false);
                go.transform.localPosition = new Vector3(s * (ML + Medidas.CANALETA / 2f), 0, 0);
                var c = go.AddComponent<MeshCollider>();
                c.sharedMesh = mc;
                c.sharedMaterial = Medidas.Material(0.2f, 0.1f);
                // divisória (capping) entre a canaleta e o gramado
                float xc = s * (ML + Medidas.CANALETA + 0.036f + 0.16f);
                Caixa(new Vector3(0.32f, 0.34f, 0.1f - (Medidas.Z_PINO1 + 0.2f)), new Vector3(xc, -0.07f, (Medidas.Z_PINO1 + 0.2f + 0.1f) / 2f), 0.3f, 0.3f);
                // para-choques (só quando a proteção está ligada)
                var bgo = new GameObject("bumper_fisica");
                bgo.layer = Medidas.CAMADA_MUNDO;
                bgo.transform.SetParent(_fisica, false);
                bgo.transform.localPosition = new Vector3(s * 0.735f, 0.1f, -0.6f - (Medidas.N_BUMPERS - 1) * 0.96f / 2f);
                var cap = bgo.AddComponent<CapsuleCollider>();
                cap.direction = 2;
                cap.radius = 0.075f;
                cap.height = Medidas.N_BUMPERS * 0.96f + 0.2f;
                cap.sharedMaterial = Medidas.Material(0.1f, 0.55f, "bola");
                _colBumpers.Add(cap);
                // paredes da caçapa (kickbacks) e o friso de cima
                float x = s * (ML + Medidas.CANALETA + 0.06f);
                float zf = Medidas.Z_PINO1 + 0.9f;
                Caixa(new Vector3(0.12f, 1.0f, zf - Z_FUNDO), new Vector3(x, 0.35f, (zf + Z_FUNDO) / 2f), 0.3f, 0.35f);
                Caixa(new Vector3(0.17f, 0.07f, zf + 0.02f - Z_FUNDO), new Vector3(x, 0.875f, (zf + 0.02f + Z_FUNDO) / 2f), 0.3f, 0.35f);
                // gabinete por fora (um pino que voa por cima da parede bate nele)
                float zt = Z_FUNDO - 0.95f + 0.3f, zm = Medidas.Z_PINO1 + 0.68f;
                Caixa(new Vector3(0.34f, 3.7f, zm - zt), new Vector3(s * 1.23f, 1.55f, (zm + zt) / 2f), 0.4f, 0.3f, "pinos", Medidas.CAMADA_SO_PINOS);
            }
            // fosso: o chão em volta do alçapão (as portinhas são da máquina)
            float yf = Medidas.Y_CHAO_FOSSO - 0.1f;
            float meioZ = (Medidas.ALCAPAO_Z0 + Medidas.ALCAPAO_Z1) / 2f;
            float comp = Medidas.ALCAPAO_Z0 - Medidas.ALCAPAO_Z1;
            Caixa(new Vector3(2f, 0.2f, Z_FIM - Medidas.ALCAPAO_Z0), new Vector3(0, yf, (Z_FIM + Medidas.ALCAPAO_Z0) / 2f), 0.9f, 0.05f);
            Caixa(new Vector3(2f, 0.2f, Medidas.ALCAPAO_Z1 - Z_FUNDO), new Vector3(0, yf, (Medidas.ALCAPAO_Z1 + Z_FUNDO) / 2f), 0.9f, 0.05f);
            foreach (float s in new[] { -1f, 1f })
                Caixa(new Vector3(1f - Medidas.ALCAPAO_X, 0.2f, comp), new Vector3(s * (1f + Medidas.ALCAPAO_X) / 2f, yf, meioZ), 0.9f, 0.05f);
            // o POÇO embaixo do alçapão (só os pinos caem nele)
            float px = Medidas.ALCAPAO_X - 0.003f, z0 = Medidas.ALCAPAO_Z0 - 0.003f, z1 = Medidas.ALCAPAO_Z1 + 0.003f;
            float alto = (Medidas.Y_CHAO_FOSSO - 0.04f) - Medidas.POCO_FUNDO;
            float yc = (Medidas.Y_CHAO_FOSSO - 0.04f + Medidas.POCO_FUNDO) / 2f;
            foreach (float s in new[] { -1f, 1f })
                Caixa(new Vector3(0.1f, alto, z0 - z1 + 0.2f), new Vector3(s * (px + 0.05f), yc, (z0 + z1) / 2f), 0.5f, 0.25f, "pinos");
            Caixa(new Vector3(px * 2f + 0.2f, alto, 0.1f), new Vector3(0, yc, z0 + 0.05f), 0.5f, 0.25f, "pinos");
            Caixa(new Vector3(px * 2f + 0.2f, alto, 0.1f), new Vector3(0, yc, z1 - 0.05f), 0.5f, 0.25f, "pinos");
            Caixa(new Vector3(px * 2f + 0.2f, 0.2f, z0 - z1 + 0.2f), new Vector3(0, Medidas.POCO_FUNDO - 0.1f, (z0 + z1) / 2f), 0.6f, 0.3f, "pinos");
            // parede de trás: em cima vale para tudo; embaixo (a boca) só para os pinos
            Caixa(new Vector3(2.4f, 1.6f, 0.2f), new Vector3(0, Medidas.Y_BOCA + 0.8f, Z_FUNDO + 0.075f - 0.1f), 0.9f, 0f);
            Caixa(new Vector3(2.4f, 0.9f, 0.2f), new Vector3(0, Medidas.Y_BOCA - 0.45f, Z_FUNDO + 0.04f - 0.1f), 0.9f, 0f, "pinos", Medidas.CAMADA_SO_PINOS);
            // a caçapa das bolas: túnel (chão, paredes, fundo) e a moldura da boca
            float meia = Medidas.LARGURA_BOCA / 2f;
            float altoB = Medidas.Y_BOCA - Medidas.Y_CHAO_FOSSO;
            Caixa(new Vector3(Medidas.LARGURA_BOCA + 0.2f, 0.2f, 1f), new Vector3(0, Medidas.Y_CHAO_FOSSO - 0.1f, Z_FUNDO - 0.5f), 0.3f, 0f, "bola");
            foreach (float s in new[] { -1f, 1f })
            {
                Caixa(new Vector3(0.1f, altoB + 0.1f, 1f), new Vector3(s * (meia + 0.05f), Medidas.Y_CHAO_FOSSO + altoB / 2f, Z_FUNDO - 0.5f), 0.4f, 0.2f, "bola");
                Caixa(new Vector3(0.13f, altoB + 0.12f, 0.14f), new Vector3(s * (meia + 0.065f), Medidas.Y_CHAO_FOSSO + (altoB + 0.12f) / 2f - 0.02f, Z_FUNDO + 0.03f), 0.5f, 0.2f);
            }
            Caixa(new Vector3(Medidas.LARGURA_BOCA + 0.2f, altoB + 0.2f, 0.1f), new Vector3(0, Medidas.Y_CHAO_FOSSO + altoB / 2f, Z_FUNDO - 1.05f), 0.4f, 0.2f, "bola");
            Caixa(new Vector3(Medidas.LARGURA_BOCA + 0.26f, 0.12f, 0.14f), new Vector3(0, Medidas.Y_BOCA + 0.06f, Z_FUNDO + 0.03f), 0.5f, 0.2f);
        }

        public void DefinirProtecao(bool ligada)
        {
            foreach (var g in _bumpers) g.SetActive(ligada);
            foreach (var c in _colBumpers) c.enabled = ligada;
        }

        // ================================================================ lâmpadas
        /// <summary>Uma fileira de lâmpadas numa malha só: uma bolinha facetada (octaedro) por
        /// ponto, com a cor de cada uma (amarela, laranja, azul, verde) e o número dela (UV.x, a
        /// partir de `fase0`) para o brilho andar (Lampadas.shader).</summary>
        public static Mesh MalhaLampadas(IList<Vector3> pontos, int fase0, float r = 0.052f)
        {
            var cores = new[] { Materiais.Hex("#FFE14D"), Materiais.Hex("#FF9F2E"), Materiais.Hex("#4FD8FF"), Materiais.Hex("#8DFF6A") };
            var bolinha = new[] { new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1), new Vector3(-1, 0, 0), new Vector3(0, 0, -1), new Vector3(0, -1, 0) };
            int[] tris = { 0, 2, 1, 0, 3, 2, 0, 4, 3, 0, 1, 4, 5, 1, 2, 5, 2, 3, 5, 3, 4, 5, 4, 1 };
            var V = new List<Vector3>();
            var C = new List<Color32>();
            var U = new List<Vector2>();
            var T = new List<int>();
            for (int i = 0; i < pontos.Count; i++)
            {
                var c = pontos[i];
                int b = V.Count;
                for (int k = 0; k < bolinha.Length; k++)
                {
                    V.Add(c + bolinha[k] * r);
                    C.Add(cores[i % cores.Length]);
                    U.Add(new Vector2(i + fase0, bolinha[k].z > 0.5f ? 1f : 0.35f));
                }
                for (int k = 0; k < tris.Length; k++) T.Add(b + tris[k]);
            }
            var m = new Mesh { name = "lampadas" };
            m.SetVertices(V);
            m.SetColors(C);
            m.SetUVs(0, U);
            m.SetTriangles(T, 0);
            m.RecalculateBounds();
            return m;
        }

        void MostrarMarca(string nome)
        {
            foreach (var k in _marcas) k.Value.gameObject.SetActive(k.Key == nome);
            balao = _marcas.TryGetValue(nome, out var b) ? b : null;
        }

        // ================================================================ pódios e retorno de bolas
        /// <summary>Liga/desliga os pódios (posições no chão) e acende o do escolhido.</summary>
        public void Podios(bool ligar, Vector3[] posicoes = null, int destaque = -1)
        {
            for (int i = 0; i < _podios.Count; i++)
            {
                var g = _podios[i];
                bool antes = g.gameObject.activeSelf;
                bool agora = ligar && posicoes != null && i < posicoes.Length;
                g.gameObject.SetActive(agora);
                if (agora)
                {
                    g.localPosition = posicoes[i];
                    if (!antes) StartCoroutine(CrescerPodio(g));
                }
            }
            _podioDestaque = destaque;
        }

        IEnumerator CrescerPodio(Transform g)
        {
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / 0.35f);
                // "back out": passa um tiquinho e volta
                float c = 1.70158f;
                float e = 1f + (c + 1f) * Mathf.Pow(u - 1f, 3f) + c * Mathf.Pow(u - 1f, 2f);
                g.localScale = new Vector3(1f, Mathf.LerpUnclamped(0.2f, 1f, e), 1f);
                yield return null;
            }
            g.localScale = Vector3.one;
        }

        /// <summary>A bola que caiu na caçapa sai rolando de baixo da capota do retorno de bolas.</summary>
        public void RetornoBola(Material mat)
        {
            if (_bolaVolta == null) _bolaVolta = Malhas.Objeto("bola_volta", Bola.Malha, mat, transform);
            _bolaVolta.GetComponent<MeshRenderer>().sharedMaterial = mat;
            _bolaVolta.SetActive(true);
            if (_retorno != null) StopCoroutine(_retorno);
            _retorno = StartCoroutine(RolarRetorno());
        }

        IEnumerator RolarRetorno()
        {
            var t = _bolaVolta.transform;
            var r = Medidas.RETORNO;
            t.localPosition = r + new Vector3(0, 0.69f, 0.62f);
            t.localRotation = Quaternion.identity;
            float dur = 0.55f, s = 0f;
            while (s < dur)
            {
                s += Time.deltaTime;
                float u = Mathf.Clamp01(s / dur);
                float e = 1f - (1f - u) * (1f - u);
                t.localPosition = new Vector3(r.x, 0.69f, Mathf.Lerp(r.z + 0.62f, r.z + 0.26f, e));
                t.localRotation = Quaternion.Euler(-4f * Mathf.Rad2Deg * e, 0, 0);
                yield return null;
            }
            float[] zs = { r.z + 0.29f, r.z + 0.26f };
            float[] ds = { 0.08f, 0.1f };
            float z0 = r.z + 0.26f;
            for (int k = 0; k < 2; k++)
            {
                s = 0f;
                while (s < ds[k])
                {
                    s += Time.deltaTime;
                    t.localPosition = new Vector3(r.x, 0.69f, Mathf.Lerp(z0, zs[k], Mathf.Clamp01(s / ds[k])));
                    yield return null;
                }
                z0 = zs[k];
            }
            _retorno = null;
        }

        /// <summary>Esconde a bola do retorno (ela vai para a mão do bichinho).</summary>
        public void GuardarRetorno()
        {
            if (_bolaVolta != null) _bolaVolta.SetActive(false);
        }

        // ================================================================ fases
        /// <summary>Troca o cenário: céu, luz, neblina, chão, cores da pista e decoração.</summary>
        public void AplicarTema(string nome)
        {
            if (!Temas.CONFIG.ContainsKey(nome)) nome = "parque";
            tema = nome;
            var c = Temas.CONFIG[nome];
            _matCeu.SetColor("_CorTopo", c.ceu[0]);
            _matCeu.SetColor("_CorMeio", c.ceu[1]);
            _matCeu.SetColor("_CorHorizonte", c.ceu[2]);
            _matCeu.SetColor("_CorChao", c.ceu[3]);
            _matCeu.SetColor("_CorSol", c.corSol);
            _matCeu.SetFloat("_ForcaSol", c.forcaSol);
            _matCeu.SetFloat("_Estrelas", c.estrelas);
            _matCeu.SetColor("_CorNebulosa", c.nebulosa);
            _matCeu.SetFloat("_Raios", c.raios);
            // o fundo pintado da fase (a paisagem lá longe), com os detalhes extras
            bool temFundo = _fundos.TryGetValue(nome, out var fundo);
            _matCeu.SetFloat("_FaixaLiga", temFundo && temas.detalhes ? 1f : 0f);
            if (temFundo) _matCeu.SetTexture("_Faixa", fundo);
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = c.ceu[2];
            // a luz: sol, céu e chão (ambiente), contraluz e neblina
            Color ceu = Materiais.Vezes(c.ambiente, c.ambienteForca * 1.15f);
            Color chao = Materiais.Vezes(Color.Lerp(c.ambiente, c.ceu[3], 0.35f), c.ambienteForca * 0.72f);
            Color sol = Materiais.Vezes(c.sol, c.solForca * 0.6f);
            Color borda = Materiais.Vezes(Color.Lerp(Color.white, c.ceu[1], 0.3f), 0.22f);
            float dens = leve ? c.neblinaDensidade * 0.6f : c.neblinaDensidade;
            Materiais.DefinirLuz(DIR_SOL, sol, ceu, chao, borda, c.neblina, dens);
            // o que as peças lustrosas (pinos, bola) refletem: o céu da fase, a pista, o neon
            Materiais.DefinirMatcap(c.ceu[0], c.ceu[2], PISO_REFLETIDO, c.neon);
            // A LUZ DO CENÁRIO (a decoração e o chão), a mesma da edição Godot: sol e ambiente
            // lineares, com a força de cada fase, e a neblina do fundo (por vértice: é ela
            // que deixa o mar verde-água ao longe)
            Color amb = Materiais.Vezes(c.ambiente.linear, c.ambienteForcaCenario);
            Color solLin = Materiais.Vezes(c.sol.linear, c.solForcaCenario);
            Color nb = c.neblina.linear;
            var solCor = new Vector3(solLin.r, solLin.g, solLin.b);
            var ambV = new Vector3(amb.r, amb.g, amb.b);
            var neblinaV = new Vector3(nb.r, nb.g, nb.b);
            temas.Luz(DIR_SOL, solCor, ambV, neblinaV, c.neblinaDensidadeCenario);
            // o chão da fase: o desenho (modo), os detalhes que se repetem e as cores (lineares)
            var mt = _matTerreno;
            if (mt != null)
            {
                mt.SetVector("_SolCor", solCor);
                mt.SetVector("_Ambiente", ambV);
                mt.SetVector("_NeblinaCor", neblinaV);
                mt.SetFloat("_NeblinaDensidade", c.neblinaDensidadeCenario);
                int modo = Mathf.Clamp(c.chaoModo, 0, 3);
                mt.SetFloat("_Modo", modo);
                if (_texturasChao[modo] != null) mt.SetTexture("_Detalhe", _texturasChao[modo]);
                mt.SetFloat("_DetalheTile", LADRILHO_CHAO[modo]);
                string[] cores = { "_LinA", "_LinB", "_LinC", "_LinD" };
                for (int i = 0; i < 4; i++)
                {
                    var l = c.chao[i].linear;
                    mt.SetVector(cores[i], new Vector4(l.r, l.g, l.b, 0f));
                }
                // a terra da selva
                mt.SetVector("_Terra", new Vector4(0.45f, 0.32f, 0.17f, 0f));
            }
            // O PALCO: as cores da fase (pista, canaletas, para-choques, gabinete, cortina,
            // fundo dos pinos, sol/lua/baleia), as peças que aparecem e a luz de palco
            // (Materiais.definir_luz da edição Godot: pinos, bichinhos, bola, gabinete)
            palco.AplicarFase(nome);
            if (_matAstro != null)
            {
                // a baleia deixa o astro andado e virado: o sol e a lua voltam ao lugar
                // (_mostrar_astro da edição Godot; a fase não traz esses dois)
                _matAstro.SetFloat("_giro_y", 0f);
                _matAstro.SetVector("_deslocamento", Vector4.zero);
                // o brilho do sol do céu (no mar: a luz da superfície) fica atrás do astro
                Vector3 d = _matAstro.GetVector("_direcao");
                _matCeu.SetVector("_SolDir", Medidas.G(d.x, d.y, d.z));
                _astro = nome == "espaco" ? "lua" : nome == "mar" ? "baleia" : "sol";
                _canto = 0f;
            }
            MostrarMarca(nome);
            temas.Mostrar(nome);
        }

        /// <summary>A baleia CANTA (o som vem do Temas, que chama aqui): por `dur` segundos ela
        /// bate o rabo mais forte, balança e respira fundo.</summary>
        public void BaleiaCantar(float dur) => _canto = dur;

        /// <summary>A baleia nada de um lado para o outro, sobe e desce de leve e vira nas pontas
        /// (de frente para a câmera bem na virada).</summary>
        void NadarBaleia(float dt)
        {
            if (_astro != "baleia" || _matAstro == null) return;
            if (_canto > 0f)
            {
                _canto = Mathf.Max(_canto - dt, 0f);
                float k = Mathf.Clamp01(_canto) * Mathf.Clamp01((3.5f - _canto) * 3f);
                _matAstro.SetFloat("_rabo", 0.07f + 0.1f * k);
                _matAstro.SetFloat("_respira", 0.015f + 0.035f * k);
                _matAstro.SetFloat("_balanco", 0.05f + 0.07f * k);
            }
            float w = Mathf.PI * 2f / BALEIA_VOLTA;
            float x = Mathf.Sin(_t * w) * BALEIA_ALCANCE;
            float ida = Mathf.Cos(_t * w);
            // (o deslocamento e o giro são da edição Godot: o shader faz a conta lá)
            _matAstro.SetVector("_deslocamento", new Vector4(x, Mathf.Sin(_t * 0.6f) * 0.08f, 0f, 0f));
            _matAstro.SetFloat("_giro_y", (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.35f, 0.35f, ida))) * Mathf.PI);
        }

        // ================================================================ animação
        public void AcenderPista(bool ligar) => _luzAlvo = ligar ? 1f : 0f;

        /// <summary>Modo das lâmpadas: 0 corrida, 1 piscando todas (strike!), 2 alternado.</summary>
        public void ModoLampadas(int modo)
        {
            if (_matLampadas != null) _matLampadas.SetFloat(ID_MODO, modo);
            Materiais.Lampadas.SetFloat(ID_MODO_CENARIO, modo);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _t = Mathf.Repeat(_t + dt, 3600f);
            _luz = Mathf.MoveTowards(_luz, _luzAlvo, dt * 2.5f);
            float pulso = 0.5f + 0.5f * Mathf.Sin(_t * 3.1f);
            if (_matPista != null)
            {
                _matPista.SetFloat(ID_LUZ, _luz);
                _matPista.SetFloat(ID_FASE, Mathf.Repeat(_t * 2.2f, 1f));
                _matPista.SetFloat(ID_FASE_LENTA, Mathf.Repeat(_t * 0.75f, 1f));
                _matPista.SetFloat(ID_PULSO, pulso);
                _matPista.SetFloat(ID_BOLA_Z, bolaZ);
            }
            if (_matPiso != null) _matPiso.SetFloat(ID_PULSO, pulso);
            // o tempo das canaletas já numa volta (G_canaleta)
            foreach (var m in _matsCanaleta)
            {
                m.SetFloat(ID_FASE, Mathf.Repeat(_t * 1.3f, 1f));
                m.SetFloat(ID_ONDA1, Mathf.Repeat(_t * 1.7f, Mathf.PI * 2f));
                m.SetFloat(ID_ONDA2, Mathf.Repeat(_t * 2.2f, Mathf.PI * 2f));
                m.SetFloat(ID_BOLHAS, Mathf.Repeat(_t * 0.12f, 3.52f));
            }
            float faseLampadas = Mathf.Repeat(_t * 0.9f, 100f);
            if (_matLampadas != null) _matLampadas.SetFloat(ID_FASE, faseLampadas);
            Materiais.Lampadas.SetFloat(ID_FASE_CENARIO, faseLampadas);
            // o símbolo do cenário flutua — sobe e desce, vai e volta e balança
            if (balao != null && balao.gameObject.activeSelf) MexerMarca();
            NadarBaleia(dt);
            if (_matFundo != null) _matFundo.SetFloat(ID_FASE, Mathf.Repeat(_t * 0.4f, 100f));
            if (_matPortal != null) _matPortal.SetFloat(ID_PULSO, Mathf.Sin(_t * 2.2f));
            MexerTiras(dt);
            for (int i = 0; i < _matsPodio.Count; i++)
            {
                if (_matsPodio[i] == null) continue;
                float on = i == _podioDestaque ? 0.75f + 0.25f * Mathf.Sin(_t * 6f) : 0f;
                _matsPodio[i].SetVector(ID_EMISSAO, new Vector4(1f, 0.75f, 0.15f, 1f) * on * 1.6f);
            }
        }

        /// <summary>Giro YXZ do Godot em radianos (como lá) para a Unity.</summary>
        static Quaternion GRad(float ax, float ay, float az) => Medidas.GRot(ax * Mathf.Rad2Deg, ay * Mathf.Rad2Deg, az * Mathf.Rad2Deg);

        /// <summary>O símbolo flutuando (o _mexer_marca da edição Godot, com o X ao contrário:
        /// giros em Y e Z trocam de sinal).</summary>
        void MexerMarca()
        {
            balao.localPosition = Medidas.BALAO_POS + new Vector3(-Mathf.Sin(_t * 0.31f) * 0.18f, Mathf.Sin(_t * 0.8f) * 0.14f, Mathf.Sin(_t * 0.23f) * 0.1f);
            switch (tema)
            {
                case "espaco":
                    balao.localRotation = GRad(Mathf.Sin(_t * 0.4f) * 0.06f, Mathf.Sin(_t * 0.3f) * 0.35f, Mathf.Sin(_t * 0.5f) * 0.05f);
                    break;
                case "mar":
                    {
                        float pulso = Mathf.Sin(_t * 2f);
                        balao.localRotation = GRad(0f, Mathf.Sin(_t * 0.35f) * 0.25f, Mathf.Sin(_t * 0.6f) * 0.06f);
                        balao.localScale = new Vector3(1f + pulso * 0.03f, 1f - pulso * 0.03f, 1f + pulso * 0.03f) * 0.3f;
                        for (int i = 0; i < _bolhinhas.Count; i++)
                        {
                            // sobem em volta da bolha e recomeçam embaixo
                            float f = Mathf.Repeat(_t * 0.22f + i * 0.25f, 1f);
                            float a = i * 1.9f + _t * 0.4f;
                            _bolhinhas[i].localPosition = new Vector3(-Mathf.Cos(a) * 1.25f, -1f + f * 3.4f, Mathf.Sin(a) * 0.5f + 0.3f);
                            _bolhinhas[i].gameObject.SetActive(f < 0.92f);
                        }
                        break;
                    }
                case "dinos":
                    {
                        balao.localRotation = GRad(Mathf.Sin(_t * 0.7f) * 0.05f, Mathf.Sin(_t * 0.4f) * 0.22f, Mathf.Sin(_t * 0.9f) * 0.06f);
                        // bate as asas (o shader) e, a cada batida para baixo, o bicho sobe um
                        // pouquinho levando a placa junto, e balança de leve
                        float batida = Mathf.Repeat(_t * ASAS_MARCA, Mathf.PI * 2f);
                        if (_matAsasMarca != null) _matAsasMarca.SetFloat("_AsaTempo", batida);
                        if (_vooMarca != null)
                        {
                            _vooMarca.localPosition = new Vector3(0f, -Mathf.Cos(batida) * 0.028f, 0f);
                            _vooMarca.localRotation = GRad(Mathf.Sin(batida) * 0.035f, 0f, 0f);
                        }
                        break;
                    }
                default:
                    balao.localRotation = GRad(Mathf.Sin(_t * 0.5f) * 0.05f, Mathf.Sin(_t * 0.45f) * 0.3f, Mathf.Sin(_t * 0.6f) * 0.08f);
                    break;
            }
        }

        /// <summary>As tiras da cortina da caçapa abrem (giram, presas em cima: G_cortina) quando
        /// a bola passa — antes de ela chegar, e depressa: ela nunca encosta nelas.</summary>
        void MexerTiras(float dt)
        {
            if (_matCortina == null) return;
            float meia = Medidas.LARGURA_BOCA / 2f;
            // (a conta é a da edição Godot: o X de lá)
            Vector3 pb = bola != null && bola.gameObject.activeInHierarchy ? bola.position : new Vector3(-100, 100, 100);
            float bx = -pb.x;
            for (int i = 0; i < Medidas.N_TIRAS; i++)
            {
                float x = -meia + Medidas.LARGURA_BOCA * (i + 0.5f) / Medidas.N_TIRAS;
                float alvo = 0f;
                if (Mathf.Abs(pb.z - Medidas.Z_FUNDO) < 0.8f && Mathf.Abs(bx - x) < 0.3f && pb.y < Medidas.Y_BOCA) alvo = 1.25f;
                float a = _aberturaTiras[i];
                float novo = Mathf.MoveTowards(a, alvo, dt * (alvo > a ? 14f : 2.2f));
                if (novo != a || !_tirasPostas)
                {
                    _aberturaTiras[i] = novo;
                    _matCortina.SetFloat(ID_TIRA[i], novo);
                }
            }
            _tirasPostas = true;
        }
    }
}
