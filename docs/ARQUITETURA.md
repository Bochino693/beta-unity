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
```
