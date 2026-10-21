using System.Collections.Generic;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// PONTUAÇÃO. Cada pino = 10 pontos. STRIKE (10 na 1ª bola) = +50.
    /// SPARE (completou os 10 depois) = +20. Números grandes: criança adora
    /// ver o placar subir!
    /// </summary>
    public class Placar
    {
        public const int PONTOS_PINO = 10;
        public const int BONUS_STRIKE = 50;
        public const int BONUS_SPARE = 20;

        public class Jogador
        {
            public string personagem;
            public List<List<int>> rodadas = new List<List<int>>();
            public int pontos, strikes, spares, pinos, seguidos;
        }

        public struct Resultado
        {
            public string tipo;     // "strike" | "spare" | "normal" | "zero"
            public int pontos;
            public bool fimRodada;
        }

        public readonly List<Jogador> jogadores = new List<Jogador>();
        public int rodadas = 3;
        public int tentativas = 3;
        public int jogadorAtual;
        public int rodadaAtual;     // 0..rodadas-1
        public int bolaAtual;       // 0..tentativas-1
        public int pinosNaRodada;

        public void Comecar(IList<string> personagens, int nRodadas, int nTentativas)
        {
            jogadores.Clear();
            foreach (var p in personagens)
            {
                var j = new Jogador { personagem = p };
                for (int r = 0; r < nRodadas; r++) j.rodadas.Add(new List<int>());
                jogadores.Add(j);
            }
            rodadas = nRodadas;
            tentativas = nTentativas;
            jogadorAtual = 0;
            rodadaAtual = 0;
            bolaAtual = 0;
            pinosNaRodada = 0;
        }

        public Jogador Atual => jogadores[jogadorAtual];

        /// <summary>Registra uma bola.</summary>
        public Resultado Registrar(int derrubados)
        {
            var j = Atual;
            j.rodadas[rodadaAtual].Add(derrubados);
            pinosNaRodada += derrubados;
            string tipo = "normal";
            int pontos = derrubados * PONTOS_PINO;
            if (derrubados == 0) tipo = "zero";
            if (pinosNaRodada >= 10)
            {
                if (bolaAtual == 0)
                {
                    tipo = "strike";
                    pontos += BONUS_STRIKE;
                    j.strikes++;
                }
                else
                {
                    tipo = "spare";
                    pontos += BONUS_SPARE;
                    j.spares++;
                }
            }
            j.pontos += pontos;
            j.pinos += derrubados;
            bool fim = pinosNaRodada >= 10 || bolaAtual + 1 >= tentativas;
            // strikes seguidos (rodada sem strike zera a sequência)
            if (tipo == "strike") j.seguidos++;
            else if (fim) j.seguidos = 0;
            return new Resultado { tipo = tipo, pontos = pontos, fimRodada = fim };
        }

        /// <summary>Passa para a próxima bola / rodada / jogador: "bola", "rodada" ou "fim".</summary>
        public string Avancar(bool fimRodada)
        {
            if (!fimRodada)
            {
                bolaAtual++;
                return "bola";
            }
            bolaAtual = 0;
            pinosNaRodada = 0;
            jogadorAtual++;
            if (jogadorAtual >= jogadores.Count)
            {
                jogadorAtual = 0;
                rodadaAtual++;
            }
            if (rodadaAtual >= rodadas)
            {
                rodadaAtual = rodadas - 1;
                return "fim";
            }
            return "rodada";
        }

        public int Total(int i) => jogadores[i].pontos;

        /// <summary>0 ou 1; -1 = empate.</summary>
        public int Vencedor()
        {
            if (jogadores.Count < 2) return 0;
            if (Total(0) == Total(1)) return -1;
            return Total(0) > Total(1) ? 0 : 1;
        }

        /// <summary>Pontuação máxima possível (para as estrelas do fim).</summary>
        public int Maximo() => rodadas * (10 * PONTOS_PINO + BONUS_STRIKE);

        public int Estrelas(int i)
        {
            float f = (float)Total(i) / Mathf.Max(Maximo(), 1);
            if (f >= 0.62f) return 3;
            if (f >= 0.38f) return 2;
            return 1;
        }
    }
}
