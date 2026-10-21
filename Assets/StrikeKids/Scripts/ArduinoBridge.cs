using System;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// O ARDUINO DA MÁQUINA (sensores, botão de configuração, START/FICHA, trava
    /// da porta das bolas e fita de LED) — CONTRATO (provisório). A
    /// implementação completa (porte do ArduinoBridge.gd + plugin StrikeSerial)
    /// troca este arquivo mantendo a MESMA interface pública.
    /// </summary>
    public class ArduinoBridge : MonoBehaviour
    {
        public static ArduinoBridge I { get; private set; }

        public static event Action Mudou;
        /// <summary>Sensor 1..5 acionado (os sensores também viram as ações Z X C V B em Controles).</summary>
        public static event Action<int> Sensor;
        /// <summary>Aviso na tela antes da janela de permissão do Android ("" = some).</summary>
        public static event Action<string> JanelaAndroid;

        public static string Status = "Arduino: no PC o teclado faz tudo (A S D F G = sensores, 1 = START, 9 = configuração)";
        public static bool Conectado;
        public static string Calibracao = "";
        public static string Sensores = "";
        public static bool PortaAberta;
        public static bool PortaQuer;

        public static void Criar()
        {
            if (I != null) return;
            var go = new GameObject("ArduinoBridge");
            DontDestroyOnLoad(go);
            I = go.AddComponent<ArduinoBridge>();
        }

        public static bool TemPlugin() => false;
        /// <summary>Firmware novo (sensores, botão e porta) conectado?</summary>
        public static bool Completo() => false;
        public static void PedirPermissaoAgora() { }
        public static bool JanelaAberta() => false;
        /// <summary>Na tela de espera (ou no menu) as janelas do Android podem abrir.</summary>
        public static void NaEspera(bool sim = true) { }
        /// <summary>A trava da porta das bolas: abre depois do START, fecha no fim.</summary>
        public static void Porta(bool abrir) { PortaQuer = abrir; }
        public static void ReenviarPorta() { }
        public static void ModoTeste(bool sim) { }
        public static bool Calibrar() => false;
        public static string Relatorio() => "";
        /// <summary>MENU, BLUE, RED, IDLE, NORMAL, STRIKE, SPARE, MISS, OFF.</summary>
        public static void SendLed(string comando) { }

        static void Avisar(string t) => JanelaAndroid?.Invoke(t);
        static void Mudar() => Mudou?.Invoke();
        static void Acionou(int n) => Sensor?.Invoke(n);
    }
}
