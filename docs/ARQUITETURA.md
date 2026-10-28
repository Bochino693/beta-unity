# Strike Kids — edição Unity (OpenGL ES 2.0)

Porte completo do Strike Kids (edições Godot em `../strike-kids` e
`../strike-kids-gles2`) para a **Unity 2022.3 LTS** — a última linha da Unity
que ainda gera jogo em **OpenGL ES 2.0** (a TV Box TX9, Mali-450/400). A
partir da 2023.1 a Unity tirou o OpenGL ES 2.0.

## Decisões

* **Render pipeline embutido** (Built-in), sem luzes da Unity: a luz é
  calculada nos nossos shaders (`Resources/Shaders`, `#pragma target 2.0`)
  com variáveis globais (`_SkLuzDir`, `_SkLuzCor`, `_SkCeu`, `_SkChao`...),
  por vértice. Nada de sombras em tempo real: sombras "blob" (discos).
* **Gráfico caprichado sem pesar na Mali-450** (tudo cabe no OpenGL ES 2.0):
  - *sombreado dos cantinhos* (oclusão de ambiente) ASSADO na cor dos
    vértices pelo modelador (`tools/modelador/ao.py`): queixo, axilas,
    vãos das escamas, o fundo das canaletas e o miolo da máquina ficam mais
    escuros, como numa foto — custo zero no jogo;
  - *verniz*: pinos (`Brilho.shader`) e bola (`Bola.shader`) refletem um
    MATCAP feito na hora para cada fase (`Materiais.DefinirMatcap`: céu da
    fase, luzes do teto, a pista, o neon) — uma leitura de textura por pixel;
  - *reflexo na pista* (`Reflexos.cs`, `Reflexo.shader`): pinos e bola
    desenhados de novo, espelhados embaixo do verniz e sumindo com a
    profundidade. A pista vem antes (fila 1990), o reflexo (1995) só pinta
    onde fica atrás do que já está na tela (`ZTest Greater`) e o resto da
    cena cobre por cima. Desligado na qualidade baixa;
  - *vinheta* leve na imagem 3D (cor dos vértices de uma gradezinha, `Ui.Figura`);
  - antisserrilhado 4x (de graça nas placas de vídeo de ladrilhos como a Mali).
* **Modelos lisos em boa resolução**: malhas com cores por vértice e normais
  lisas por ângulo (superfície curva lisa, quina viva só onde é quina:
  `SUAVE_PADRAO` = 40°), com gomos e anéis a mais pela `RESOLUCAO` de cada
  tipo de modelo (bichinhos 2x, dinossauros 1,6x, cenário 1,4x) — nada de
  aspecto quadriculado; os pinos do reflexo usam uma malha mais leve.
  Antes eram malhas de faces retas e cores por vértice,
  feitas pelo modelador em Python (`tools/modelador`). Nada de SDF/massinha
  arredondada. Arquivos `.bytes` em `Resources/Malhas` (formato no
  `tools/modelador/malha.py`, função `gravar`, e em `Scripts/Malhas.cs`).
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
tools/modelador               modelador 3D (Python) + prévias + retratos
tools/verificar               compila o C# sem a Unity (referências do NuGet)
                              e confere os shaders (glslangValidator, HLSL)
tools/godot                   traz o cenário da edição Godot (exportar_cenario.gd)
                              e confere o plano contra ela (conferir_cenario.sh)
```
