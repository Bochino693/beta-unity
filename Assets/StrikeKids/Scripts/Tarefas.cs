using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// Utilidades de tempo: rodar algo no fim do quadro, no próximo quadro,
    /// depois de alguns segundos (do jogo ou de verdade), corrotinas soltas e
    /// respostas de linhas de trabalho de volta para a linha principal.
    /// </summary>
    public static class Tarefas
    {
        static Executor _ex;

        static Executor Ex
        {
            get
            {
                if (_ex == null)
                {
                    var go = new GameObject("Tarefas");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    go.hideFlags = HideFlags.HideInHierarchy;
                    _ex = go.AddComponent<Executor>();
                }
                return _ex;
            }
        }

        public static void NoFimDoQuadro(Action a) => Ex.fimDoQuadro.Add(a);

        public static void NoProximoQuadro(Action a) => Ex.proximo.Add(a);

        /// <summary>Depois de `segundos` do relógio do JOGO (anda com a câmera lenta e a velocidade).</summary>
        public static void Depois(float segundos, Action a) => Ex.timers.Add(new Timer { resta = segundos, real = false, acao = a });

        /// <summary>Depois de `segundos` de verdade.</summary>
        public static void DepoisReal(float segundos, Action a) => Ex.timers.Add(new Timer { resta = segundos, real = true, acao = a });

        /// <summary>De uma linha de trabalho: roda `a` na linha principal (no próximo quadro).</summary>
        public static void NaPrincipal(Action a)
        {
            lock (Ex.trava) Ex.daLinha.Add(a);
        }

        public static Coroutine Rodar(IEnumerator e) => Ex.StartCoroutine(e);

        public static void Parar(Coroutine c)
        {
            if (c != null && _ex != null) _ex.StopCoroutine(c);
        }

        /// <summary>Prepara o executor na linha principal (chame no começo).</summary>
        public static void Preparar()
        {
            var _ = Ex;
        }

        internal class Timer
        {
            public float resta;
            public bool real;
            public Action acao;
        }

        internal class Executor : MonoBehaviour
        {
            public readonly List<Action> fimDoQuadro = new List<Action>();
            public readonly List<Action> proximo = new List<Action>();
            public readonly List<Action> daLinha = new List<Action>();
            public readonly List<Timer> timers = new List<Timer>();
            public readonly object trava = new object();
            readonly List<Action> _rodar = new List<Action>();

            void Update()
            {
                if (proximo.Count > 0)
                {
                    _rodar.Clear();
                    _rodar.AddRange(proximo);
                    proximo.Clear();
                    Executar(_rodar);
                }
                lock (trava)
                {
                    if (daLinha.Count > 0)
                    {
                        _rodar.Clear();
                        _rodar.AddRange(daLinha);
                        daLinha.Clear();
                    }
                    else _rodar.Clear();
                }
                if (_rodar.Count > 0) Executar(_rodar);
                if (timers.Count > 0)
                {
                    float dt = Time.deltaTime;
                    float dr = Time.unscaledDeltaTime;
                    for (int i = timers.Count - 1; i >= 0; i--)
                    {
                        var t = timers[i];
                        t.resta -= t.real ? dr : dt;
                        if (t.resta <= 0f)
                        {
                            timers.RemoveAt(i);
                            try { t.acao(); } catch (Exception e) { Debug.LogException(e); }
                        }
                    }
                }
            }

            void LateUpdate()
            {
                if (fimDoQuadro.Count == 0) return;
                _rodar.Clear();
                _rodar.AddRange(fimDoQuadro);
                fimDoQuadro.Clear();
                Executar(_rodar);
            }

            static void Executar(List<Action> lista)
            {
                for (int i = 0; i < lista.Count; i++)
                {
                    try { lista[i](); } catch (Exception e) { Debug.LogException(e); }
                }
                lista.Clear();
            }

            void OnApplicationQuit()
            {
                Ajustes.GravarJa();
            }

            void OnApplicationPause(bool pausa)
            {
                if (pausa) Ajustes.GravarJa();
            }
        }
    }
}
