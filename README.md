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

* **Arduino da máquina — 2 versões, o mesmo jogo:** [`arduino/`](arduino/) —
  `strike_kids` (porta pelo **relé**/servo, sem ponte H, como a primeira) e
  `strike_kids_ponte_h` (porta com **motor pela ponte H**). Grave só a da sua
  montagem; como gravar em [`arduino/LEIA-ME.txt`](arduino/LEIA-ME.txt) e os
  desenhos da ligação em [`docs/`](docs/) (`ligacao_arduino.pdf` = relé,
  `ligacao_arduino_ponte_h.pdf` = ponte H).
* **Abrir no editor da Unity (PC com a Unity 2022.3):** Unity Hub › Adicionar
  projeto do disco › esta pasta (num caminho curto, ex. `C:\StrikeKids`). Na
  primeira vez o projeto se prepara sozinho (cena do jogo aberta, configuração
  da TV Box). **PLAY** joga no editor; menu **Strike Kids › Gerar APK (TV Box)**
  ou **› Gerar versão Windows** gera em `Build/` (precisa do módulo Android
  Build Support com OpenJDK e Android SDK & NDK Tools para o APK).
* **Baixar o jogo pronto (a última geração):**
  [APK da TV Box](https://github.com/Bochino693/beta-unity/releases/latest/download/StrikeKids-Unity.apk) ·
  [versão de Windows para testar no PC](https://github.com/Bochino693/beta-unity/releases/latest/download/StrikeKids-Windows.zip)
  — gerados sozinhos na nuvem a cada mudança do jogo e publicados em
  [Releases](https://github.com/Bochino693/beta-unity/releases) (precisa dos
  3 segredos da licença grátis da Unity, uma vez só — veja abaixo).
* **Gerar o APK e a versão de PC sem instalar a Unity (na nuvem):** aba
  **Actions** do GitHub › **Gerar APK (TV Box)** › **Run workflow** — o APK da
  TV Box e o `StrikeKids.exe` de Windows saem em *Artifacts*. Uma vez
  só, antes: os 3 segredos da licença Personal grátis — passo a passo em
  [`docs/GERAR_APK_NA_NUVEM.md`](docs/GERAR_APK_NA_NUVEM.md).
* **Gerar o APK no próprio PC (Windows):** instale o Unity Hub + Unity 2022.3.62f2 com
  *Android Build Support* (OpenJDK + Android SDK & NDK) e dê dois cliques no
  `GERAR_APK.bat`. O APK sai em `APK-Pronto/StrikeKids-Unity.apk`.
* Instruções completas: [`LEIA-ME.txt`](LEIA-ME.txt). Decisões técnicas:
  [`docs/ARQUITETURA.md`](docs/ARQUITETURA.md).
