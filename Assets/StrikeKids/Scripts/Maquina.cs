using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// A MÁQUINA DE PINOS (pinsetter), como num boliche de verdade:
    ///   * BARRA varredora: desce na frente dos pinos (proteção), varre os
    ///     caídos para o fosso (empurrando de verdade, com física) e sobe.
    ///   * ALÇAPÃO: depois da varrida as duas portinhas do chão do fosso abrem
    ///     para baixo e os pinos caem no poço escuro (só com a bola já
    ///     recolhida; na jogada o chão fica sempre fechado).
    ///   * MESA: desce sobre os pinos, levanta os que ficaram em pé, espera a
    ///     varrida e devolve cada um no seu lugar. Num rack novo, desce já com
    ///     os 10 pinos encaixados e os deixa na pista.
    /// Tudo anda nos passos da física (corpos cinemáticos com MovePosition),
    /// então a barra empurra os pinos de verdade e tudo aparece liso.
    /// </summary>
    public class Maquina : MonoBehaviour
    {
        public event Action Terminou;
        public event Action<string> Som;
        /// <summary>o alçapão vai abrir (true: a câmera corta para o fosso) / já fechou (false)</summary>
        public event Action<bool> Alcapao;

        public const float Y_REPOUSO = 1.85f;
        public const float Y_ENCAIXE = 0.575f;   // altura da mesa com os pinos apoiados na pista
        public const float Y_LEVANTA = 0.95f;
        public const float Y_BARRA_CIMA = 1.72f;
        public const float Y_BARRA_BAIXO = 0.085f;
        public const float ABRE_ALCAPAO = 1.4f;  // quanto as portinhas giram para baixo (rad)

        public Pinos pinos;
        public bool trabalhando;
        /// <summary>&gt;1 = máquina mais rápida</summary>
        public float rapidez = 1.25f;

        Rigidbody _mesa, _barra;
        readonly Rigidbody[] _portas = new Rigidbody[2];
        readonly float[] _portaX = new float[2];
        readonly float[] _portaGiro = new float[2];
        float _zMesa, _zBarra;
        Vector3 _mesaPos, _barraPos;
        float _abre;    // 0 = portinhas fechadas, 1 = abertas

        class Carga
        {
            public Pino p;
            public Vector3 de, para;   // da mesa até a base do pino: onde estava / o lugar certo
        }

        readonly List<Carga> _carregando = new List<Carga>();
        float _centrar;
        int _seq;
        static readonly WaitForFixedUpdate PASSO = new WaitForFixedUpdate();
        Coroutine _ciclo;

        public Vector3 PosicaoMesa => _mesa.transform.position;

        public static Maquina Criar(Transform pai, Pinos p)
        {
            var go = new GameObject("Maquina");
            go.transform.SetParent(pai, false);
            var m = go.AddComponent<Maquina>();
            m.Montar(p);
            return m;
        }

        static Rigidbody Cinematico(GameObject go)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.useGravity = false;
            return rb;
        }

        void Montar(Pinos p)
        {
            pinos = p;
            _zMesa = Medidas.Z_PINO1 - Medidas.FILEIRA * 1.5f;
            _zBarra = Medidas.Z_PINO1 + 0.5f;
            var pc = Malhas.Ler("pista");
            // mesa (sem colisão: os pinos que ela carrega ficam "fantasmas")
            var mesa = Malhas.Objeto("mesa", pc.Malha("mesa"), Materiais.Facetado, transform);
            _mesa = Cinematico(mesa);
            _mesaPos = new Vector3(0, Y_REPOUSO, _zMesa);
            mesa.transform.position = _mesaPos;
            // barra varredora (corpo cinemático que empurra os pinos)
            var barra = Malhas.Objeto("barra", pc.Malha("barra"), Materiais.Facetado, transform);
            barra.layer = Medidas.CAMADA_MUNDO;
            var bc = barra.AddComponent<BoxCollider>();
            bc.size = new Vector3(Medidas.MEIA_LARGURA * 2f + 0.3f, 0.17f, 0.07f);
            bc.sharedMaterial = Medidas.Material(1f, 0f, "pinos");
            _barra = Cinematico(barra);
            _barraPos = new Vector3(0, Y_BARRA_CIMA, _zBarra);
            barra.transform.position = _barraPos;
            MontarAlcapao(pc);
        }

        /// <summary>As duas portinhas do alçapão: cada uma gira na dobradiça da beirada de
        /// fora; fechadas, o chão fica liso como o resto do fosso.</summary>
        void MontarAlcapao(Malhas.Pacote pc)
        {
            float comp = Medidas.ALCAPAO_Z0 - Medidas.ALCAPAO_Z1;
            var tam = new Vector3(Medidas.ALCAPAO_X - 0.004f, 0.04f, comp - 0.008f);
            var pm = Medidas.Material(0.9f, 0.05f, "pinos");
            for (int i = 0; i < 2; i++)
            {
                // a malha vai da dobradiça para -X: a da direita (x +) fica como está,
                // a da esquerda gira meia volta
                float s = i == 0 ? 1f : -1f;
                var porta = Malhas.Objeto("alcapao_" + i, pc.Malha("porta"), Materiais.Facetado, transform);
                porta.layer = Medidas.CAMADA_MUNDO;
                var bc = porta.AddComponent<BoxCollider>();
                bc.size = tam;
                bc.center = new Vector3(-tam.x / 2f, -tam.y / 2f, 0);
                bc.sharedMaterial = pm;
                _portas[i] = Cinematico(porta);
                _portaX[i] = s * Medidas.ALCAPAO_X;
                _portaGiro[i] = s > 0 ? 0f : 180f;
            }
            FecharPortas();
        }

        Quaternion GiroPorta(int i, float abre) => Quaternion.Euler(0, _portaGiro[i], 0) * Quaternion.Euler(0, 0, abre * ABRE_ALCAPAO * Mathf.Rad2Deg);

        Vector3 PosPorta(int i) => new Vector3(_portaX[i], Medidas.Y_CHAO_FOSSO, (Medidas.ALCAPAO_Z0 + Medidas.ALCAPAO_Z1) / 2f);

        /// <summary>Portinhas fechadas, na hora.</summary>
        void FecharPortas()
        {
            _abre = 0f;
            for (int i = 0; i < 2; i++)
            {
                var rb = _portas[i];
                rb.position = PosPorta(i);
                rb.rotation = GiroPorta(i, 0f);
                rb.transform.SetPositionAndRotation(rb.position, rb.rotation);
            }
        }

        void Teleportar(Rigidbody rb, Vector3 pos)
        {
            rb.position = pos;
            rb.transform.position = pos;
        }

        // ------------------------------------------------------------ passos da física
        void FixedUpdate()
        {
            foreach (var c in _carregando)
                c.p.Levar(_mesaPos + Vector3.Lerp(c.de, c.para, _centrar));
        }

        /// <summary>Um trecho animado (seno de ida e volta) nos passos da física.</summary>
        IEnumerator Tw(Func<float> de, Action<float> poe, float alvo, float dur, int s, int curva = 0)
        {
            float a = de();
            dur /= rapidez;
            for (float t = 0f; t < dur;)
            {
                yield return PASSO;
                if (s != _seq) yield break;
                t += Time.fixedDeltaTime;
                float u = Mathf.Clamp01(t / dur);
                float k = curva == 1 ? u * u : 0.5f - 0.5f * Mathf.Cos(u * Mathf.PI);   // 1 = acelera (cai)
                poe(Mathf.Lerp(a, alvo, k));
            }
        }

        IEnumerator Pausa(float dur, int s)
        {
            dur /= rapidez;
            for (float t = 0f; t < dur;)
            {
                yield return PASSO;
                if (s != _seq) yield break;
                t += Time.fixedDeltaTime;
            }
        }

        float MesaY() => _mesaPos.y;
        void MesaY(float y)
        {
            _mesaPos.y = y;
            _mesa.MovePosition(_mesaPos);
        }

        float BarraY() => _barraPos.y;
        void BarraY(float y)
        {
            _barraPos.y = y;
            _barra.MovePosition(_barraPos);
        }

        float BarraZ() => _barraPos.z;
        void BarraZ(float z)
        {
            _barraPos.z = z;
            _barra.MovePosition(_barraPos);
        }

        float Abre() => _abre;
        void Abre(float a)
        {
            _abre = a;
            for (int i = 0; i < 2; i++) _portas[i].MoveRotation(GiroPorta(i, a));
        }

        // ------------------------------------------------------------ ciclos
        /// <summary>Depois de uma bola que não fechou a rodada: guarda os pinos em pé, varre
        /// os caídos e devolve os em pé no lugar. derrubou: numa jogada errada o alçapão não abre.</summary>
        public void Reposicionar(bool derrubou = true) => Comecar(true, derrubou);

        /// <summary>Rack novo (rodada nova): varre tudo e desce os 10 pinos.</summary>
        public void RackNovo(bool derrubou = true) => Comecar(false, derrubou);

        void Comecar(bool manter, bool derrubou)
        {
            if (_ciclo != null) StopCoroutine(_ciclo);
            _seq++;
            _ciclo = StartCoroutine(Ciclo(manter, derrubou, _seq));
        }

        /// <summary>Pinos caídos lá no fosso (atrás da pista), que o alçapão faria cair.</summary>
        int PinosNoFosso(HashSet<Pino> levados)
        {
            int n = 0;
            foreach (var p in pinos.pinos)
            {
                if (levados.Contains(p) || p.removido) continue;
                var g = p.Base();
                if (g.z < Medidas.Z_FIM_DECK && g.z > Medidas.ALCAPAO_Z1 - 0.1f && Mathf.Abs(g.x) < Medidas.ALCAPAO_X + 0.05f && g.y < 0f) n++;
            }
            return n;
        }

        IEnumerator Ciclo(bool manter, bool derrubou, int s)
        {
            trabalhando = true;
            pinos.PrenderTodos();
            // 1. barra desce na frente (proteção)
            Som?.Invoke("barra");
            yield return Tw(BarraY, BarraY, Y_BARRA_BAIXO, 0.55f, s);
            if (s != _seq) yield break;
            // 2. mesa desce e levanta os pinos em pé (centralizando no lugar certo)
            _carregando.Clear();
            _centrar = 0f;
            if (manter)
            {
                var emPe = pinos.EmPe();
                if (emPe.Count > 0)
                {
                    Som?.Invoke("mesa");
                    yield return Tw(MesaY, MesaY, Y_ENCAIXE + 0.02f, 0.7f, s);
                    if (s != _seq) yield break;
                    foreach (int n in emPe)
                    {
                        var p = pinos.Numero(n);
                        var b = p.Base();
                        var onde = new Vector3(b.x, p.lugar.y, b.z);
                        p.Colocar(onde);
                        p.Fantasma(true);
                        _carregando.Add(new Carga { p = p, de = onde - _mesaPos, para = p.lugar - _mesaPos });
                    }
                    // sobe centralizando ao mesmo tempo
                    float c0 = _centrar;
                    float y0 = _mesaPos.y;
                    yield return Tw(() => 0f, u =>
                    {
                        MesaY(Mathf.Lerp(y0, Y_LEVANTA, u));
                        _centrar = Mathf.Lerp(c0, 1f, u);
                    }, 1f, 0.45f, s);
                    if (s != _seq) yield break;
                }
            }
            // 3. varre os caídos para o fosso (a barra empurra de verdade)
            var levados = new HashSet<Pino>();
            foreach (var c in _carregando) levados.Add(c.p);
            foreach (var p in pinos.pinos)
                if (!levados.Contains(p) && !p.removido) p.Soltar();
            Som?.Invoke("varrer");
            yield return Tw(BarraZ, BarraZ, Medidas.Z_FIM_DECK - 0.15f, 0.7f, s);
            if (s != _seq) yield break;
            // 3b. o ALÇAPÃO: as portinhas abrem para baixo e os pinos do fosso caem até o
            // fundo do poço — só se a bola derrubou pinos e há pino caído no fosso
            if (derrubou && PinosNoFosso(levados) > 0)
            {
                Alcapao?.Invoke(true);
                yield return Pausa(0.25f, s);
                if (s != _seq) yield break;
                Som?.Invoke("alcapao");
                yield return Tw(Abre, Abre, 1f, 0.45f, s, 1);
                if (s != _seq) yield break;
                yield return Pausa(0.85f, s);
                if (s != _seq) yield break;
                // a barra volta enquanto o alçapão fecha; os pinos lá embaixo saem do jogo
                float a0 = _abre, z0 = _barraPos.z;
                float dFecha = 0.55f, dBarra = 0.55f;
                float tt = 0f, fim = Mathf.Max(dFecha, dBarra) / rapidez;
                while (tt < fim)
                {
                    yield return PASSO;
                    if (s != _seq) yield break;
                    tt += Time.fixedDeltaTime;
                    float uf = Mathf.Clamp01(tt / (dFecha / rapidez));
                    float ub = Mathf.Clamp01(tt / (dBarra / rapidez));
                    Abre(Mathf.Lerp(a0, 0f, 0.5f - 0.5f * Mathf.Cos(uf * Mathf.PI)));
                    BarraZ(Mathf.Lerp(z0, _zBarra, 0.5f - 0.5f * Mathf.Cos(ub * Mathf.PI)));
                }
                yield return PASSO;
                FecharPortas();
                foreach (var p in pinos.pinos)
                    if (!levados.Contains(p)) p.Tirar();
                Alcapao?.Invoke(false);
            }
            else
            {
                foreach (var p in pinos.pinos)
                    if (!levados.Contains(p)) p.Tirar();
                yield return Tw(BarraZ, BarraZ, _zBarra, 0.5f, s);
                if (s != _seq) yield break;
            }
            // 4. mesa devolve (ou traz os 10 novos)
            if (!manter)
            {
                _mesaPos.y = Y_REPOUSO;
                Teleportar(_mesa, _mesaPos);
                _centrar = 1f;
                foreach (var p in pinos.pinos)
                {
                    p.Recolocar();
                    p.Fantasma(true);
                    var off = p.lugar - new Vector3(0, Y_ENCAIXE, _zMesa);
                    _carregando.Add(new Carga { p = p, de = off, para = off });
                    // já na mesa (sem aparecer um quadro na pista)
                    p.Colocar(_mesaPos + off);
                }
            }
            if (_carregando.Count > 0)
            {
                Som?.Invoke("mesa");
                yield return Tw(MesaY, MesaY, Y_ENCAIXE, manter ? 0.75f : 0.85f, s);
                if (s != _seq) yield break;
                foreach (var c in _carregando) c.p.Recolocar();
                _carregando.Clear();
                Som?.Invoke("pousar");
                pinos.AvisarPousou();
                yield return Tw(MesaY, MesaY, Y_REPOUSO, 0.6f, s);
                if (s != _seq) yield break;
            }
            // 5. barra sobe
            Som?.Invoke("barra");
            yield return Tw(BarraY, BarraY, Y_BARRA_CIMA, 0.5f, s);
            if (s != _seq) yield break;
            trabalhando = false;
            _ciclo = null;
            Terminou?.Invoke();
        }

        /// <summary>Sem animação (atração, começo).</summary>
        public void Instantaneo()
        {
            _seq++;
            if (_ciclo != null) StopCoroutine(_ciclo);
            _ciclo = null;
            _carregando.Clear();
            _centrar = 0f;
            _mesaPos = new Vector3(0, Y_REPOUSO, _zMesa);
            _barraPos = new Vector3(0, Y_BARRA_CIMA, _zBarra);
            Teleportar(_mesa, _mesaPos);
            Teleportar(_barra, _barraPos);
            FecharPortas();
            pinos.ReporTodos();
            trabalhando = false;
        }
    }
}
