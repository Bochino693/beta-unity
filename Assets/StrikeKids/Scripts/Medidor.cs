using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

namespace StrikeKids
{
    /// <summary>
    /// MEDIDOR DE DESEMPENHO — para saber, NA PRÓPRIA MÁQUINA (TV Box ou PC),
    /// se o jogo está rodando liso.
    /// 1) PAINEL NA TELA (F3 no PC, ou menu do operador &gt; "Mostrar desempenho
    ///    na tela"): quadros por segundo (QPS), tempo de cada quadro, o pior
    ///    quadro, desenhos e triângulos (quando a Unity informa), memória e a
    ///    resolução do 3D. Verde = liso.
    /// 2) TESTE DE DESEMPENHO (menu do operador): 18 segundos pelas vistas mais
    ///    pesadas; no fim mostra a média, o pior momento e a QUALIDADE
    ///    RECOMENDADA (na automática, já passa a usar ela). Fica gravado em
    ///    desempenho.txt na pasta do jogo.
    /// Como ler: 60 QPS = perfeito; 50+ = liso; 30-50 = joga, mas dá para ver
    /// travadinhas; menos de 30 = travado (baixe a qualidade).
    /// </summary>
    public class Medidor : MonoBehaviour
    {
        const float ATUALIZA = 0.5f;

        Principal _p;
        Caixa _painel;
        Rotulo _texto;
        float _t0;
        int _quadros;
        float _pior;
        float _ultimo;
        ProfilerRecorder _desenhos, _triangulos;

        public static Medidor Criar(Transform pai, Principal p)
        {
            var rt = Ui.Camada("medidor", pai);
            var m = rt.gameObject.AddComponent<Medidor>();
            m._p = p;
            m.Montar();
            return m;
        }

        void Montar()
        {
            _painel = Ui.Caixa(transform, new Color(0f, 0.03f, 0.1f, 0.78f), 10, 0, null, 0);
            _texto = Ui.Rotulo(_painel.transform, "", 28, Color.white, 4, new Color(0f, 0.03f, 0.1f));
            _texto.Alinhar(-1);
            Mostrar(Ajustes.Bool("video", "medidor"));
        }

        void OnEnable()
        {
            try
            {
                _desenhos = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
                _triangulos = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            }
            catch (Exception) { }
        }

        void OnDisable()
        {
            _desenhos.Dispose();
            _triangulos.Dispose();
        }

        public bool Visivel => Ui.Visivel(_painel);

        public void Mostrar(bool sim)
        {
            Ui.Ver(_painel, sim);
            _t0 = Time.realtimeSinceStartup;
            _ultimo = _t0;
            _quadros = 0;
            _pior = 0f;
        }

        public void Alternar() => Mostrar(!Ui.Visivel(_painel));

        void Update()
        {
            var T = Tela.Tamanho;
            Ui.Ret((RectTransform)transform, 0, 0, T.x, T.y);
            if (!Ui.Visivel(_painel)) return;
            float agora = Time.realtimeSinceStartup;
            _pior = Mathf.Max(_pior, (agora - _ultimo) * 1000f);
            _ultimo = agora;
            _quadros++;
            if (agora - _t0 < ATUALIZA) return;
            float ms = (agora - _t0) * 1000f / Mathf.Max(_quadros, 1);
            float qps = 1000f / Mathf.Max(ms, 0.001f);
            _texto.color = qps >= 50f ? Materiais.Hex("#7CFF7C") : (qps >= 30f ? Materiais.Hex("#FFE14D") : Materiais.Hex("#FF6B6B"));
            string desenhos = _desenhos.Valid ? _desenhos.LastValue + " desenhos · " : "";
            string tri = _triangulos.Valid ? (_triangulos.LastValue / 1000) + " mil triângulos · " : "";
            var rt = _p != null ? _p.Tamanho3D : Vector2Int.zero;
            string como = _p != null && _p.Direto ? "direto na tela" : Mathf.RoundToInt((_p != null ? _p.Fator : 1f) * 100f) + "%";
            _texto.text = string.Format("{0} QPS   ({1:0.0} ms, pior {2:0} ms)\n{3}{4}memória {5} MB\n3D {6}x{7} ({8}) · {9} · {10}",
                Mathf.RoundToInt(qps), ms, _pior, desenhos, tri, (int)(GC.GetTotalMemory(false) / 1048576),
                rt.x, rt.y, como, Nome(_p != null ? _p.Qualidade : ""), SystemInfo.graphicsDeviceName);
            float w = Mathf.Min(T.x - 24f, 1000f);
            Ui.Ret(_painel, 12, 12, w, 132);
            Ui.Ret(_texto, 16, 6, w - 32, 120);
            _texto.Tamanho(26f, 4f);
            _t0 = agora;
            _quadros = 0;
            _pior = 0f;
        }

        // ================================================================ teste
        /// <summary>Os planos mais pesados do jogo (coordenadas das edições Godot, convertidas).</summary>
        static readonly (string nome, Vector3 pos, Vector3 alvo, float fovV, float fovH)[] PLANOS =
        {
            ("a pista de frente", Medidas.G(0f, 1.05f, 2.2f), Medidas.G(0f, 0.18f, -9.9f), 34f, 40f),
            ("os pinos e o portal", Medidas.G(0f, 0.9f, -6.5f), Medidas.G(0f, 0.7f, -12.5f), 44f, 40f),
            ("os bichinhos", Medidas.G(0f, 1.35f, 6.6f), Medidas.G(0f, 1.0f, 2.3f), 40f, 44f),
            ("o cenário de cima", Medidas.G(-2.6f, 4.6f, -9.5f), Medidas.G(0f, 2.2f, -16.7f), 50f, 40f),
        };
        const float SEG_POR_PLANO = 4.5f;

        public class Resultado
        {
            public float mediaQps, piorQuadroMs, piorPlanoQps;
            public string piorPlano, qualidade, recomendada, veredito, texto;
            public RectTransform cartaz;
        }

        /// <summary>Roda o teste (o jogo precisa estar no estado TESTE). `pronto` recebe o resumo
        /// (null se o teste foi interrompido).</summary>
        public IEnumerator Testar(Action<Resultado> pronto)
        {
            bool antes = Ui.Visivel(_painel);
            Mostrar(true);
            var cartaz = Cartaz("TESTE DE DESEMPENHO\nmedindo... não mexa na máquina", out var aviso);
            var tempos = new List<float>();
            var porPlano = new Dictionary<string, float>();
            for (int i = 0; i < PLANOS.Length; i++)
            {
                var p = PLANOS[i];
                _p.mundo.AplicarTema(Temas.ORDEM[i % Temas.ORDEM.Length]);
                _p.cam.Corte(p.pos, p.alvo, p.fovV, p.fovH);
                for (int k = 0; k < 20; k++)
                {
                    yield return null;
                    if (!_p.EmTeste)
                    {
                        Interrompido(cartaz, antes);
                        pronto(null);
                        yield break;
                    }
                }
                float ini = Time.realtimeSinceStartup, ult = ini;
                var meus = new List<float>();
                while (Time.realtimeSinceStartup - ini < SEG_POR_PLANO)
                {
                    yield return null;
                    if (!_p.EmTeste)
                    {
                        Interrompido(cartaz, antes);
                        pronto(null);
                        yield break;
                    }
                    float agora = Time.realtimeSinceStartup;
                    meus.Add((agora - ult) * 1000f);
                    ult = agora;
                }
                tempos.AddRange(meus);
                porPlano[p.nome] = 1000f / Media(meus);
                aviso.text = "TESTE DE DESEMPENHO\nmedindo... " + (int)(100f * (i + 1) / PLANOS.Length) + "%";
            }
            tempos.Sort();
            float mediaQps = 1000f / Media(tempos);
            float p99 = tempos.Count > 0 ? tempos[Mathf.Min(tempos.Count - 1, (int)(tempos.Count * 0.99f))] : 0f;
            string piorPlano = "";
            float piorQps = 1e9f;
            foreach (var kv in porPlano)
                if (kv.Value < piorQps)
                {
                    piorQps = kv.Value;
                    piorPlano = kv.Key;
                }
            string qual = _p.Qualidade;
            string recomendada = qual, veredito;
            if (mediaQps >= 55f && piorQps >= 45f)
            {
                veredito = "ÓTIMO: roda liso";
                if (qual == "baixa") recomendada = "media";
                else if (qual == "media" && mediaQps >= 58f) recomendada = "alta";
            }
            else if (mediaQps >= 45f) veredito = "BOM: roda liso quase sempre";
            else if (mediaQps >= 30f)
            {
                veredito = "MÉDIO: dá para jogar, mas com travadinhas";
                recomendada = qual == "media" ? "baixa" : (qual == "alta" ? "media" : "baixa");
            }
            else
            {
                veredito = "FRACO: travado nesta qualidade";
                recomendada = "baixa";
            }
            string data = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            var rt = _p.Tamanho3D;
            var linhas = new System.Text.StringBuilder();
            linhas.AppendFormat("TESTE DE DESEMPENHO\n{0}\n\n{1}\n\nMédia: {2} quadros por segundo (QPS)\nPior momento: {3:0} ms ({4}: {5} QPS)\n" +
                "Qualidade usada: {6} (imagem 3D {7}x{8})\nRECOMENDADA: {9}\n\n", data, veredito, Mathf.RoundToInt(mediaQps), p99, piorPlano,
                Mathf.RoundToInt(piorQps), Nome(qual), rt.x, rt.y, Nome(recomendada));
            foreach (var kv in porPlano) linhas.AppendFormat("  {0}: {1} QPS\n", kv.Key, Mathf.RoundToInt(kv.Value));
            linhas.AppendFormat("\n{0} · {1}\n", SystemInfo.deviceModel, SystemInfo.graphicsDeviceName);
            try { File.WriteAllText(Path.Combine(Ajustes.Pasta, "desempenho.txt"), linhas.ToString()); }
            catch (Exception) { }
            Debug.Log(linhas.ToString());
            // na qualidade automática, já fica valendo a recomendada
            if (Ajustes.Texto("video", "qualidade") == "auto") Ajustes.Definir("video", "qualidade_auto", recomendada);
            aviso.text = linhas + "\nAPERTE START";
            Mostrar(antes);
            pronto(new Resultado
            {
                mediaQps = mediaQps, piorQuadroMs = p99, piorPlano = piorPlano, piorPlanoQps = piorQps, qualidade = qual,
                recomendada = recomendada, veredito = veredito, texto = linhas.ToString(), cartaz = cartaz,
            });
        }

        void Interrompido(RectTransform cartaz, bool antes)
        {
            if (cartaz != null) Destroy(cartaz.gameObject);
            Mostrar(antes);
        }

        public static string Nome(string q)
        {
            switch (q)
            {
                case "alta": return "ALTA";
                case "media": return "MÉDIA";
                case "baixa": return "BAIXA";
            }
            return (q ?? "").ToUpperInvariant();
        }

        static float Media(List<float> v)
        {
            if (v.Count == 0) return 1f;
            float s = 0f;
            foreach (float x in v) s += x;
            return s / v.Count;
        }

        RectTransform Cartaz(string texto, out Rotulo l)
        {
            var T = Tela.Tamanho;
            var fundo = Ui.Retangulo(transform, new Color(0f, 0.04f, 0.12f, 0.82f));
            Ui.Ret(fundo, 0, 0, T.x, T.y);
            l = Ui.Rotulo(fundo.transform, texto, 34, Color.white, 6, Ui.MARINHO);
            l.Quebrar = true;
            Ui.Ret(l, T.x * 0.05f, T.y * 0.05f, T.x * 0.9f, T.y * 0.9f);
            Ui.Frente(_painel);
            return fundo.rectTransform;
        }
    }
}
