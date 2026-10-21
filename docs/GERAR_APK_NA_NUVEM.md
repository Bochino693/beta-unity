# Gerar o APK na nuvem — sem instalar a Unity no PC

O **GitHub gera o APK sozinho**, num computador dele, com a Unity 2022.3 e o
módulo Android já prontos (imagem do [GameCI](https://game.ci)). Você aperta
um botão no site do GitHub e, no fim, baixa o APK. Nada de Unity no seu PC.

O que roda lá é o mesmo do `GERAR_APK.bat`: configura OpenGL ES 2.0, calibra
as jogadas na primeira vez, assina com a chave do projeto e gera o APK.

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

## Toda vez que quiser um APK

1. No repositório, aba **Actions** › à esquerda, **Gerar APK (TV Box)** ›
   botão **Run workflow**.
   - **Giro**: `1` é o da TV Box deitada (o padrão do `GERAR_APK.bat`);
     `-1` gira para o outro lado; `0` é sem giro.
   - **Versão**: vazio = 6.0.
   - **ARM64**: deixe desmarcado (o APK sai em ARMv7, que roda na TX9 e em
     TV Box de 32 e de 64 bits, e gera na metade do tempo).
2. Clique em **Run workflow** e espere a bolinha ficar verde:
   - 1ª vez: uns **40 a 70 minutos** (a Unity importa tudo e calibra as
     jogadas com a física);
   - depois: uns **25 a 40 minutos** (a importação fica guardada).
3. Abra a execução e, **no fim da página, em “Artifacts”**, baixe
   **StrikeKids-Unity-APK** (um .zip com o `StrikeKids-Unity.apk` dentro).
4. Instale na TV Box como sempre (pendrive ou `adb install`).

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
