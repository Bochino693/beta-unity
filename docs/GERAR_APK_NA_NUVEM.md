# Gerar o jogo na nuvem — sem instalar a Unity no PC

O **GitHub gera o jogo sozinho**, num computador dele, com a Unity 2022.3 já
pronta (imagens do [GameCI](https://game.ci)). Você aperta um botão no site do
GitHub e, no fim, baixa:

* **o APK da TV Box** (`StrikeKids-Unity-APK`);
* **a versão de Windows para testar no seu PC** (`StrikeKids-Windows`: é só
  descompactar e abrir o `StrikeKids.exe`).

Nada de Unity no seu PC.

O que roda lá é o mesmo do `GERAR_APK.bat`: configura o projeto (OpenGL ES 2.0
no APK), calibra as jogadas na primeira vez, assina o APK com a chave do
projeto e gera.

---

## Uma vez só (uns 10 minutos)

### 1. Conta Unity (grátis)

Crie em <https://id.unity.com> (se ainda não tem). A conta precisa ter
**e-mail e senha**: se você entrou com o Google, defina uma senha na página da
conta Unity.

### 2. O arquivo da licença Personal (grátis)

A Unity só entrega o arquivo da licença pelo **Unity Hub** — o programinha de
entrada da Unity (pequeno, **não é** a Unity inteira; pode desinstalar depois):

1. Baixe e instale o Unity Hub: <https://unity.com/download>
2. Abra e entre com a sua conta Unity.
3. Engrenagem (**Preferências**) › **Licenças** › **Adicionar** ›
   **Obter uma licença Personal gratuita** (é preciso clicar em *Adicionar*,
   senão o arquivo não é criado).
4. O arquivo fica em `C:\ProgramData\Unity\Unity_lic.ulf` (pasta escondida:
   no Explorador de Arquivos, **Exibir › Itens ocultos**). Abra com o Bloco de
   Notas e copie **o texto inteiro**.

> Não precisa instalar nenhuma versão da Unity no Hub — só a licença.

### 3. Os 3 segredos no GitHub

No repositório **Bochino693/beta-unity**: **Settings › Secrets and variables ›
Actions › New repository secret**, e crie estes três:

| Nome             | O que colar                          |
|------------------|--------------------------------------|
| `UNITY_LICENSE`  | o texto inteiro do `Unity_lic.ulf`   |
| `UNITY_EMAIL`    | o e-mail da conta Unity              |
| `UNITY_PASSWORD` | a senha da conta Unity               |

O GitHub guarda trancado: depois de salvo, ninguém vê o valor (nem eu).

---

## Toda vez que quiser gerar

1. No repositório, aba **Actions** › à esquerda, **Gerar APK (TV Box)** ›
   botão **Run workflow**.
   - **O que gerar**: `APK + Windows` (o padrão), só o APK ou só o Windows.
   - **Giro**: `1` é o da TV Box deitada (o padrão do `GERAR_APK.bat`);
     `-1` gira para o outro lado; `0` é sem giro.
   - **Versão**: vazio = 6.0.
   - **ARM64**: deixe desmarcado (o APK sai em ARMv7, que roda na TX9 e em
     TV Box de 32 e de 64 bits, e gera na metade do tempo).
2. Clique em **Run workflow** e espere a bolinha ficar verde:
   - 1ª vez: uns **40 a 70 minutos** (a Unity importa tudo e calibra as
     jogadas com a física);
   - depois: uns **25 a 40 minutos** (a importação fica guardada).
3. Abra a execução e, **no fim da página, em “Artifacts”**, baixe:
   - **StrikeKids-Unity-APK** — um .zip com o `StrikeKids-Unity.apk` dentro:
     instale na TV Box como sempre (pendrive ou `adb install`);
   - **StrikeKids-Windows** — um .zip com o jogo de PC (veja abaixo).
   Os arquivos ficam lá por 7 dias: baixe logo.

---

## Jogar no PC (testar sem a TV Box)

1. Descompacte o `StrikeKids-Windows.zip` numa pasta (por exemplo
   `C:\StrikeKids`) — **todos os arquivos juntos**: o `StrikeKids.exe` precisa
   da pasta `StrikeKids_Data` e dos .dll do lado.
2. Dê dois cliques em **StrikeKids.exe**. Se o Windows mostrar “O Windows
   protegeu o computador”, clique em **Mais informações › Executar assim
   mesmo** (o jogo não tem assinatura de loja).
3. Teclas (no lugar da máquina):

   | Tecla              | Faz                          |
   |--------------------|------------------------------|
   | **5**              | põe ficha (crédito)          |
   | **1** ou **Espaço**| START                        |
   | **A S D F G**      | os 5 sensores da pista       |
   | **9**              | menu do operador             |
   | **F3**             | medidor de desempenho (FPS)  |

   Abre em tela cheia; **Alt+Enter** sai da tela cheia, **Alt+F4** fecha.

---

## Se der erro

- **Vermelho no passo “Gerar o APK”**: abra o passo e procure a mensagem.
  - Fala de *license*, *activation* ou *serial*: confira os 3 segredos (o
    `UNITY_LICENSE` tem que ser o arquivo **inteiro**, do `<?xml` até o fim).
  - Qualquer outra coisa: copie as últimas linhas do registro e me mande.
- **Repositório privado**: o GitHub gratuito dá **2.000 minutos por mês** de
  Actions (cada APK gasta uns 30 a 60). Repositório público não tem limite.
- **Licença Personal**: grátis dentro das regras da Unity (limite de
  faturamento da empresa) — confira em <https://unity.com/products/unity-personal>.

O arquivo do processo é o `.github/workflows/gerar-apk.yml` (o mesmo método
de geração do `GERAR_APK.bat`: `StrikeKids.Editor.Construir.Apk`).
