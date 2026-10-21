using System.Collections.Generic;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// CÂMERA DE CINEMA: planos com transição suave, seguir a bola, tremida,
    /// "soco" de zoom e enquadramento RESPONSIVO — cada plano diz quanto quer
    /// ver na vertical e na horizontal, e a câmera escolhe a abertura (FOV)
    /// que garante as duas coisas em qualquer proporção de tela (em pé ou
    /// deitada). A projeção vem da Tela (já girada para o monitor em pé).
    /// </summary>
    public class CameraCine : MonoBehaviour
    {
        /// <summary>A câmera 3D do jogo (os efeitos e as estrelinhas olham para ela).</summary>
        public static Transform Atual { get; private set; }

        public Camera cam;
        public float perto = 0.05f, longe = 160f;

        Vector3 _pos = new Vector3(0, 2, 6), _alvo = new Vector3(0, 0, -8);
        float _fovV = 50f, _fovH = 40f;
        Vector3 _posDest = new Vector3(0, 2, 6), _alvoDest = new Vector3(0, 0, -8);
        float _fovVDest = 50f, _fovHDest = 40f;
        float _rapidez = 3f;
        Transform _seguir;
        Vector3 _deslPos, _deslAlvo;
        float _tremor, _tremorDur, _soco;
        bool _orbita;
        Vector3 _orbitaCentro;
        float _orbitaRaio = 6f, _orbitaAltura = 2f, _orbitaAng, _orbitaVel = 0.15f;
        float _balanco, _balancoBase, _orbitaT;
        // movimento em trilho: pontos (pos, alvo, segundos) numa curva suave
        readonly List<Vector3> _trP = new List<Vector3>(), _trA = new List<Vector3>();
        readonly List<float> _trTempos = new List<float>();
        Vector3[] _trVp, _trVa;
        float _trT;
        bool _trilho;

        public static CameraCine Criar(Transform pai)
        {
            var go = new GameObject("Camera3D");
            go.transform.SetParent(pai, false);
            go.tag = "MainCamera";
            var c = go.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.4f, 0.6f, 0.9f);
            c.nearClipPlane = 0.05f;
            c.farClipPlane = 160f;
            c.depth = 0;
            c.allowHDR = false;
            c.allowMSAA = true;
            c.useOcclusionCulling = false;
            var cc = go.AddComponent<CameraCine>();
            cc.cam = c;
            Atual = go.transform;
            return cc;
        }

        void Awake()
        {
            if (cam == null) cam = GetComponent<Camera>();
            Atual = transform;
        }

        /// <summary>Corte seco para um plano.</summary>
        public void Corte(Vector3 pos, Vector3 alvo, float fovV = 50f, float fovH = 34f)
        {
            PararModos();
            _pos = _posDest = pos;
            _alvo = _alvoDest = alvo;
            _fovV = _fovVDest = fovV;
            _fovH = _fovHDest = fovH;
            Aplicar(0f);
        }

        /// <summary>Vai suavemente até o plano. rapidez: maior = chega mais rápido.</summary>
        public void Ir(Vector3 pos, Vector3 alvo, float fovV = 50f, float fovH = 34f, float rapidez = 3f)
        {
            PararModos();
            _posDest = pos;
            _alvoDest = alvo;
            _fovVDest = fovV;
            _fovHDest = fovH;
            _rapidez = rapidez;
        }

        /// <summary>Segue um objeto (bola, personagem) mantendo o deslocamento.</summary>
        public void Seguir(Transform no, Vector3 deslPos, Vector3 deslAlvo, float fovV = 50f, float fovH = 34f, float rapidez = 5f)
        {
            _orbita = false;
            _trilho = false;
            _seguir = no;
            _deslPos = deslPos;
            _deslAlvo = deslAlvo;
            _fovVDest = fovV;
            _fovHDest = fovH;
            _rapidez = rapidez;
        }

        public void Orbitar(Vector3 centro, float raio, float altura, float vel = 0.15f, float fovV = 45f, float fovH = 40f, float balanco = 0f)
        {
            PararModos();
            _orbita = true;
            _balanco = balanco;
            _orbitaT = 0f;
            _balancoBase = 0f;
            _orbitaCentro = centro;
            _orbitaRaio = raio;
            _orbitaAltura = altura;
            _orbitaVel = vel;
            _orbitaAng = Mathf.Atan2(_pos.x - centro.x, _pos.z - centro.z);
            _fovVDest = fovV;
            _fovHDest = fovH;
            _rapidez = 1.5f;
        }

        public struct Ponto
        {
            public Vector3 pos, alvo;
            public float tempo;
            public Ponto(Vector3 p, Vector3 a, float t)
            {
                pos = p;
                alvo = a;
                tempo = t;
            }
        }

        /// <summary>Movimento em trilho (replay, abertura): a câmera passa pelos pontos numa
        /// CURVA SUAVE (cúbica monótona): sai parada, passa pelos pontos do meio sem frear e
        /// chega parada.</summary>
        public void Trilho(IList<Ponto> pontos, float fovV = 45f, float fovH = 34f)
        {
            PararModos();
            _trilho = pontos.Count > 0;
            _trT = 0f;
            _fovVDest = _fovV = fovV;
            _fovHDest = _fovH = fovH;
            _trP.Clear();
            _trA.Clear();
            _trTempos.Clear();
            for (int i = 0; i < pontos.Count; i++)
            {
                _trP.Add(pontos[i].pos);
                _trA.Add(pontos[i].alvo);
                _trTempos.Add(i == 0 ? 0f : _trTempos[i - 1] + Mathf.Max(pontos[i].tempo, 0.001f));
            }
            _trVp = Velocidades(_trP);
            _trVa = Velocidades(_trA);
        }

        /// <summary>Velocidade em cada ponto (por eixo, Fritsch-Carlson): zero nas pontas e onde
        /// o movimento daquele eixo muda de sentido — a curva nunca passa do ponto.</summary>
        Vector3[] Velocidades(List<Vector3> p)
        {
            int n = p.Count;
            var v = new Vector3[n];
            for (int i = 1; i < n - 1; i++)
            {
                float h0 = _trTempos[i] - _trTempos[i - 1];
                float h1 = _trTempos[i + 1] - _trTempos[i];
                var d0 = (p[i] - p[i - 1]) / h0;
                var d1 = (p[i + 1] - p[i]) / h1;
                var m = Vector3.zero;
                for (int e = 0; e < 3; e++)
                {
                    if (d0[e] * d1[e] > 0f)
                    {
                        float w1 = 2f * h1 + h0, w2 = h1 + 2f * h0;
                        m[e] = (w1 + w2) / (w1 / d0[e] + w2 / d1[e]);
                    }
                }
                v[i] = m;
            }
            return v;
        }

        void NoTrilho(float t, out Vector3 pos, out Vector3 alvo)
        {
            int n = _trP.Count;
            if (n == 1 || t >= _trTempos[n - 1])
            {
                pos = _trP[n - 1];
                alvo = _trA[n - 1];
                return;
            }
            int i = 0;
            while (i < n - 2 && t > _trTempos[i + 1]) i++;
            float h = _trTempos[i + 1] - _trTempos[i];
            float u = Mathf.Clamp01((t - _trTempos[i]) / h);
            float u2 = u * u, u3 = u2 * u;
            float a = 2f * u3 - 3f * u2 + 1f, b = (u3 - 2f * u2 + u) * h, c = -2f * u3 + 3f * u2, d = (u3 - u2) * h;
            pos = _trP[i] * a + _trVp[i] * b + _trP[i + 1] * c + _trVp[i + 1] * d;
            alvo = _trA[i] * a + _trVa[i] * b + _trA[i + 1] * c + _trVa[i + 1] * d;
        }

        /// <summary>Quanto falta para o trilho acabar (segundos).</summary>
        public float FaltaTrilho => _trilho && _trTempos.Count > 0 ? Mathf.Max(0f, _trTempos[_trTempos.Count - 1] - _trT) : 0f;

        public void Tremer(float forca, float dur = 0.35f)
        {
            _tremor = Mathf.Max(_tremor, forca);
            _tremorDur = Mathf.Max(_tremorDur, dur);
        }

        /// <summary>"Soco" de zoom (fecha a abertura de repente e volta).</summary>
        public void Soco(float graus) => _soco = graus;

        /// <summary>Fica onde está (sem órbita, trilho nem perseguição): a imagem não muda.</summary>
        public void Congelar()
        {
            PararModos();
            _posDest = _pos;
            _alvoDest = _alvo;
            _fovVDest = _fovV;
            _fovHDest = _fovH;
        }

        void PararModos()
        {
            _seguir = null;
            _orbita = false;
            _trilho = false;
        }

        public Vector3 AlvoAtual => _alvo;
        public Vector3 PosAtual => _pos;

        void LateUpdate()
        {
            Aplicar(Time.deltaTime);
        }

        void Aplicar(float dt)
        {
            if (_trilho)
            {
                _trT += dt;
                NoTrilho(_trT, out _pos, out _alvo);
                _posDest = _pos;
                _alvoDest = _alvo;
            }
            else if (_orbita)
            {
                if (_balanco > 0f)
                {
                    _orbitaT += dt;
                    _orbitaAng = _balancoBase + Mathf.Sin(_orbitaT * _orbitaVel) * _balanco;
                }
                else _orbitaAng += _orbitaVel * dt;
                _posDest = _orbitaCentro + new Vector3(Mathf.Sin(_orbitaAng) * _orbitaRaio, _orbitaAltura, Mathf.Cos(_orbitaAng) * _orbitaRaio);
                _alvoDest = _orbitaCentro;
            }
            else if (_seguir != null)
            {
                // a bola aparece interpolada entre os passos da física: a câmera segue onde ela APARECE
                var g = _seguir.position;
                _posDest = g + _deslPos;
                _alvoDest = g + _deslAlvo;
            }
            if (dt > 0f && !_trilho)
            {
                float k = 1f - Mathf.Exp(-_rapidez * dt);
                _pos = Vector3.Lerp(_pos, _posDest, k);
                _alvo = Vector3.Lerp(_alvo, _alvoDest, k);
            }
            float kf = dt > 0f ? 1f - Mathf.Exp(-3f * dt) : 1f;
            _fovV = Mathf.Lerp(_fovV, _fovVDest, kf);
            _fovH = Mathf.Lerp(_fovH, _fovHDest, kf);
            _soco = Mathf.MoveTowards(_soco, 0f, dt * 30f);
            var tremor = Vector3.zero;
            if (_tremorDur > 0f)
            {
                _tremorDur -= dt;
                tremor = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * _tremor * Mathf.Clamp01(_tremorDur * 3f);
                if (_tremorDur <= 0f) _tremor = 0f;
            }
            var p = _pos + tremor;
            var olhar = _alvo + tremor * 0.5f - p;
            transform.position = p;
            if (olhar.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(olhar, Vector3.up);
            if (cam != null)
            {
                float fov = FovResponsivo() - _soco;
                // desenhando numa textura (o jeito do jogo): a imagem sai na tela lógica e quem gira
                // para o monitor em pé é a câmera das telas; direto na tela: a projeção já girada
                var T = Tela.Tamanho;
                cam.projectionMatrix = cam.targetTexture != null
                    ? Matrix4x4.Perspective(fov, T.x / Mathf.Max(T.y, 1f), perto, longe)
                    : Tela.Perspectiva(fov, perto, longe);
            }
        }

        /// <summary>A abertura vertical que mostra pelo menos _fovV na vertical E _fovH na
        /// horizontal, seja qual for a proporção da tela.</summary>
        float FovResponsivo()
        {
            var tam = Tela.Tamanho;
            float aspecto = tam.x / Mathf.Max(tam.y, 1f);
            float vPelaH = 2f * Mathf.Atan(Mathf.Tan(_fovH * Mathf.Deg2Rad * 0.5f) / Mathf.Max(aspecto, 0.01f)) * Mathf.Rad2Deg;
            return Mathf.Clamp(Mathf.Max(_fovV, vPelaH), 10f, 110f);
        }
    }
}
