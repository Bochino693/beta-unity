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
* **Visual facetado**: malhas low-poly de faces retas e cores por vértice,
  feitas pelo modelador em Python (`tools/modelador`). Nada de SDF/massinha
  arredondada. Arquivos `.bytes` em `Resources/Malhas` (formato no
  `tools/modelador/formato.py` e em `Scripts/Malhas.cs`).
* **Tudo montado por código**: uma cena só (`Assets/StrikeKids/Cenas/Principal.unity`,
  criada pelo script de build) com o objeto `Jogo` (`Principal`). Mundo,
  pinos, bola, personagens, câmera e telas nascem em `Principal.Awake`.
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
* **Tela girada** (monitor em pé no HDMI deitado): a câmera 3D e a câmera
  das telas usam `projectionMatrix` girada 90° — sem imagem extra (nada de
  RenderTexture), o mais leve para a Mali-450. As telas (uGUI) ficam num
  Canvas "World Space" do tamanho lógico (lado menor = 1080).
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

## Pastas

```
Assets/StrikeKids/Scripts     o jogo (namespace StrikeKids)
Assets/StrikeKids/Editor      build do APK, calibração, importação
Assets/StrikeKids/Resources   malhas, texturas, telas, sons, fontes, shaders, dados
Assets/Plugins/Android        Java (atividade, Arduino, câmera) + manifesto
tools/modelador               modelador facetado (Python) + prévias
tools/verificar               compila o C# sem a Unity (referências do NuGet)
```
