using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// UM PINO com carinha. Corpo físico com o centro de massa no lugar de um
    /// pino de verdade (ele tomba fácil), sons de batida e expressões:
    /// normal / piscando / medo (bola chegando) / tonto ou bravinho (caiu) /
    /// feliz (ficou de pé — às vezes até dá pulinhos de comemoração).
    /// Os pulinhos são animação (o pino fica preso, sem física) e terminam
    /// exatamente onde ele estava: na hora da jogada está tudo no lugar.
    ///
    /// A ORIGEM do pino é o centro de massa (COM, 17 cm acima da base); a colisão
    /// do modelador vem nesse espaço e o desenho (o pino da edição Godot, com a
    /// base na origem) fica no pivô do pé. `lugar` é a BASE do pino; Base() /
    /// Colocar() fazem a conta. A carinha é a arte de cada expressão numa
    /// textura (rosto_pino, 4 x 2 células) no toon de personagem
    /// (G_toon_personagem); trocar de cara = trocar a célula. Três malhas: a de
    /// PERTO (mais redonda, com os detalhes extras), a de sempre e a de LONGE
    /// (Pinos escolhe pela distância da câmera).
    /// </summary>
    public class Pino : MonoBehaviour
    {
        public static readonly string[] EXPR = { "normal", "piscando", "medo", "tonto", "feliz", "surpreso", "triste", "bravo" };
        public const float MASSA = 1.5f;
        public static readonly Vector3 COM = new Vector3(0f, 0.17f, 0f);
        public const float ALTURA = 0.48f;

        public event Action<Pino, float> Bateu;

        public int numero = 1;
        public Vector3 lugar;
        public bool removido;
        public Rigidbody rb;
        public string Expr => _expr;

        static Mesh _malhaPerto, _malhaLonge;
        static Mesh _colisao;
        static Material _matEstrela;
        static Vector4 _rostoCentro = new Vector4(0f, 0.215f, 0f, 0f), _rostoTamanho = new Vector4(0.15f, 0.13f, 0f, 0f);
        static readonly int ID_ESCURO = Shader.PropertyToID("_escuro");
        static readonly int ID_CELULA = Shader.PropertyToID("_rosto_celula");
        /// <summary>O pino de perto mais redondo (os detalhes extras).</summary>
        public static bool redondos = true;
        Material _mat;

        MeshFilter _mf;
        MeshRenderer _mr;
        MeshCollider _col;
        Transform _pe;          // pivô na base: os pulinhos amassam e a raiva treme em volta do pé
        Transform _estrelas;
        Transform[] _estrela = new Transform[3];
        MaterialPropertyBlock _mpb;
        string _expr = "normal";
        float _piscar;
        float _piscando;
        float _ultimoSom = -1f;
        float _escuro = 1f;
        Coroutine _pulo, _raiva;
        bool _pulando;
        Vector3 _basePuloPos;
        Quaternion _basePuloRot;
        int _voltaInterp;

        static void Carregar()
        {
            if (_malhaPerto != null) return;
            // a forma de colisão (calibrada com a tabela de jogadas) é a do modelador
            _colisao = Malhas.Ler("pinos", true).Malha("colisao");
            // o desenho: os pinos da edição Godot (base na origem)
            var meio = Malhas.Ler("pino").partes[0].malha;
            _malhaPerto = redondos && Malhas.Existe("pino_perto") ? Malhas.Ler("pino_perto").partes[0].malha : meio;
            _malhaLonge = Malhas.Existe("pino_longe") ? Malhas.Ler("pino_longe").partes[0].malha : meio;
            var meta = Personagem.Meta("pino");
            if (meta != null && Json.Dic(Json.Pegar(meta, "rosto")) is Dictionary<string, object> r && r.ContainsKey("centro"))
            {
                _rostoCentro = Palco.Vetor(r["centro"]);
                _rostoTamanho = Palco.Vetor(r["tamanho"]);
            }
            _matEstrela = Materiais.Novo("Recorte", "estrela_tonto");
            _matEstrela.SetTexture(Materiais.ID_TEX, Materiais.Textura("estrela"));
            _matEstrela.SetColor(Materiais.ID_TINTA, Materiais.Hex("#FFE14D"));
            _matEstrela.SetFloat("_Corte", 0.5f);
        }

        public static Pino Criar(Transform pai, int n, Vector3 base_)
        {
            Carregar();
            var go = new GameObject("Pino" + n);
            go.layer = Medidas.CAMADA_PINOS;
            go.transform.SetParent(pai, false);
            var p = go.AddComponent<Pino>();
            p.Montar(n, base_);
            return p;
        }

        void Montar(int n, Vector3 base_)
        {
            numero = n;
            lugar = base_;
            _piscar = UnityEngine.Random.Range(1f, 4f);
            _mpb = new MaterialPropertyBlock();
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            _col = gameObject.AddComponent<MeshCollider>();
            _col.convex = true;
            _col.sharedMesh = _colisao;
            _col.sharedMaterial = Medidas.MaterialPino();
            rb.mass = MASSA;
            rb.centerOfMass = Vector3.zero;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.maxAngularVelocity = 100f;
            rb.isKinematic = true;
            // a malha num pivô na base (os pulinhos amassam em volta do pé)
            _pe = new GameObject("pe").transform;
            _pe.SetParent(transform, false);
            _pe.localPosition = -COM;
            var m = new GameObject("malha");
            m.transform.SetParent(_pe, false);
            _mf = m.AddComponent<MeshFilter>();
            _mf.sharedMesh = _malhaPerto;
            _mr = m.AddComponent<MeshRenderer>();
            // o toon de personagem com a carinha (cada pino a dele: a cara e o escuro do poço)
            _mat = Materiais.Novo("G/toon_personagem", "pino_" + n);
            _mat.SetFloat("_usa_rosto", 1f);
            _mat.SetTexture("_rosto", Palco.Textura("rosto_pino"));
            _mat.SetVector("_rosto_grade", new Vector4(4f, 2f, 0f, 0f));
            _mat.SetVector("_rosto_centro", _rostoCentro);
            _mat.SetVector("_rosto_tamanho", _rostoTamanho);
            _mat.SetFloat("_borda", 0.4f);
            _mat.SetVector("_cor_borda", new Vector4(0.92f, 0.96f, 1f, 1f));
            if (Personagem.contornos)
            {
                var c = Materiais.Novo("G/contorno", "pino_contorno");
                c.SetFloat("_espessura", 0.0026f);
                _mr.sharedMaterials = new[] { _mat, c };
            }
            else _mr.sharedMaterial = _mat;
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mr.receiveShadows = false;
            _mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            // estrelinhas da tontura (soltas no mundo, giram em volta da cabeça)
            _estrelas = new GameObject("estrelas_" + n).transform;
            _estrelas.SetParent(transform.parent, false);
            var q = Malhas.Quadrado(0.07f, 0.07f);
            for (int i = 0; i < 3; i++)
            {
                var e = Malhas.Objeto("estrela", q, _matEstrela, _estrelas);
                float a = Mathf.PI * 2f * i / 3f;
                e.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.1f, 0, Mathf.Sin(a) * 0.1f);
                _estrela[i] = e.transform;
            }
            _estrelas.gameObject.SetActive(false);
            Recolocar();
        }

        /// <summary>O desenho do pino (para o reflexo na pista).</summary>
        public MeshRenderer Corpo => _mr;
        /// <summary>A malha de agora.</summary>
        public Mesh MalhaAtual => _mf.sharedMesh;
        /// <summary>O pino do reflexo na pista (o de longe: metade do custo).</summary>
        public Mesh MalhaReflexo => _malhaLonge != null ? _malhaLonge : _mf.sharedMesh;

        /// <summary>Perto ou longe (Pinos escolhe pela distância da câmera).</summary>
        public void DeLonge(bool longe) => _mf.sharedMesh = longe ? _malhaLonge : _malhaPerto;

        // ------------------------------------------------------------ onde
        /// <summary>A base do pino (onde ele pisa), pela física.</summary>
        public Vector3 Base() => rb.position + rb.rotation * -COM;

        /// <summary>A base do pino onde ele APARECE neste quadro (interpolada).</summary>
        public Vector3 BaseNaTela() => transform.TransformPoint(-COM);

        /// <summary>Põe o pino em pé com a base em `b`, na hora (sem deslizar).</summary>
        public void Colocar(Vector3 b)
        {
            Teleportar(b + COM, Quaternion.identity);
        }

        /// <summary>Leva o pino preso (a mesa da máquina carregando): anda liso entre os passos.</summary>
        public void Levar(Vector3 b)
        {
            rb.MovePosition(b + COM);
            rb.MoveRotation(Quaternion.identity);
        }

        public void Teleportar(Vector3 pos, Quaternion rot)
        {
            // sem interpolar no quadro do pulo (senão ele "escorrega" do lugar antigo)
            rb.interpolation = RigidbodyInterpolation.None;
            rb.position = pos;
            rb.rotation = rot;
            transform.SetPositionAndRotation(pos, rot);
            _voltaInterp = 2;
        }

        /// <summary>Preso = sem física (a máquina carrega, os pulinhos animam).</summary>
        public bool Preso => rb.isKinematic;

        public void Recolocar()
        {
            PararPulo();
            if (!rb.isKinematic)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
            removido = false;
            _mr.enabled = true;
            _col.enabled = true;
            Colocar(lugar);
            _pe.localScale = Vector3.one;
            _pe.localRotation = Quaternion.identity;
            _estrelas.gameObject.SetActive(false);
            Escurecer(1f);
            Expressao("normal");
        }

        /// <summary>Libera a física (antes da bola chegar).</summary>
        public void Soltar()
        {
            if (removido) return;
            PararPulo();
            rb.isKinematic = false;
            rb.WakeUp();
        }

        public void Prender()
        {
            if (!rb.isKinematic)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
        }

        /// <summary>Sem colidir com nada (carregado pela mesa) / de volta.</summary>
        public void Fantasma(bool sim) => _col.enabled = !sim && !removido;

        public bool Caido()
        {
            if (removido) return true;
            var up = rb.rotation * Vector3.up;
            if (up.y < 0.74f) return true;
            var b = Base();
            if (b.y < -0.12f) return true;
            if (new Vector2(b.x - lugar.x, b.z - lugar.z).magnitude > 0.75f) return true;
            return false;
        }

        public void Tirar()
        {
            removido = true;
            Prender();
            _mr.enabled = false;
            _col.enabled = false;
            Colocar(lugar + new Vector3(0, -3f, 0));
            _estrelas.gameObject.SetActive(false);
        }

        public bool Parado => rb.isKinematic || (rb.velocity.magnitude <= 0.06f && rb.angularVelocity.magnitude <= 0.25f);

        // ------------------------------------------------------------ carinhas
        public void Expressao(string nome)
        {
            _expr = nome;
            Celula(nome);
        }

        void Celula(string nome)
        {
            int i = Array.IndexOf(EXPR, nome);
            if (i < 0) i = 0;
            _mat.SetVector(ID_CELULA, new Vector4(i % 4, i / 4, 0f, 0f));
        }

        void Escurecer(float k)
        {
            if (Mathf.Abs(k - _escuro) < 0.01f && (k < 1f || _escuro == 1f)) return;
            _escuro = k;
            _mat.SetFloat(ID_ESCURO, k);
        }

        public bool Tonto => _estrelas.gameObject.activeSelf;

        public void SemEstrelas() => _estrelas.gameObject.SetActive(false);

        public void FicarTonto()
        {
            if (removido) return;
            Expressao("tonto");
            _estrelas.gameObject.SetActive(true);
        }

        /// <summary>Caiu e ficou bravinho: cara brava e uma tremidinha de raiva.</summary>
        public void FicarBravo()
        {
            if (removido) return;
            Expressao("bravo");
            _estrelas.gameObject.SetActive(false);
            if (_raiva != null) StopCoroutine(_raiva);
            _raiva = StartCoroutine(Raiva());
        }

        IEnumerator Raiva()
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.1f, 0.5f));
            for (int i = 0; i < 3; i++)
            {
                yield return Girar(0.09f, 0.05f);
                yield return Girar(-0.09f, 0.07f);
            }
            yield return Girar(0f, 0.06f);
            _raiva = null;
        }

        IEnumerator Girar(float alvo, float dur)
        {
            float de = Angulo(_pe.localEulerAngles.x) * Mathf.Deg2Rad;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                _pe.localRotation = Quaternion.Euler(Mathf.Lerp(de, alvo, t / dur) * Mathf.Rad2Deg, 0, 0);
                yield return null;
            }
            _pe.localRotation = Quaternion.Euler(alvo * Mathf.Rad2Deg, 0, 0);
        }

        static float Angulo(float a) => a > 180f ? a - 360f : a;

        /// <summary>Ficou de pé: feliz. pulos &gt; 0: pulinhos de comemoração (agacha, pula,
        /// amortece), começando depois de `atraso`; no fim volta exatamente ao lugar.</summary>
        public void Comemorar(float atraso, int pulos)
        {
            if (removido) return;
            Expressao("feliz");
            if (pulos <= 0) return;
            PararPulo();
            Prender();
            _basePuloPos = rb.position;
            _basePuloRot = rb.rotation;
            _pulando = true;
            rb.interpolation = RigidbodyInterpolation.None;
            _pulo = StartCoroutine(Pulos(atraso, pulos));
        }

        IEnumerator Pulos(float atraso, int pulos)
        {
            yield return new WaitForSeconds(atraso);
            for (int i = 0; i < pulos; i++)
            {
                float h = i == 0 ? 0.16f : 0.1f;
                yield return Escala(new Vector3(1.08f, 0.86f, 1.08f), 0.08f, 0f, 0f);
                // estica e sobe ao mesmo tempo
                yield return Escala(new Vector3(0.94f, 1.1f, 0.94f), 0.07f, 0f, h * 0.41f);
                yield return Escala(new Vector3(0.94f, 1.1f, 0.94f), 0.10f, h * 0.41f, h, true);
                yield return Escala(Vector3.one, 0.17f, h, 0f, false, true);
                yield return Escala(new Vector3(1.07f, 0.9f, 1.07f), 0.06f, 0f, 0f);
                yield return Escala(Vector3.one, 0.1f, 0f, 0f);
            }
            PararPulo();
        }

        /// <summary>Um trecho do pulinho: escala do pé até `alvo` e altura de h0 até h1
        /// (subindo: desacelera; descendo: acelera, como quem cai).</summary>
        IEnumerator Escala(Vector3 alvo, float dur, float h0, float h1, bool sobe = false, bool desce = false)
        {
            Vector3 de = _pe.localScale;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                float u = t / dur;
                _pe.localScale = Vector3.Lerp(de, alvo, u);
                float k = sobe ? 1f - (1f - u) * (1f - u) : desce ? u * u : u;
                Altura(Mathf.Lerp(h0, h1, k));
                yield return null;
            }
            _pe.localScale = alvo;
            Altura(h1);
        }

        void Altura(float h)
        {
            var p = _basePuloPos + new Vector3(0, h, 0);
            rb.position = p;
            transform.position = p;
        }

        /// <summary>Interrompe a comemoração e põe o pino de volta onde estava.</summary>
        public void PararPulo()
        {
            if (_pulo != null) StopCoroutine(_pulo);
            _pulo = null;
            if (_raiva != null) StopCoroutine(_raiva);
            _raiva = null;
            if (_pe != null)
            {
                _pe.localRotation = Quaternion.identity;
                _pe.localScale = Vector3.one;
            }
            if (_pulando)
            {
                Teleportar(_basePuloPos, _basePuloRot);
                _pulando = false;
            }
        }

        // ------------------------------------------------------------ replay
        /// <summary>Replay: quem move é o jogo, quadro a quadro (sem física, sem interpolar).</summary>
        public void Replay(bool ligar)
        {
            if (ligar)
            {
                PararPulo();
                Prender();
                rb.interpolation = RigidbodyInterpolation.None;
            }
            else
            {
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
        }

        public void Pose(Vector3 pos, Quaternion rot)
        {
            rb.position = pos;
            rb.rotation = rot;
            transform.SetPositionAndRotation(pos, rot);
        }

        // ------------------------------------------------------------ quadro a quadro
        void FixedUpdate()
        {
            if (_voltaInterp > 0 && --_voltaInterp == 0 && !_pulando)
                rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void LateUpdate()
        {
            if (removido) return;
            // caindo no poço do alçapão: escurece com a fundura (lá embaixo é escuro)
            float y = BaseNaTela().y;
            if (y < Medidas.Y_CHAO_FOSSO - 0.05f || _escuro < 1f)
                Escurecer(Mathf.Clamp(1f + (y - Medidas.Y_CHAO_FOSSO + 0.05f) * 0.75f, 0.1f, 1f));
            // tontura: estrelinhas girando em volta da cabeça
            if (_estrelas.gameObject.activeSelf)
            {
                var topo = transform.TransformPoint(new Vector3(0, ALTURA, 0) - COM);
                _estrelas.position = topo + new Vector3(0, 0.1f, 0);
                _estrelas.Rotate(0, Time.deltaTime * 5f * Mathf.Rad2Deg, 0, Space.World);
                var cam = CameraCine.Atual;
                if (cam != null)
                {
                    var r = Quaternion.LookRotation(-cam.forward, cam.up);
                    for (int i = 0; i < 3; i++) _estrela[i].rotation = r;
                }
            }
            // piscar
            if (_piscando > 0f)
            {
                _piscando -= Time.deltaTime;
                if (_piscando <= 0f) Celula(_expr);
            }
            else if (_expr == "normal" || _expr == "feliz")
            {
                _piscar -= Time.deltaTime;
                if (_piscar <= 0f)
                {
                    _piscar = UnityEngine.Random.Range(1.5f, 4.5f);
                    _piscando = 0.12f;
                    Celula("piscando");
                }
            }
        }

        void OnCollisionEnter(Collision c)
        {
            float agora = Time.time;
            if (agora - _ultimoSom < 0.09f) return;
            _ultimoSom = agora;
            float v = rb.velocity.magnitude;
            if (c.rigidbody != null) v = Mathf.Max(v, c.rigidbody.velocity.magnitude);
            if (v > 0.35f) Bateu?.Invoke(this, v);
        }
    }
}
