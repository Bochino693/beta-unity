# Strike Kids — edição Unity (OpenGL ES 2.0)

Porte completo do Strike Kids (edições Godot em `../strike-kids` e
`../strike-kids-gles2`) para a **Unity 2022.3 LTS** — a última linha da Unity
que ainda gera jogo em **OpenGL ES 2.0** (a TV Box TX9, Mali-450/400). A
partir da 2023.1 a Unity tirou o OpenGL ES 2.0.

## Decisões

* **Render pipeline embutido** (Built-in), sem luzes da Unity: a luz é
  calculada nos nossos shaders (`Resources/Shaders`, `#pragma target 2.0`)
  com variáveis globais, por vértice: a LUZ DE PALCO da edição Godot
  (`_luz_dir`, `_luz_cor`, `_ambiente`: pista, pinos, bichinhos, bola,
  gabinete — `Palco.DefinirLuz`), a do cenário (`_CnSolDir`, `_CnNeblina`:
  `Temas.Luz`) e a dos poucos shaders antigos que sobraram (`_SkLuzDir`...:
  sombras, partículas, reflexo). Nada de sombras em tempo real: sombras
  "blob" (discos).
* **Gráfico caprichado sem pesar na Mali-450** (tudo cabe no OpenGL ES 2.0):
  - o PALCO, os bichinhos, os pinos e a bola são os da edição Godot 7.6
    (ver abaixo): toon com borda iluminada, contorno, rosto por textura,
    estampas no shader, três níveis de detalhe;
  - *reflexo na pista* (`Reflexos.cs`, `Reflexo.shader`): pinos e bola
    desenhados de novo, espelhados embaixo do verniz e sumindo com a
    profundidade. A pista vem antes (fila 1990), o reflexo (1995) só pinta
    onde fica atrás do que já está na tela (`ZTest Greater`) e o resto da
    cena cobre por cima. Desligado na qualidade baixa;
  - *vinheta* leve na imagem 3D (cor dos vértices de uma gradezinha, `Ui.Figura`);
  - antisserrilhado 4x (de graça nas placas de vídeo de ladrilhos como a Mali).
* **Modelos da edição Godot**: tudo o que aparece (cenário, palco,
  bichinhos, pinos, bola) vem exportado do Godot 7.6 (`tools/godot`), nos
  mesmos triângulos. Arquivos `.bytes` em `Resources/Malhas` no formato SKP1
  (`Scripts/Malhas.cs`: partes com pai e pivô, posições em 16 bits, normais,
  cor RGBA, UV e UV2). O modelador antigo desta edição (`tools/modelador`)
  só faz hoje o casco de colisão dos pinos (`Malhas/pinos`, calibrado com a
  tabela de jogadas); os modelos antigos só com `--forcar`.
* **Tudo montado por código**: uma cena só (`Assets/StrikeKids/Cenas/Jogo.unity`,
  criada pelo `Editor/Construir.cs` na geração) com o objeto `Jogo`
  (`Principal` + `AudioListener`). Mundo, pinos, máquina, bola, personagens,
  câmeras e telas nascem em `Principal.Start`. As sequências (abertura,
  vez, jogada, replay, fim) são corrotinas canceladas pelo número da
  sequência (`_seq`), como os `yield` das edições Godot.
* **Coordenadas**: as do jogo Godot com o **X espelhado** (a Unity é de mão
  esquerda): `x_unity = -x_godot`; ângulos YXZ `(ax, ay, az) -> (ax, -ay, -az)`;
  giro em Y `θ -> -θ`. A pista vai para -Z (pinos em z = -10,5), o jogador
  fica em +Z, o personagem olha para +Z (`transform.forward`).
  `Medidas.G(x, y, z)` faz a conversão nos números portados.
* **Física**: PhysX da Unity, passo fixo de 1/120 s (como no Godot). A
  câmera lenta e o jogo mais rápido mudam só `Time.timeScale` (o passo
  continua 1/120 s: o resultado das jogadas não muda). Bola e pinos com
  interpolação.
* **Jogadas planejadas** (`Jogada.cs`): a mesma regra da edição Godot — a
  tabela `Resources/Dados/tabela_jogadas.json` diz, para cada lançamento,
  quantos pinos caem. Ela é refeita com a física da Unity pelo
  `Editor/Calibrar.cs` (o `GERAR_APK.bat` roda antes do build, se faltar).
* **Imagem 3D numa textura** (`RenderTexture`, como a Viewport das edições
  Godot): a câmera 3D desenha na tela LÓGICA, no tamanho da qualidade
  (alta 1080, média 720, baixa 540 linhas, vezes a nitidez automática de
  70% a 100%) com antisserrilhado 4x (2x na baixa); a imagem é a primeira
  peça das telas. Assim o 3D pode PARAR atrás dos modais (a TV Box só
  desenha o modal) e a resolução muda sem refazer a tela.
* **Tela girada** (monitor em pé no HDMI deitado): só a câmera das telas
  gira (`projectionMatrix` girada 90°, `Tela.Ortografica`) — ela leva junto
  a imagem 3D. As telas (uGUI montado por código, `Ui.cs`) ficam num Canvas
  "World Space" do tamanho lógico (lado menor = 1080, y das contas para
  baixo como no Godot), com placas desenhadas por malha (bordas lisas, sem
  textura por placa) e textos com contorno.
* **Controles: só o ARDUINO** (sem placa Zero Delay): os 5 sensores da
  pista (jogadas Z X C V B), START, FICHA (crédito) e CONFIGURAÇÃO chegam pelo
  USB serial (`ArduinoBridge` → `Controles.Injetar`). Um teclado também vale
  para testar (A S D F G, 1/Espaço, 5, 9) e o controle remoto da TV Box
  navega no menu do operador — as teclas chegam pela `StrikeAtividade`
  (Java, `Plugins/Android`), com o código cru do Android.
* **Arduino e câmera USB**: o Java dos plugins da edição Godot, sem a parte
  do Godot (`com.lazersport.strikekids.*`), chamado por `AndroidJavaClass`
  (o C# lê os eventos a cada quadro).
* **Ajustes, recordes, classificação**: JSON em `Application.persistentDataPath`
  (`Json.cs`, sem dependências).

## Cenário — o mesmo da edição Godot 7.6

Os 4 cenários são os da edição Godot que roda na TV Box
(`../beta/strike-kids-gles2`, `scripts/temas.gd` 7.6), trazidos sem
redesenhar nada:

* **Malhas e texturas exportadas do Godot** (`tools/godot/exportar_cenario.gd`):
  cada peça nos 3 níveis (`Malhas/cenario/<nome>`, `_meio` ~45% dos
  triângulos, `_longe` ~18%), o terreno com os morros e os PATAMARES das
  peças grandes (`Malhas/terreno`), o ATLAS de cada fase (4096², ETC1 com
  mipmaps: 8 MB + 2,7 MB na placa de vídeo), os fundos pintados do céu
  (4096x1024, ETC1) e os detalhes do chão (`chao_*`, pesos de cor: RGB sem
  perda, repetindo). No SKP1 o X vem trocado de sinal e o V da textura
  invertido; a ordem dos triângulos é a mesma.
* **O plano em C# puro** (`Scripts/Cenario/PlanoCenario.cs` + `Geometria.cs`):
  a cópia do `temas.gd` nas coordenadas do Godot, com as mesmas contas do
  Godot 3 (Basis por eixo e ângulo, Euler YXZ, `scaled`, a pose do Spatial)
  e o MESMO SORTEADOR (o PCG32 do `RandomNumberGenerator`, com o `randf` de
  precisão do Godot): as mesmas sementes dão as mesmas peças nos mesmos
  lugares, a conferência pelos pontos do modelo para nada entrar no morro, a
  pegada para nada atravessar nada, o lugar firme, os pilares, a escolha do
  nível pelo tamanho na tela, quem balança ao vento, quem se mexe, as vozes
  e as partículas.
* **Conferido peça por peça** (`tools/godot/conferir_cenario.sh`): o plano
  roda no .NET com as malhas exportadas e o Godot despeja o dele
  (`tools/godot/despejar_cenario.gd`) — 422 peças (579 no cenário completo),
  diferença máxima de 0,1 mm na posição e 0,0001 na base, as mesmas
  animações, vozes e partículas, nos 4 jeitos (leve/completo, com e sem os
  detalhes extras).
* **Na tela** (`Temas.cs`): cada nó do plano vira um GameObject com o X
  espelhado (`x_unity = -x_godot`, giro `(ax, -ay, -az)`); as peças paradas
  vão para o lote estático da Unity (`StaticBatchingUtility.Combine`), as que
  têm versão mais leve ficam no nó `alto` e a gêmea no `leve` (o nível do
  cenário: `NivelCenario`), as que se mexem trocam de malha. As animações
  são as do `_process` do `temas.gd`, nas poses do Godot; os sons de ambiente
  saem do lado da tela onde está o bicho (`Sons.TocarEm`).
* **Shaders** (as mesmas contas dos `toon_leve*.shader` do Godot, em
  `StrikeCenario.cginc`: `CnS2L`, `CnViva`, `CnTela`): `CenarioAtlas`
  (luz do material pronta na CPU — `Temas.Fatores` —, por pixel só a divisa
  sol/sombra e o atlas), `CenarioAtlasVento` (UV3: o quanto cada vértice
  balança), `CenarioAtlasAsas` (as asas do pterodáctilo no vértice),
  `CenarioCor` (nuvens brancas, fio dos balões), `Borboleta`, `Terreno`
  (o `terreno_leve`: listras, lua, areia, selva + os detalhes) e o `Ceu`
  (o `ceu_leve`: degradê por pixel, nebulosa, raios do mar e o fundo
  pintado). A luz do cenário (`Temas.Luz`) é a do Godot: sol e ambiente
  lineares com a força de cada fase e a neblina por vértice.
* **Ajustes**: `video/cenario` (auto = leve, como na TV Box), `video/cenario3d`
  (auto = leve nas Mali-400/450) e `video/detalhes` (vento, enfeites que se
  mexem, borboletas e as vozes).

## Palco, bichinhos, pinos e bola — os mesmos da edição Godot 7.6

Tudo o que fica em volta da pista e quem joga também veio da edição Godot,
sem redesenhar (`tools/godot/exportar_palco.gd`):

* **O palco** (`Scripts/Palco.cs`): a pista (a madeira, as setas e a trilha
  de estrelas prontas na textura, o neon das bordas, o brilho que acompanha a
  bola, o reflexo do portal no verniz), a madeira da aproximação, as
  canaletas com o desenho de cada fase (ondas, bolhas, estrelas, pegadas), as
  divisórias com a faixinha de luz, os para-choques listrados de doce, o poço
  do alçapão, o gabinete com as colunas e frisos, a cortina da bola com o
  desenho da fase, o fundo estrelado e o arco de raios atrás dos pinos, as
  lâmpadas do letreiro, as logos, os pódios da escolha, o símbolo Lazer &
  Sport de cada fase (balão, planetinha, bolha, pterossauro com a placa) e a
  máquina de pinos (mesa, barra varredora, portinhas do alçapão). O Godot
  monta o mundo (`Mundo`, `Pinos`, `Maquina`) e o exportador grava cada peça
  (`Resources/Palco/palco.json`: pai, lugar já na Unity, malha, material e o
  "papel" — o nome da variável no `mundo.gd`/`maquina.gd` —, mais o que muda
  em cada fase e a luz de palco de cada uma) e as malhas
  (`Resources/Malhas/palco.bytes`). As peças paradas já vêm juntadas pelo
  `Lote` do Godot (poucos desenhos por quadro). A física continua a desta
  edição (`Mundo.Fisica`, as mesmas medidas).
* **Os shaders do Godot** (`Resources/Shaders/Palco/G_<nome>`): `toon`,
  `toon_personagem` (rosto projetado de frente, estampa da roupa, escurecer
  no poço), `contorno` (casca invertida: um 2º material da peça), `pista`,
  `piso`, `canaleta`, `doce_listrado`, `poco`, `cortina`, `fundo_estrelas`,
  `raios`, `lampadas`, `sombra`, `aditivo`, `bola`, `astro3d`,
  `brilho_marca` e o `spatial` (os SpatialMaterial sem luz). Cada uniform
  `nome` do Godot é a propriedade `_nome` do material e os parâmetros ficam
  no espaço do Godot (o shader troca o X: `CnGodot`; o espaço da câmera é o
  mesmo nas duas edições). As animações a cada quadro são as do `_process`
  do `mundo.gd` (`Mundo.Update`: luzes da pista, ondas das canaletas, as
  tiras da cortina abrindo quando a bola passa, o símbolo flutuando, o pódio
  aceso, a baleia nadando e cantando).
* **Sol, lua e baleia 3D no céu** (`G_astro3d`): o objeto vai longe, na
  direção do sol do céu, sempre à mesma distância da câmera; raios girando,
  rostinho que pisca, a baleia do mar nada de um lado para o outro.
* **Bob e Mia** (`Scripts/Personagem.cs`): as mesmas juntas e malhas
  (`Malhas/<bicho>[_<roupa>][_hd|_longe]`), as 4 roupinhas, o rosto de cada
  expressão numa textura (`rosto_<bicho>`, 4 x 2 células: trocar de cara e
  piscar é trocar a célula), a estampa (estrela, bolinhas, listras) e os
  dados de cada pacote em `Resources/Palco/personagens.json`. Os três NÍVEIS
  pelo tamanho na tela (perto ~15 mil triângulos, meio ~6,8 mil, longe ~2,7
  mil): o de perto só para o bichinho maior na tela, inteiro nela, e com o
  quadro folgado (`Principal.OrcamentoBichos`). Fora do quadro a pose não é
  calculada (só a animação continua contando). As animações, a IK das pernas
  e a inércia já eram as mesmas.
* **Pinos** (`Pino.cs`, `Pinos.cs`): o pino de perto (1500 triângulos, com os
  detalhes extras), o de sempre (734) e o de longe (386) pela distância da
  câmera; a carinha pela textura `rosto_pino`; a colisão continua a
  calibrada desta edição.
* **Bola** (`Bola.cs`): a esfera do Godot (32 x 16) e a textura de cada
  bichinho no `G_bola`.
* **Governador do quadro**: medindo lento duas vezes seguidas, o primeiro
  passo (no automático) é o cenário 3D leve (`video/cenario3d_medido`, guardado
  para as próximas vezes); depois a qualidade e a nitidez, como antes.
* **Texturas** (`Resources/Texturas/palco`, no tamanho do jogo — o limite do
  `.import` do Godot): sem transparência em ETC1 (a madeira, as bolas, a
  cortina), com transparência sem perda (os rostos, a pista pronta, as
  logos); fora de potência de 2 vão para a potência mais perto.
* **Conferir**: o exportador roda numa CÓPIA do projeto Godot em que cada
  `ShaderMaterial` guarda os parâmetros (o Godot sem tela não guarda):
  `tools/godot/copia_que_guarda.sh` monta a cópia.

## Geração (Windows)

`GERAR_APK.bat` → `GERAR_APK.ps1`: acha o Unity 2022.3 (Hub), espelha o
projeto em `C:\StrikeKidsUnity` e roda
`Unity -batchmode -executeMethod StrikeKids.Editor.Construir.Apk`, que
configura o projeto (OpenGL ES 2.0 só, Gamma, IL2CPP ARMv7+ARM64, API 22+,
Android TV, chave `tools/android/strikekids.keystore`), cria a cena, calibra
as jogadas se a tabela não for da física atual (`Medidas.VERSAO_FISICA`) e
gera o APK. O giro de fábrica da tela vai em `Resources/Dados/fabrica.json`.

## Pastas

```
Assets/StrikeKids/Scripts     o jogo (namespace StrikeKids)
Assets/StrikeKids/Editor      build do APK, calibração, importação
Assets/StrikeKids/Resources   malhas, texturas, telas, sons, fontes, shaders, dados
Assets/Plugins/Android        Java (atividade, Arduino, câmera) + manifesto
tools/modelador               modelador 3D antigo (Python): hoje só a colisão dos pinos
tools/verificar               compila o C# sem a Unity (referências do NuGet)
                              e confere os shaders (glslangValidator, HLSL)
tools/godot                   traz o cenário (exportar_cenario.gd) e o palco, os
                              bichinhos, os pinos e a bola (exportar_palco.gd +
                              copia_que_guarda.sh) da edição Godot e confere o
                              plano do cenário contra ela (conferir_cenario.sh)
```
