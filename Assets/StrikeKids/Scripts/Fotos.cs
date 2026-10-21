using System;
using System.Collections.Generic;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// A CÂMERA DA MÁQUINA E AS FOTOS DAS CRIANÇAS — CONTRATO (provisório).
    /// A implementação completa (porte do Fotos.gd + plugin StrikeCamera) troca
    /// este arquivo mantendo a MESMA interface pública.
    /// </summary>
    public class Fotos : MonoBehaviour
    {
        public static Fotos I { get; private set; }
        public const int LADO = 384;

        public static event Action Mudou;
        /// <summary>Aviso na tela antes da janela de permissão do Android ("" = some).</summary>
        public static event Action<string> JanelaAndroid;

        public static string Status = "procurando";
        public static string NomeCamera = "";

        static readonly Dictionary<string, Texture2D> _fotos = new Dictionary<string, Texture2D>();

        public static void Criar()
        {
            if (I != null) return;
            var go = new GameObject("Fotos");
            DontDestroyOnLoad(go);
            I = go.AddComponent<Fotos>();
        }

        public static bool Pronta() => Status == "pronta";
        public static string TextoStatus() => "procurando...";
        public static void Iniciar() { }
        public static void ProcurarAgora() { }
        public static void PedirPermissaoAgora() { }
        public static bool JanelaAberta() => false;
        public static void NaEspera() { }
        public static void Preparar() { }
        public static bool PodeFotografar() => false;
        public static string Relatorio() => "";
        public static bool Ligar() => false;
        public static void Desligar(float atraso = 6f) { }
        /// <summary>A imagem ao vivo já recortada (quadrada), espelhada e girada.</summary>
        public static Texture AoVivo() => null;
        public static bool TemImagem() => false;
        /// <summary>Tira a foto agora ("j0", "j1"...) e guarda para a partida.</summary>
        public static Texture2D Tirar(string chave) => null;
        public static Texture2D Foto(string chave) => _fotos.TryGetValue(chave, out var t) ? t : null;
        public static void LimparPartida() => _fotos.Clear();
        /// <summary>Grava a foto da partida num PNG (fotos/nome) e volta o caminho ("" = sem foto).</summary>
        public static string Gravar(string chave, string nomeArquivo) => "";
        /// <summary>Foto gravada (classificação, recorde) — lida do disco uma vez e guardada.</summary>
        public static Texture2D Carregar(string caminho) => null;
        /// <summary>Some com a foto guardada desse caminho (o arquivo foi apagado).</summary>
        public static void Esquecer(string caminho) { }
        public static string SalvarRecorde(string chave, int jogadores) => "";
        public static Texture2D FotoRecorde(int jogadores) => null;
        public static void ApagarRecordes() { }

        static void Avisar(string t) => JanelaAndroid?.Invoke(t);
        static void Mudar() => Mudou?.Invoke();
    }
}
