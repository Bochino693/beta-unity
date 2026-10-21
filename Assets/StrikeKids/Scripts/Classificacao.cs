using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// CLASSIFICAÇÃO DO DIA: os melhores de hoje, cada um com a SUA FOTO (a da
    /// câmera da máquina; sem câmera, o mascote com que jogou). Aparece no fim
    /// da partida ("você ficou em 2º lugar hoje!") e na tela de atração. Vira a
    /// cada dia; o operador zera junto com os recordes.
    ///
    /// Fica em classificacao.json; as fotos em fotos/rank_*.png.
    /// </summary>
    public static class Classificacao
    {
        public const int MAXIMO = 10;

        public class Entrada
        {
            public int pontos;
            public string personagem = "";
            public string foto = "";      // caminho do PNG ("" = sem foto)
            public string hora = "";
            public string id = "";
            public string chaveFoto = "";  // só durante o registro
        }

        static List<Entrada> _lista;
        static string _dia = "";
        static int _versao, _gravada;
        static readonly object _trava = new object();

        static string Arquivo => Path.Combine(Ajustes.Pasta, "classificacao.json");

        public static string Hoje() => DateTime.Now.ToString("yyyy-MM-dd");

        static void Ler()
        {
            if (_lista != null && _dia == Hoje()) return;
            var lista = new List<Entrada>();
            string dia = "";
            try
            {
                if (File.Exists(Arquivo))
                {
                    var d = Json.Dic(Json.Ler(File.ReadAllText(Arquivo)));
                    dia = Json.Str(Json.Pegar(d, "dia"));
                    var l = Json.Lst(Json.Pegar(d, "lista"));
                    if (l != null)
                        foreach (var o in l)
                        {
                            var e = Json.Dic(o);
                            if (e == null) continue;
                            lista.Add(new Entrada
                            {
                                pontos = Json.Int(Json.Pegar(e, "pontos")),
                                personagem = Json.Str(Json.Pegar(e, "personagem")),
                                foto = Json.Str(Json.Pegar(e, "foto")),
                                hora = Json.Str(Json.Pegar(e, "hora")),
                                id = Json.Str(Json.Pegar(e, "id")),
                            });
                        }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("classificação: " + e.Message);
            }
            if (dia != Hoje())
            {
                ApagarFotos(lista);
                lista.Clear();
                dia = Hoje();
            }
            _lista = lista;
            _dia = dia;
        }

        /// <summary>Grava numa linha de trabalho separada (gravar na hora dava um tranco).</summary>
        static void Gravar()
        {
            var l = new List<object>();
            foreach (var e in _lista)
                l.Add(new Dictionary<string, object>
                {
                    ["pontos"] = e.pontos, ["personagem"] = e.personagem, ["foto"] = e.foto, ["hora"] = e.hora, ["id"] = e.id,
                });
            string texto = Json.Escrever(new Dictionary<string, object> { ["dia"] = _dia, ["lista"] = l }, true);
            int v = ++_versao;
            string arq = Arquivo;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                lock (_trava)
                {
                    if (v <= _gravada) return;
                    try
                    {
                        File.WriteAllText(arq, texto);
                        _gravada = v;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("classificação: não gravei: " + e.Message);
                    }
                }
            });
        }

        static void ApagarFotos(List<Entrada> lista)
        {
            foreach (var e in lista)
            {
                if (string.IsNullOrEmpty(e.foto)) continue;
                try { if (File.Exists(e.foto)) File.Delete(e.foto); } catch (Exception) { }
                Fotos.Esquecer(e.foto);
            }
        }

        /// <summary>Os de hoje, do melhor para o pior.</summary>
        public static List<Entrada> Lista()
        {
            Ler();
            return new List<Entrada>(_lista);
        }

        /// <summary>Registra os jogadores da partida. Volta a posição de cada um no dia (1 = primeiro).</summary>
        public static List<int> Registrar(List<Entrada> entradas)
        {
            Ler();
            string hora = DateTime.Now.ToString("HH:mm");
            long agora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var todos = new List<Entrada>(_lista);
            var novos = new List<Entrada>();
            for (int i = 0; i < entradas.Count; i++)
            {
                var e = entradas[i];
                var r = new Entrada { pontos = e.pontos, personagem = e.personagem, hora = hora, id = agora + "_" + i, chaveFoto = e.chaveFoto ?? "" };
                novos.Add(r);
                todos.Add(r);
            }
            // maior pontuação primeiro; empate: quem fez antes fica na frente
            var ordem = new List<Entrada>(todos);
            ordem.Sort((a, b) => a.pontos != b.pontos ? b.pontos.CompareTo(a.pontos) : todos.IndexOf(a).CompareTo(todos.IndexOf(b)));
            var posicoes = new List<int>();
            foreach (var r in novos) posicoes.Add(ordem.IndexOf(r) + 1);
            var fica = ordem.GetRange(0, Mathf.Min(MAXIMO, ordem.Count));
            if (ordem.Count > MAXIMO) ApagarFotos(ordem.GetRange(MAXIMO, ordem.Count - MAXIMO));
            foreach (var r in novos)
            {
                if (fica.Contains(r) && r.chaveFoto != "") r.foto = Fotos.Gravar(r.chaveFoto, "rank_" + r.id + ".png");
                r.chaveFoto = "";
            }
            _lista = fica;
            Gravar();
            return posicoes;
        }

        public static void Zerar()
        {
            Ler();
            ApagarFotos(_lista);
            _lista = new List<Entrada>();
            _dia = Hoje();
            Gravar();
        }
    }
}
