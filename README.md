# Strike Kids — edição Unity (OpenGL ES 2.0)

Boliche infantil 3D (Bob & Mia) para máquina de arcade com TV Box —
feito na **Unity 2022.3 LTS**, desenhando com **OpenGL ES 2.0** (TV Box TX9,
Mali-400/450). Modelos novos, LISOS e em boa resolução (feitos por código em
`tools/modelador`: formas bem definidas, nada de massinha nem de aspecto
quadriculado), as 4 fases (Parque, Espaço com as navinhas, Fundo do Mar,
Selva dos Dinos) com o mesmo acabamento — sombreado dos cantinhos assado nos
modelos, pinos e bola envernizados, reflexo na pista —, física PhysX, o
Arduino da máquina (START, 5 sensores, ficha e configuração) e a câmera USB
para a foto das crianças.

* **Gerar o APK sem instalar a Unity (na nuvem):** aba **Actions** do GitHub ›
  **Gerar APK (TV Box)** › **Run workflow** — o APK sai em *Artifacts*. Uma vez
  só, antes: os 3 segredos da licença Personal grátis — passo a passo em
  [`docs/GERAR_APK_NA_NUVEM.md`](docs/GERAR_APK_NA_NUVEM.md).
* **Gerar o APK no próprio PC (Windows):** instale o Unity Hub + Unity 2022.3.62f2 com
  *Android Build Support* (OpenJDK + Android SDK & NDK) e dê dois cliques no
  `GERAR_APK.bat`. O APK sai em `APK-Pronto/StrikeKids-Unity.apk`.
* Instruções completas: [`LEIA-ME.txt`](LEIA-ME.txt). Decisões técnicas:
  [`docs/ARQUITETURA.md`](docs/ARQUITETURA.md).
