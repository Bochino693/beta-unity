using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// O MUNDO 3D: céu, sol, terreno com morros, a pista (deck, canaletas,
    /// para-choques), o fundo dos pinos (cortina, letreiro com lâmpadas, arco
    /// de raios), a área de quem joga e os 4 CENÁRIOS (Temas).
    ///
    /// Linha de falta em z = 0; os pinos em z = -10,5; a pista vai para -Z.
    /// As peças vêm do modelador (Malhas/pista e Malhas/pista_&lt;fase&gt;); a
    /// física (colisores) é montada aqui com as mesmas medidas das edições Godot.
    /// </summary>
    public class Mundo : MonoBehaviour
    {
        public static Mundo I { get; private set; }

        public string tema = "";
        public bool leve;
        public Temas temas;
        public Transform bola;          // a bola do jogo (as tiras da caçapa abrem quando ela passa)
        public float bolaZ = 100f;      // distância da bola à linha de falta (o brilho da pista)

        Material _matPista, _matCeu, _matAstro, _matBumper, _matLogo;
        readonly Dictionary<string, GameObject> _estruturas = new Dictionary<string, GameObject>();
        readonly List<GameObject> _bumpers = new List<GameObject>();
        readonly List<Collider> _colBumpers = new List<Collider>();
        Transform[] _tiras;
        float[] _aberturaTiras;
        GameObject _bolaVolta;
        Coroutine _retorno;
        readonly List<Transform> _podios = new List<Transform>();
        readonly List<Material> _matsAnel = new List<Material>();
        int _podioDestaque = -1;
        readonly Dictionary<string, Transform> _marcas = new Dictionary<string, Transform>();
        readonly List<Transform> _bolhinhas = new List<Transform>();
        Transform _pteroMarca;
        public Transform balao;
        float _t, _luz, _luzAlvo;
        Transform _fisica;

        static readonly int ID_LUZ = Shader.PropertyToID("_Luz");
        static readonly int ID_FASE = Shader.PropertyToID("_Fase");
        static readonly int ID_FASE_LENTA = Shader.PropertyToID("_FaseLenta");
        static readonly int ID_PULSO = Shader.PropertyToID("_Pulso");
        static readonly int ID_BOLA_Z = Shader.PropertyToID("_BolaZ");
        static readonly int ID_MODO = Shader.PropertyToID("_Modo");

        // sol e lua no céu: imagem, onde fica visto da área de jogo (Godot), tamanho a ~67 m, tinta
        static readonly Dictionary<string, (string img, Vector3 pos, float tam, Color tinta)> ASTROS =
            new Dictionary<string, (string, Vector3, float, Color)>
            {
                ["parque"] = ("ceu_sol", Medidas.G(9.8f, 25.2f, -59.4f), 8.5f, Color.white),
                ["dinos"] = ("ceu_sol", Medidas.G(9.8f, 25.2f, -59.4f), 8.5f, Color.white),
                ["mar"] = ("ceu_sol", Medidas.G(9.8f, 25.2f, -59.4f), 8.0f, new Color(0.86f, 1f, 1f, 0.82f)),
                ["espaco"] = ("ceu_lua", Medidas.G(-1.1f, 26.3f, -59.7f), 7.2f, Color.white),
            };
        static readonly Vector3 ASTRO_REFERENCIA = new Vector3(0f, 1.2f, 2.4f);
        const float ASTRO_DISTANCIA = 140f;
        /// <summary>Para onde está o sol (direção da luz) — o mesmo das edições Godot.</summary>
        public static readonly Vector3 DIR_SOL = Medidas.G(0.289f, 0.788f, 0.544f).normalized;

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
            Astro();
            Estrutura();
            Fisica();
            Lampadas();
            Logos();
            Marcas();
            Podios();
            var tg = new GameObject("Temas");
            tg.transform.SetParent(transform, false);
            temas = tg.AddComponent<Temas>();
            temas.Construir(leve);
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

        Material MaterialDaParte(Malhas.Parte p)
        {
            switch (p.material)
            {
                case "pista": return _matPista;
                case "bumper": return _matBumper;
                default: return Materiais.DoPacote(p.material);
            }
        }

        void Estrutura()
        {
            _matPista = Materiais.Novo("Pista", "pista");
            var tex = Materiais.Textura("pista_pronta");
            tex.wrapMode = TextureWrapMode.Clamp;
            _matPista.SetTexture(Materiais.ID_TEX, tex);
            _matBumper = Materiais.Novo("Bumper", "bumper");
            var pc = Malhas.Ler("pista", true);
            var pular = new HashSet<string> { "lampadas", "tira", "bumper", "mesa", "barra", "porta", "podio_0", "podio_1", "podio_anel_0", "podio_anel_1" };
            Malhas.Montar(pc, transform, null, MaterialDaParte, pular);
            // o que muda em cada fase (cores e desenhos)
            foreach (var f in Temas.ORDEM)
            {
                var go = Malhas.Montar(Malhas.Ler("pista_" + f), transform, null, MaterialDaParte);
                go.SetActive(false);
                _estruturas[f] = go;
            }
            // para-choques: 10 de cada lado (acabam antes da barra varredora)
            var mb = pc.Malha("bumper");
            foreach (float s in new[] { -1f, 1f })
            {
                var g = new GameObject("bumpers_" + (s < 0 ? "e" : "d"));
                g.transform.SetParent(transform, false);
                for (int i = 0; i < Medidas.N_BUMPERS; i++)
                {
                    var b = Malhas.Objeto("bumper", mb, _matBumper, g.transform);
                    b.transform.localPosition = new Vector3(s * 0.735f, 0.1f, -0.6f - i * 0.96f);
                }
                _bumpers.Add(g);
            }
            // cortina de tiras de borracha na boca da caçapa
            var mt = pc.Malha("tira");
            _tiras = new Transform[Medidas.N_TIRAS];
            _aberturaTiras = new float[Medidas.N_TIRAS];
            var gt = new GameObject("tiras");
            gt.transform.SetParent(transform, false);
            for (int i = 0; i < Medidas.N_TIRAS; i++)
            {
                float x = -Medidas.LARGURA_BOCA / 2f + Medidas.LARGURA_BOCA * (i + 0.5f) / Medidas.N_TIRAS;
                var t = Malhas.Objeto("tira", mt, Materiais.Facetado, gt.transform);
                t.transform.localPosition = new Vector3(x, Medidas.Y_BOCA - 0.015f, Medidas.Z_FUNDO + 0.03f);
                _tiras[i] = t.transform;
            }
        }

        // ================================================================ céu e sol
        void Ceu()
        {
            _matCeu = Materiais.Novo("Ceu", "ceu");
            _matCeu.SetTexture(Materiais.ID_TEX, Materiais.Textura("estrelas_ceu"));
            var m = new Mesh { name = "ceu" };
            int gomos = 32, aneis = 16;
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

        void Astro()
        {
            _matAstro = Materiais.Novo("Astro", "astro");
            _matAstro.SetTexture("_Raios", Materiais.Textura("ceu_sol_raios"));
            var q = Malhas.Quadrado(1f, 1f);
            q.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            Malhas.Objeto("astro", q, _matAstro, transform);
        }

        void MostrarAstro(string nome)
        {
            var a = ASTROS.TryGetValue(nome, out var x) ? x : ASTROS["parque"];
            var rel = a.pos - ASTRO_REFERENCIA;
            var dir = rel.normalized;
            bool sol = a.img == "ceu_sol";
            _matAstro.SetTexture(Materiais.ID_TEX, Materiais.Textura(a.img));
            _matAstro.SetFloat("_UsaRaios", sol ? 1f : 0f);
            _matAstro.SetColor(Materiais.ID_TINTA, a.tinta);
            _matAstro.SetVector("_Direcao", dir);
            _matAstro.SetFloat("_Distancia", ASTRO_DISTANCIA);
            _matAstro.SetFloat("_Tamanho", a.tam * ASTRO_DISTANCIA / rel.magnitude);
            _matCeu.SetVector("_SolDir", dir);
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

        // ================================================================ lâmpadas e logos
        void Lampadas()
        {
            var pts = Malhas.Ler("pista", true).Achar("lampadas");
            if (pts == null || pts.vertices == null) return;
            var cores = new[] { Materiais.Hex("#FFE14D"), Materiais.Hex("#FF9F2E"), Materiais.Hex("#4FD8FF"), Materiais.Hex("#8DFF6A") };
            // uma bolinha facetada (octaedro alongado) por lâmpada, numa malha só
            var bolinha = new[] { new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1), new Vector3(-1, 0, 0), new Vector3(0, 0, -1), new Vector3(0, -1, 0) };
            int[] tris = { 0, 2, 1, 0, 3, 2, 0, 4, 3, 0, 1, 4, 5, 1, 2, 5, 2, 3, 5, 3, 4, 5, 4, 1 };
            var V = new List<Vector3>();
            var C = new List<Color32>();
            var U = new List<Vector2>();
            var T = new List<int>();
            float r = 0.052f;
            for (int i = 0; i < pts.vertices.Length; i++)
            {
                var c = pts.vertices[i];
                int b = V.Count;
                for (int k = 0; k < bolinha.Length; k++)
                {
                    V.Add(c + bolinha[k] * r);
                    C.Add(cores[i % cores.Length]);
                    U.Add(new Vector2(i, bolinha[k].z > 0.5f ? 1f : 0.35f));
                }
                for (int k = 0; k < tris.Length; k++) T.Add(b + tris[k]);
            }
            var m = new Mesh { name = "lampadas" };
            m.SetVertices(V);
            m.SetColors(C);
            m.SetUVs(0, U);
            m.SetTriangles(T, 0);
            m.RecalculateBounds();
            Malhas.Objeto("lampadas", m, Materiais.Lampadas, transform);
        }

        void Logos()
        {
            var logo = Materiais.UI("logo");
            if (logo == null) return;
            _matLogo = Materiais.Novo("Recorte", "logo");
            _matLogo.SetTexture(Materiais.ID_TEX, logo);
            float asp = (float)logo.width / logo.height;
            // letreiro acima dos pinos (virado para quem joga)
            float w = Mathf.Min(2.5f, 0.86f * asp);
            var l1 = Malhas.Objeto("logo_pinos", Malhas.Quadrado(w, w / asp), _matLogo, transform);
            l1.transform.localPosition = new Vector3(0, 1.86f, Medidas.Z_PINO1 + 0.83f);
            // letreiro dos jogadores (virado para a pista)
            var l2 = Malhas.Objeto("logo_jogadores", Malhas.Quadrado(3f, 3f / asp), _matLogo, transform);
            l2.transform.localPosition = new Vector3(0, 2.55f, 7.7f - 0.2f);
            l2.transform.localRotation = Quaternion.Euler(0, 180f, 0);
        }

        // ================================================================ símbolo da marca em cada cenário
        void Marcas()
        {
            var tex = Materiais.UI("marca_lazer_sport");
            Material matLogo = null;
            if (tex != null)
            {
                matLogo = Materiais.Novo("Recorte", "marca");
                matLogo.SetTexture(Materiais.ID_TEX, tex);
            }
            float asp = tex != null ? (float)tex.width / tex.height : 1.5f;
            Transform Novo(string nome)
            {
                var n = new GameObject(nome).transform;
                n.SetParent(transform, false);
                n.localPosition = Medidas.BALAO_POS;
                n.gameObject.SetActive(false);
                return n;
            }
            void Logo(Transform pai, float largura, Vector3 pos)
            {
                if (matLogo == null) return;
                var l = Malhas.Objeto("logo", Malhas.Quadrado(largura, largura / asp), matLogo, pai);
                l.transform.localPosition = pos;
            }
            // Parque: o balãozinho vermelho com a logo
            var parque = Novo("marca_parque");
            var mb = Materiais.Novo("Facetado", "balao_marca");
            mb.SetColor(Materiais.ID_TINTA, Materiais.Hex("#E8384F"));
            var b = Malhas.Montar(Malhas.Ler("cenario/balao"), parque, null, p => p.material == "luz" ? Materiais.SemLuz : mb);
            b.transform.localScale = Vector3.one * 1.25f;
            b.transform.localPosition = new Vector3(0, -0.35f, 0);
            Logo(parque, 0.42f, new Vector3(0, 0.0f, 0.26f));
            _marcas["parque"] = parque;
            // Espaço: um planetinha roxo com anel, a logo na frente
            var espaco = Novo("marca_espaco");
            var mp = Materiais.Novo("Facetado", "planeta_marca");
            mp.SetColor(Materiais.ID_TINTA, Materiais.Hex("#B9A8FF"));
            var pl = Malhas.Montar(Malhas.Ler("cenario/planeta_anel"), espaco, null, p => p.material == "luz" ? Materiais.SemLuz : mp);
            pl.transform.localScale = Vector3.one * 0.3f;
            Logo(espaco, 0.4f, new Vector3(0, 0, 0.33f));
            _marcas["espaco"] = espaco;
            // Mar: uma bolha de vidro com a logo dentro e bolhinhas subindo
            var mar = Novo("marca_mar");
            var bolha = Malhas.Objeto("bolha", EsferaSimples(0.3f, 14, 9), Materiais.Vidro, mar);
            bolha.GetComponent<MeshRenderer>().sharedMaterial = Materiais.Vidro;
            Logo(mar, 0.38f, new Vector3(0, 0, 0.02f));
            for (int i = 0; i < 4; i++)
            {
                var bi = Malhas.Objeto("bolhinha", EsferaSimples(0.04f + 0.015f * (i % 2), 8, 5), Materiais.Vidro, mar);
                _bolhinhas.Add(bi.transform);
            }
            _marcas["mar"] = mar;
            // Selva dos Dinos: o pterossauro segurando a plaquinha de madeira com a logo
            var dinos = Novo("marca_dinos");
            var placa = Malhas.Objeto("placa", CaixaSimples(0.74f, 0.52f, 0.05f, new Color32(122, 74, 34, 255)), Materiais.Facetado, dinos);
            placa.transform.localPosition = new Vector3(0, 0, -0.012f);
            var frente = Malhas.Objeto("frente", CaixaSimples(0.68f, 0.46f, 0.05f, new Color32(255, 233, 194, 255)), Materiais.Facetado, dinos);
            frente.transform.localPosition = new Vector3(0, 0, 0.002f);
            Logo(dinos, 0.6f, new Vector3(0, 0, 0.03f));
            var nos = new Dictionary<string, Transform>();
            var pt = Malhas.Montar(Malhas.Ler("cenario/ptero"), dinos, nos);
            pt.transform.localPosition = new Vector3(0, 0.66f, 0.02f);
            pt.transform.localScale = Vector3.one * 0.62f;
            _pteroMarca = pt.transform;
            _asasMarca = new[] { nos.TryGetValue("asa_e", out var ae) ? ae : null, nos.TryGetValue("asa_d", out var ad) ? ad : null };
            _marcas["dinos"] = dinos;
            balao = parque;
        }

        Transform[] _asasMarca = new Transform[0];

        static Mesh EsferaSimples(float r, int gomos, int aneis)
        {
            var V = new List<Vector3>();
            var N = new List<Vector3>();
            var C = new List<Color32>();
            var T = new List<int>();
            for (int j = 0; j <= aneis; j++)
            {
                float b = Mathf.PI * j / aneis;
                for (int i = 0; i <= gomos; i++)
                {
                    float a = 2f * Mathf.PI * i / gomos;
                    var n = new Vector3(Mathf.Cos(a) * Mathf.Sin(b), Mathf.Cos(b), Mathf.Sin(a) * Mathf.Sin(b));
                    V.Add(n * r);
                    N.Add(n);
                    C.Add(new Color32(220, 248, 255, 60));
                }
            }
            for (int j = 0; j < aneis; j++)
                for (int i = 0; i < gomos; i++)
                {
                    int a = j * (gomos + 1) + i, b = a + 1, c = a + gomos + 1, d = c + 1;
                    T.Add(a); T.Add(c); T.Add(b);
                    T.Add(b); T.Add(c); T.Add(d);
                }
            var m = new Mesh { name = "esfera" };
            m.SetVertices(V);
            m.SetNormals(N);
            m.SetColors(C);
            m.SetTriangles(T, 0);
            m.RecalculateBounds();
            return m;
        }

        static Mesh CaixaSimples(float x, float y, float z, Color32 cor)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var src = go.GetComponent<MeshFilter>().sharedMesh;
            var m = Instantiate(src);
            Destroy(go);
            var V = m.vertices;
            for (int i = 0; i < V.Length; i++) V[i] = Vector3.Scale(V[i], new Vector3(x, y, z));
            m.vertices = V;
            var C = new Color32[V.Length];
            for (int i = 0; i < C.Length; i++) C[i] = cor;
            m.colors32 = C;
            m.RecalculateBounds();
            return m;
        }

        void MostrarMarca(string nome)
        {
            foreach (var k in _marcas) k.Value.gameObject.SetActive(k.Key == nome);
            balao = _marcas.TryGetValue(nome, out var b) ? b : _marcas["parque"];
        }

        // ================================================================ pódios e retorno de bolas
        void Podios()
        {
            var pc = Malhas.Ler("pista");
            for (int i = 0; i < 2; i++)
            {
                var g = new GameObject("podio_" + i).transform;
                g.SetParent(transform, false);
                g.gameObject.SetActive(false);
                Malhas.Objeto("base", pc.Malha("podio_" + i), Materiais.Facetado, g);
                var ma = Materiais.Novo("SemLuz", "anel_podio");
                ma.SetColor(Materiais.ID_TINTA, new Color(0.55f, 0.48f, 0.25f));
                Malhas.Objeto("anel", pc.Malha("podio_anel_" + i), ma, g);
                _podios.Add(g);
                _matsAnel.Add(ma);
            }
        }

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
            if (_bolaVolta == null)
            {
                _bolaVolta = Malhas.Montar(Malhas.Ler("bola"), transform);
            }
            foreach (var r in _bolaVolta.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = mat;
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
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = c.ceu[2];
            // a luz: sol, céu e chão (ambiente), contraluz e neblina
            Color ceu = Materiais.Vezes(c.ambiente, c.ambienteForca * 1.15f);
            Color chao = Materiais.Vezes(Color.Lerp(c.ambiente, c.ceu[3], 0.35f), c.ambienteForca * 0.72f);
            Color sol = Materiais.Vezes(c.sol, c.solForca * 0.6f);
            Color borda = Materiais.Vezes(Color.Lerp(Color.white, c.ceu[1], 0.3f), 0.22f);
            float dens = leve ? c.neblinaDensidade * 0.6f : c.neblinaDensidade;
            Materiais.DefinirLuz(DIR_SOL, sol, ceu, chao, borda, c.neblina, dens);
            // o chão da fase
            var mc = Materiais.Chao;
            mc.SetColor("_Cor0", c.chao[0]);
            mc.SetColor("_Cor1", c.chao[1]);
            mc.SetColor("_Cor2", c.chao[2]);
            mc.SetColor("_Cor3", c.chao[3]);
            // a pista: neon e reflexo do portal; para-choques
            _matPista.SetColor("_CorLuz", c.neon);
            _matPista.SetColor("_CorReflexo", c.portal[0]);
            _matPista.SetColor("_LuzCena", Color.Lerp(Color.white, c.ambiente, 0.15f));
            _matBumper.SetColor("_Cor1", c.bumper1);
            _matBumper.SetColor("_Cor2", c.bumper2);
            foreach (var e in _estruturas) e.Value.SetActive(e.Key == nome);
            MostrarMarca(nome);
            MostrarAstro(nome);
            temas.Mostrar(nome);
        }

        // ================================================================ animação
        public void AcenderPista(bool ligar) => _luzAlvo = ligar ? 1f : 0f;

        /// <summary>Modo das lâmpadas: 0 corrida, 1 piscando todas (strike!), 2 alternado.</summary>
        public void ModoLampadas(int modo) => Materiais.Lampadas.SetFloat(ID_MODO, modo);

        void Update()
        {
            float dt = Time.deltaTime;
            _t = Mathf.Repeat(_t + dt, 3600f);
            _luz = Mathf.MoveTowards(_luz, _luzAlvo, dt * 2.5f);
            _matPista.SetFloat(ID_LUZ, _luz);
            _matPista.SetFloat(ID_FASE, Mathf.Repeat(_t * 2.2f, 1f));
            _matPista.SetFloat(ID_FASE_LENTA, Mathf.Repeat(_t * 0.75f, 1f));
            _matPista.SetFloat(ID_PULSO, 0.5f + 0.5f * Mathf.Sin(_t * 3.1f));
            _matPista.SetFloat(ID_BOLA_Z, bolaZ);
            Materiais.Lampadas.SetFloat(ID_FASE, Mathf.Repeat(_t * 0.9f, 100f));
            if (balao != null && balao.gameObject.activeSelf) MexerMarca();
            MexerTiras(dt);
            for (int i = 0; i < _matsAnel.Count; i++)
            {
                float on = i == _podioDestaque ? 0.75f + 0.25f * Mathf.Sin(_t * 6f) : 0f;
                _matsAnel[i].SetColor(Materiais.ID_TINTA, Color.Lerp(new Color(0.55f, 0.48f, 0.25f), new Color(1.6f, 1.25f, 0.45f), on));
            }
        }

        void MexerMarca()
        {
            balao.localPosition = Medidas.BALAO_POS + new Vector3(-Mathf.Sin(_t * 0.31f) * 0.18f, Mathf.Sin(_t * 0.8f) * 0.14f, Mathf.Sin(_t * 0.23f) * 0.1f);
            switch (tema)
            {
                case "espaco":
                    balao.localRotation = Quaternion.Euler(Mathf.Sin(_t * 0.4f) * 3.4f, -Mathf.Sin(_t * 0.3f) * 20f, -Mathf.Sin(_t * 0.5f) * 2.9f);
                    break;
                case "mar":
                    {
                        float pulso = Mathf.Sin(_t * 2f);
                        balao.localRotation = Quaternion.Euler(0, -Mathf.Sin(_t * 0.35f) * 14f, -Mathf.Sin(_t * 0.6f) * 3.4f);
                        balao.localScale = new Vector3(1f + pulso * 0.03f, 1f - pulso * 0.03f, 1f + pulso * 0.03f);
                        for (int i = 0; i < _bolhinhas.Count; i++)
                        {
                            float f = Mathf.Repeat(_t * 0.22f + i * 0.25f, 1f);
                            float a = i * 1.9f + _t * 0.4f;
                            _bolhinhas[i].localPosition = new Vector3(-Mathf.Cos(a) * 0.37f, -0.3f + f * 1.0f, Mathf.Sin(a) * 0.15f + 0.09f);
                            _bolhinhas[i].gameObject.SetActive(f < 0.92f);
                        }
                        break;
                    }
                case "dinos":
                    {
                        balao.localRotation = Quaternion.Euler(Mathf.Sin(_t * 0.7f) * 2.9f, -Mathf.Sin(_t * 0.4f) * 12.6f, -Mathf.Sin(_t * 0.9f) * 3.4f);
                        if (_pteroMarca != null)
                        {
                            _pteroMarca.localPosition = new Vector3(0, 0.66f + Mathf.Sin(_t * 5f) * 0.025f, 0.02f);
                            _pteroMarca.localRotation = Quaternion.Euler(Mathf.Sin(_t * 5f) * 4.6f, 0, 0);
                            float bate = Mathf.Sin(_t * 10f) * 30f;
                            if (_asasMarca.Length > 1)
                            {
                                if (_asasMarca[0] != null) _asasMarca[0].localRotation = Quaternion.Euler(0, 0, bate);
                                if (_asasMarca[1] != null) _asasMarca[1].localRotation = Quaternion.Euler(0, 0, -bate);
                            }
                        }
                        break;
                    }
                default:
                    balao.localRotation = Quaternion.Euler(Mathf.Sin(_t * 0.5f) * 2.9f, -Mathf.Sin(_t * 0.45f) * 17f, -Mathf.Sin(_t * 0.6f) * 4.6f);
                    break;
            }
        }

        /// <summary>As tiras abrem (giram, presas em cima) quando a bola passa.</summary>
        void MexerTiras(float dt)
        {
            if (_tiras == null) return;
            Vector3 pb = bola != null && bola.gameObject.activeInHierarchy ? bola.position : new Vector3(100, 100, 100);
            for (int i = 0; i < _tiras.Length; i++)
            {
                float x = _tiras[i].localPosition.x;
                float alvo = 0f;
                // abrem ANTES de a bola chegar (e depressa): ela nunca encosta nelas
                if (Mathf.Abs(pb.z - Medidas.Z_FUNDO) < 0.8f && Mathf.Abs(pb.x - x) < 0.3f && pb.y < Medidas.Y_BOCA) alvo = 1.25f;
                float a = _aberturaTiras[i];
                float novo = Mathf.MoveTowards(a, alvo, dt * (alvo > a ? 14f : 2.2f));
                if (novo != a)
                {
                    _aberturaTiras[i] = novo;
                    _tiras[i].localRotation = Quaternion.Euler(-novo * Mathf.Rad2Deg, 0, 0);
                }
            }
        }
    }
}
