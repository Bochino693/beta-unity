using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StrikeKids.Editor
{
    /// <summary>
    /// CALIBRAÇÃO DAS JOGADAS (física de verdade, sem tela, aqui no editor).
    /// Para cada posição de sensor, joga a bola em várias linhas, velocidades e
    /// efeitos (cada uma 2 vezes, saindo de pontos um pouco diferentes), com o
    /// rack completo, e anota quantos/quais pinos caíram. Grava
    /// Resources/Dados/tabela_jogadas.json (o planejador, Jogada.cs, usa).
    ///
    /// A tabela fica marcada com a versão da física (Medidas.VERSAO_FISICA): a
    /// geração do APK calibra de novo sozinha quando a física muda (ou com
    /// GERAR_APK.bat -Calibrar). A física é a PhysX da Unity, passo de 1/120 s
    /// — a mesma do jogo.
    /// </summary>
    public static class Calibrar
    {
        public const string ARQUIVO = "Assets/StrikeKids/Resources/Dados/tabela_jogadas.json";
        static readonly float[] VELOCIDADES = { 7f, 7.9f, 8.8f };
        static readonly float[] EFEITOS = { -0.12f, 0f, 0.12f };
        static readonly float[] SAIDAS = { 0.35f, 0.29f };
        static readonly string[] ZONAS = { "Z", "X", "C", "V", "B" };

        public static string Assinatura => "physx-" + Medidas.VERSAO_FISICA;

        /// <summary>A tabela que está no projeto é desta física?</summary>
        public static bool Precisa()
        {
            if (!File.Exists(ARQUIVO)) return true;
            try
            {
                var d = Json.Dic(Json.Ler(File.ReadAllText(ARQUIVO)));
                return d == null || Json.Str(Json.Pegar(d, "fisica")) != Assinatura;
            }
            catch (Exception)
            {
                return true;
            }
        }

        [MenuItem("Strike Kids/Calibrar as jogadas (física)")]
        static void Menu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Rodar(true);
        }

        static void Log(string t) => Debug.Log("[StrikeKids] " + t);

        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>Roda a calibração inteira (alguns minutos). progresso: barra do editor.</summary>
        public static bool Rodar(bool progresso)
        {
            var cenaAntes = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Medidas.ConfigurarFisica();
#pragma warning disable 618
            bool auto = Physics.autoSimulation;
            Physics.autoSimulation = false;
#pragma warning restore 618
            var raiz = new GameObject("calibracao").transform;
            bool ok = false;
            try
            {
                var mundo = Mundo.Criar(raiz);
                mundo.ConstruirFisica(true);
                var pinos = Pinos.Criar(raiz);
                var maquina = Maquina.Criar(raiz, pinos);   // as portinhas do alçapão fechadas, como no jogo
                maquina.Instantaneo();
                var bola = Bola.Criar(raiz);
                bola.Vestir("bob");
                bola.Guardar();
                Physics.SyncTransforms();
                var fila = new List<(string z, float x0, float xt, float v, float e, float zs)>();
                foreach (var z in ZONAS)
                {
                    float x0 = Jogada.LINHA[z];
                    for (int i = 0; i <= 50; i++)
                    {
                        float xt = Mathf.Round((-0.5f + i * 0.02f) * 1000f) / 1000f;
                        if (Mathf.Abs(xt - x0) > 0.62f) continue;
                        foreach (float v in VELOCIDADES)
                            foreach (float e in EFEITOS)
                                foreach (float zs in SAIDAS)
                                    fila.Add((z, x0, xt, v, e, zs));
                    }
                }
                Log("jogadas para simular: " + fila.Count);
                const float dt = 1f / Medidas.PASSOS_FISICA;
                var linhas = new List<string>(fila.Count);
                var inicio = DateTime.Now;
                var porZona = new Dictionary<string, (int n, int soma, int strikes)>();
                for (int k = 0; k < fila.Count; k++)
                {
                    var j = fila[k];
                    pinos.ReporTodos();
                    pinos.SoltarTodos();
                    bola.Guardar();
                    for (int p = 0; p < 6; p++) Physics.Simulate(dt);
                    // mesma linha (de x0 em z = 0,35 até xt nos pinos), saindo de zs
                    Jogada.Lancamento(j.x0, j.xt, j.v, j.zs, out var origem, out var vel);
                    bola.Lancar(origem, vel, j.e);
                    float t = 0f, xEntrada = 9f;
                    while (true)
                    {
                        bola.PassoFisica();
                        Physics.Simulate(dt);
                        t += dt;
                        var b = bola.rb.position;
                        if (xEntrada > 8f && b.z < Medidas.Z_PINO1 + 0.4f) xEntrada = b.x;
                        if (b.z < Medidas.Z_FIM_DECK - 0.3f || b.y < -0.4f || t > 5f) break;
                    }
                    float t2 = 0f;
                    while (true)
                    {
                        bola.PassoFisica();
                        Physics.Simulate(dt);
                        t2 += dt;
                        if (t2 > 3f || (t2 > 1.2f && pinos.TodosParados())) break;
                    }
                    var emPe = pinos.EmPe();
                    var caidos = new List<string>();
                    for (int n = 1; n <= 10; n++)
                        if (!emPe.Contains(n)) caidos.Add(n.ToString());
                    linhas.Add("[\"" + j.z + "\"," + F(j.x0) + "," + F(j.xt) + "," + F(j.v) + "," + F(j.e) + "," + caidos.Count + ",[" +
                        string.Join(",", caidos) + "]," + F(Mathf.Round(xEntrada * 1000f) / 1000f) + "]");
                    porZona.TryGetValue(j.z, out var pz);
                    porZona[j.z] = (pz.n + 1, pz.soma + caidos.Count, pz.strikes + (caidos.Count == 10 ? 1 : 0));
                    if ((k + 1) % 100 == 0)
                    {
                        Log((k + 1) + " prontas (" + (int)(DateTime.Now - inicio).TotalSeconds + " s)");
                        if (progresso && EditorUtility.DisplayCancelableProgressBar("Strike Kids", "Calibrando as jogadas: " + (k + 1) + " de " + fila.Count,
                                (k + 1f) / fila.Count))
                        {
                            Log("calibração cancelada");
                            return false;
                        }
                    }
                }
                var sb = new StringBuilder();
                sb.Append("{\"versao\":2,\"fisica\":\"").Append(Assinatura).Append("\",\"jogadas\":[");
                sb.Append(string.Join(",", linhas));
                sb.Append("]}");
                Directory.CreateDirectory(Path.GetDirectoryName(ARQUIVO));
                File.WriteAllText(ARQUIVO, sb.ToString());
                foreach (var kv in porZona)
                    Log(string.Format(CultureInfo.InvariantCulture, "zona {0}: {1} jogadas, média {2:0.0} pinos, {3} strikes ({4:0}%)", kv.Key,
                        kv.Value.n, (float)kv.Value.soma / kv.Value.n, kv.Value.strikes, 100f * kv.Value.strikes / kv.Value.n));
                Log("CALIBRACAO OK (" + (int)(DateTime.Now - inicio).TotalSeconds + " s)");
                ok = true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                if (progresso) EditorUtility.ClearProgressBar();
                UnityEngine.Object.DestroyImmediate(raiz.gameObject);
#pragma warning disable 618
                Physics.autoSimulation = auto;
#pragma warning restore 618
                AssetDatabase.ImportAsset(ARQUIVO, ImportAssetOptions.ForceUpdate);
                Jogada.Recarregar();
                if (!string.IsNullOrEmpty(cenaAntes) && File.Exists(cenaAntes)) EditorSceneManager.OpenScene(cenaAntes);
            }
            return ok;
        }
    }
}
