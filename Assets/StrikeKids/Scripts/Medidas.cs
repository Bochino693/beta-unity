using System.Collections.Generic;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// MEDIDAS DO MUNDO 3D (pista, pinos, fosso, camadas da física).
    ///
    /// Linha de falta em z = 0; os pinos em z = -10,5 (pista infantil, mais
    /// curta que a de adulto: a bola chega rápido); a pista vai para -Z.
    ///
    /// COORDENADAS: as das edições Godot com o X ESPELHADO (a Unity é de mão
    /// esquerda): G(x, y, z) converte um ponto do Godot, GRot uma rotação YXZ.
    /// Quem joga fica em +Z olhando para -Z (os pinos); o personagem olha
    /// para o próprio +Z (transform.forward).
    /// </summary>
    public static class Medidas
    {
        public const float MEIA_LARGURA = 0.675f;
        public const float CANALETA = 0.25f;
        public const float Z_PINO1 = -10.5f;
        public const float ESPACO_PINOS = 0.38f;
        public const float FILEIRA = 0.329f;
        public const float Z_FIM_DECK = -11.95f;
        public const float Z_FUNDO = -13.7f;
        /// <summary>para-choques: 10 pedaços de 0,96 m a partir da linha de falta — acabam
        /// em z = -9,8, antes da barra varredora (z = -10) e do fosso</summary>
        public const int N_BUMPERS = 10;
        /// <summary>ALÇAPÃO no chão do fosso (o vão das duas portinhas): a máquina abre
        /// depois da varrida e os pinos caem num POÇO fundo de verdade.</summary>
        public const float ALCAPAO_X = 0.9f;
        public const float ALCAPAO_Z0 = -12.0f;
        public const float ALCAPAO_Z1 = -13.62f;
        public const float POCO_FUNDO = -1.75f;
        /// <summary>topo da boca da caçapa das bolas (no fundo do fosso)</summary>
        public const float Y_BOCA = 0.28f;
        public const float LARGURA_BOCA = 1.5f;
        public const int N_TIRAS = 7;
        public const float Y_CHAO_FOSSO = -0.14f;
        public const float ALTURA_PODIO = 0.152f;
        /// <summary>retorno de bolas (perto de quem joga; no Godot x = +2,35)</summary>
        public static readonly Vector3 RETORNO = G(2.35f, 0f, 5.2f);
        /// <summary>o símbolo da Lazer &amp; Sport de cada cenário, boiando ao lado da pista</summary>
        public static readonly Vector3 BALAO_POS = G(-1.75f, 2.2f, -7.5f);

        // ------------------------------------------------------------ conversão
        /// <summary>Um ponto das edições Godot nas coordenadas da Unity (X espelhado).</summary>
        public static Vector3 G(float x, float y, float z) => new Vector3(-x, y, z);

        public static Vector3 G(Vector3 v) => new Vector3(-v.x, v.y, v.z);

        /// <summary>Rotação YXZ em graus das edições Godot (Basis(Vector3)) na Unity.</summary>
        public static Quaternion GRot(float ax, float ay, float az) => Quaternion.Euler(ax, -ay, -az);

        public static Quaternion GRot(Vector3 graus) => Quaternion.Euler(graus.x, -graus.y, -graus.z);

        // ------------------------------------------------------------ camadas
        // (números de camada da Unity; os nomes não precisam existir)
        public const int CAMADA_MUNDO = 8;      // bate em tudo
        public const int CAMADA_PINOS = 9;
        public const int CAMADA_BOLA = 10;
        public const int CAMADA_SO_BOLA = 11;   // piso liso da bola, para-choques, túnel da caçapa
        public const int CAMADA_SO_PINOS = 12;  // piso dos pinos, poço, boca da caçapa (a bola passa)

        // ------------------------------------------------------------ física
        /// <summary>Passos da física por segundo (o mesmo das edições Godot e da calibração).</summary>
        public const int PASSOS_FISICA = 120;

        /// <summary>
        /// ATRITO E QUIQUE: a PhysX MULTIPLICA os valores dos dois corpos
        /// (Multiply, como a Bullet da edição Godot 3). Os números de cada corpo
        /// são convertidos para que os pares que importam saiam iguais aos do
        /// jogo original: pino x pino atrito 0,38 e quique 0,76; bola x pino 0,12 e 0,5.
        /// </summary>
        public const float ATRITO_PINO = 0.6164f;   // sqrt(0.38)
        public const float QUIQUE_PINO = 0.8718f;   // sqrt(0.76)
        public const float ATRITO_BOLA = 0.1947f;   // 0.12 / ATRITO_PINO
        public const float QUIQUE_BOLA = 0.5735f;   // 0.5 / QUIQUE_PINO

        static readonly Dictionary<string, PhysicMaterial> _mats = new Dictionary<string, PhysicMaterial>();

        static PhysicMaterial Novo(string nome, float atrito, float quique)
        {
            var m = new PhysicMaterial(nome)
            {
                dynamicFriction = atrito,
                staticFriction = atrito,
                bounciness = quique,
                frictionCombine = PhysicMaterialCombine.Multiply,
                bounceCombine = PhysicMaterialCombine.Multiply,
            };
            return m;
        }

        public static PhysicMaterial MaterialPino()
        {
            if (!_mats.TryGetValue("pino", out var m)) _mats["pino"] = m = Novo("pino", ATRITO_PINO, QUIQUE_PINO);
            return m;
        }

        public static PhysicMaterial MaterialBola()
        {
            if (!_mats.TryGetValue("bola", out var m)) _mats["bola"] = m = Novo("bola", ATRITO_BOLA, QUIQUE_BOLA);
            return m;
        }

        /// <summary>Material de uma superfície parada, com o atrito e o quique que ela tinha
        /// no jogo original. quem = quem encosta nela: "pinos", "bola" ou "todos" (aí fica
        /// no meio do caminho entre o certo para os pinos e para a bola).</summary>
        public static PhysicMaterial Material(float atrito, float quique, string quem = "todos")
        {
            string chave = atrito.ToString("0.###") + "|" + quique.ToString("0.###") + "|" + quem;
            if (_mats.TryGetValue(chave, out var pm)) return pm;
            float fp = Mathf.Min(atrito, 0.38f) / ATRITO_PINO;
            float fb = Mathf.Min(atrito, 0.12f) / ATRITO_BOLA;
            float rp = Mathf.Min(quique + 0.38f, 1f) / QUIQUE_PINO;
            float rb = Mathf.Min(quique + 0.12f, 1f) / QUIQUE_BOLA;
            float f, r;
            if (quem == "pinos") { f = fp; r = rp; }
            else if (quem == "bola") { f = fb; r = rb; }
            else { f = (fp + fb) * 0.5f; r = (rp + rb) * 0.5f; }
            // nenhum par passa de quique 1 (não ganha energia); a PhysX aceita até 1 por corpo
            r = Mathf.Min(r, 1f / QUIQUE_PINO, 1f);
            pm = Novo("parado " + chave, f, r);
            _mats[chave] = pm;
            return pm;
        }

        /// <summary>A física do jogo (vale também na calibração): passo de 1/120 s,
        /// mais iterações (pinos de pé estáveis), giro alto liberado e as camadas.</summary>
        public static void ConfigurarFisica()
        {
            Time.fixedDeltaTime = 1f / PASSOS_FISICA;
            Time.maximumDeltaTime = 0.1f;
            Physics.gravity = new Vector3(0f, -9.8f, 0f);
            Physics.defaultSolverIterations = 12;
            Physics.defaultSolverVelocityIterations = 4;
            Physics.bounceThreshold = 1.0f;
            Physics.sleepThreshold = 0.004f;
            Physics.defaultContactOffset = 0.006f;
            Physics.defaultMaxAngularSpeed = 100f;
            int[] todas = { 0, CAMADA_MUNDO, CAMADA_PINOS, CAMADA_BOLA, CAMADA_SO_BOLA, CAMADA_SO_PINOS };
            foreach (int a in todas)
                foreach (int b in todas)
                    Physics.IgnoreLayerCollision(a, b, true);
            void Ligar(int a, int b) => Physics.IgnoreLayerCollision(a, b, false);
            Ligar(CAMADA_PINOS, CAMADA_PINOS);
            Ligar(CAMADA_PINOS, CAMADA_BOLA);
            Ligar(CAMADA_PINOS, CAMADA_MUNDO);
            Ligar(CAMADA_PINOS, CAMADA_SO_PINOS);
            Ligar(CAMADA_BOLA, CAMADA_MUNDO);
            Ligar(CAMADA_BOLA, CAMADA_SO_BOLA);
        }
    }
}
