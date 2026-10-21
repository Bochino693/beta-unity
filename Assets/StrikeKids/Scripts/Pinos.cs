using System;
using System.Collections.Generic;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// OS 10 PINOS: montagem no triângulo, contagem dos caídos, carinhas e
    /// gravação para o replay. Quem varre e recoloca é a Maquina. A queda é
    /// 100% física: um pino derruba o outro, batem nas paredes e voltam,
    /// caem no fosso.
    ///
    /// Numeração oficial (vista de quem joga):
    ///          7   8   9   10
    ///            4   5   6
    ///              2   3
    ///                1
    /// </summary>
    public class Pinos : MonoBehaviour
    {
        public event Action<Vector3, float> Batida;
        public event Action SomPousar;

        public readonly List<Pino> pinos = new List<Pino>();

        Mesh _sombras;
        Vector3[] _sv = new Vector3[40];
        Color32[] _sc = new Color32[40];

        /// <summary>A base do pino n (1..10). X da Unity: o pino 7 fica à esquerda de quem
        /// joga (x positivo), o 10 à direita (x negativo) — como na pista de verdade.</summary>
        public static Vector3 PosicaoDoPino(int n)
        {
            int fileira = 0, primeiro = 1;
            while (n >= primeiro + fileira + 1)
            {
                primeiro += fileira + 1;
                fileira++;
            }
            int k = n - primeiro;
            float x = (k - fileira * 0.5f) * Medidas.ESPACO_PINOS;
            return Medidas.G(x, 0f, Medidas.Z_PINO1 - fileira * Medidas.FILEIRA);
        }

        public static Pinos Criar(Transform pai)
        {
            var go = new GameObject("Pinos");
            go.transform.SetParent(pai, false);
            var p = go.AddComponent<Pinos>();
            p.Montar();
            return p;
        }

        void Montar()
        {
            for (int n = 1; n <= 10; n++)
            {
                var p = Pino.Criar(transform, n, PosicaoDoPino(n));
                p.Bateu += (pino, f) => Batida?.Invoke(pino.transform.position, f);
                pinos.Add(p);
            }
            // sombrinhas no chão embaixo de cada pino (deitam junto com ele): uma malha, um desenho
            _sombras = new Mesh { name = "sombras_pinos" };
            _sombras.MarkDynamic();
            var uv = new Vector2[40];
            var tri = new int[60];
            for (int i = 0; i < 10; i++)
            {
                uv[i * 4] = new Vector2(0, 0);
                uv[i * 4 + 1] = new Vector2(1, 0);
                uv[i * 4 + 2] = new Vector2(1, 1);
                uv[i * 4 + 3] = new Vector2(0, 1);
                int k = i * 4;
                tri[i * 6] = k; tri[i * 6 + 1] = k + 2; tri[i * 6 + 2] = k + 1;
                tri[i * 6 + 3] = k; tri[i * 6 + 4] = k + 3; tri[i * 6 + 5] = k + 2;
            }
            _sombras.vertices = _sv;
            _sombras.uv = uv;
            _sombras.colors32 = _sc;
            _sombras.triangles = tri;
            _sombras.bounds = new Bounds(new Vector3(0, 0, Medidas.Z_PINO1), new Vector3(4, 2, 4));
            Malhas.Objeto("sombras_pinos", _sombras, Materiais.Sombra(0.5f), transform);
        }

        void LateUpdate()
        {
            for (int i = 0; i < pinos.Count; i++)
            {
                var p = pinos[i];
                // onde o pino APARECE neste quadro; a sombra é da BASE do pino
                var tp = p.transform;
                Vector3 g = p.BaseNaTela();
                float f = p.removido || g.y < -0.1f ? 0f : Mathf.Clamp01(1f - g.y * 2.2f);
                // deitado: a sombra fica comprida no sentido do pino
                Vector3 eixo = tp.up;
                float deitado = 1f - Mathf.Abs(eixo.y);
                bool naPista = Mathf.Abs(g.x) < Medidas.MEIA_LARGURA && g.z > Medidas.Z_FIM_DECK;
                // fora da pista não tem sombra (nem aparece no alçapão aberto); perto da
                // borda ela encolhe: o adesivo reto não sai da pista
                if (!naPista) f = 0f;
                else f *= Mathf.Clamp01((Medidas.MEIA_LARGURA - Mathf.Abs(g.x)) / 0.16f);
                Vector3 lado, frente;
                var h = new Vector3(eixo.x, 0, eixo.z);
                if (h.sqrMagnitude > 1e-6f) frente = h.normalized;
                else frente = Vector3.forward;
                lado = new Vector3(frente.z, 0, -frente.x);
                float w = 0.14f * f * (1f + deitado * 0.2f);
                float c = 0.14f * f * (1f + deitado * 1.6f);
                Vector3 centro = g + new Vector3(eixo.x, 0, eixo.z) * 0.19f * deitado;
                centro.y = 0.004f;
                int k = i * 4;
                _sv[k] = centro - lado * w - frente * c;
                _sv[k + 1] = centro + lado * w - frente * c;
                _sv[k + 2] = centro + lado * w + frente * c;
                _sv[k + 3] = centro - lado * w + frente * c;
                byte a = (byte)(f > 0f ? 255 : 0);
                _sc[k] = _sc[k + 1] = _sc[k + 2] = _sc[k + 3] = new Color32(255, 255, 255, a);
            }
            _sombras.vertices = _sv;
            _sombras.colors32 = _sc;
        }

        public List<int> EmPe()
        {
            var r = new List<int>();
            foreach (var p in pinos)
                if (!p.removido && !p.Caido()) r.Add(p.numero);
            return r;
        }

        public List<int> CaidosDesde(List<int> antes)
        {
            var agora = EmPe();
            var r = new List<int>();
            foreach (int n in antes)
                if (!agora.Contains(n)) r.Add(n);
            return r;
        }

        public Pino Numero(int n) => pinos[n - 1];

        /// <summary>Antes da bola chegar: libera a física dos pinos em pé.</summary>
        public void SoltarTodos()
        {
            foreach (var p in pinos)
                if (!p.removido) p.Soltar();
        }

        /// <summary>Carinha de medo quando a bola se aproxima.</summary>
        public void Susto(Vector3 bola)
        {
            foreach (var p in pinos)
            {
                if (p.removido || p.Caido()) continue;
                if (Vector3.Distance(p.transform.position, bola) < 3.2f && p.Expr == "normal") p.Expressao("medo");
            }
        }

        /// <summary>Replay: os pinos voltam a ter cara normal (sem estrelinhas).</summary>
        public void Reviver()
        {
            foreach (var p in pinos)
                if (!p.removido)
                {
                    p.Expressao("normal");
                    p.SemEstrelas();
                }
        }

        public bool TodosParados()
        {
            foreach (var p in pinos)
            {
                if (p.removido || p.Preso) continue;
                if (!p.Parado) return false;
            }
            return true;
        }

        /// <summary>Depois da jogada: os caídos ficam tontos ou bravinhos; os de pé
        /// comemoram — quanto mais sobraram, mais festa. Devolve quantos vão pular.</summary>
        public int Reagir()
        {
            var dePe = new List<Pino>();
            foreach (var p in pinos)
            {
                if (p.removido) continue;
                if (p.Caido())
                {
                    if (UnityEngine.Random.value < 0.55f) p.FicarTonto();
                    else p.FicarBravo();
                }
                else dePe.Add(p);
            }
            float chance = Mathf.Clamp(0.3f + dePe.Count * 0.06f, 0.35f, 0.85f);
            for (int i = dePe.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (dePe[i], dePe[j]) = (dePe[j], dePe[i]);
            }
            int pulando = 0;
            for (int i = 0; i < dePe.Count; i++)
            {
                int pulos = 0;
                if (UnityEngine.Random.value < chance)
                {
                    pulos = 1 + (UnityEngine.Random.value < 0.45f ? 1 : 0);
                    pulando++;
                }
                dePe[i].Comemorar(0.1f + i * 0.07f, pulos);
            }
            return pulando;
        }

        public void PrenderTodos()
        {
            foreach (var p in pinos)
            {
                p.PararPulo();
                p.Prender();
            }
        }

        /// <summary>Rack completo no lugar, sem animação.</summary>
        public void ReporTodos()
        {
            foreach (var p in pinos) p.Recolocar();
        }

        public void AvisarPousou() => SomPousar?.Invoke();

        // ------------------------------------------------------------ replay
        public void Foto(Vector3[] pos, Quaternion[] rot)
        {
            for (int i = 0; i < pinos.Count; i++)
            {
                pos[i] = pinos[i].rb.position;
                rot[i] = pinos[i].rb.rotation;
            }
        }

        public void Replay(bool ligar)
        {
            foreach (var p in pinos) p.Replay(ligar);
        }

        public void AplicarFoto(Vector3[] pos, Quaternion[] rot)
        {
            for (int i = 0; i < pinos.Count; i++)
                if (!pinos[i].removido) pinos[i].Pose(pos[i], rot[i]);
        }
    }
}
