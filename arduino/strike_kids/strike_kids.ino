// STRIKE KIDS - ARDUINO DA MAQUINA
// ================================
// Le os 5 SENSORES da pista, o botao START, o moedeiro (FICHA) e o botao de
// CONFIGURACAO, comanda a PORTA das bolas (ponte H, servo e/ou rele) e a
// fita de LED. Conversa com a TV Box pelo cabo USB (9600, linhas de texto).
// Edicao Unity: TODOS os botoes da maquina vem SO deste Arduino (nao tem
// mais a placa Zero Delay) - START, sensores, ficha e configuracao.
//
// Placas: Arduino Uno, Nano (original ou clone CH340), Pro Mini 5 V,
// Mega 2560 e Leonardo/Micro.
//
// LIGACOES (padrao - mude abaixo se precisar; desenho: docs/ligacao_arduino.pdf)
//   Sensores, da ESQUERDA para a DIREITA (olhando da crianca para os pinos):
//     S1 = D2   S2 = D3   S3 = D4 (centro)   S4 = D5   S5 = D7
//     Sensor optico/infravermelho com saida NPN, chave ou micro-switch:
//     o sinal no pino e o GND no GND do Arduino (o pino ja tem pull-up).
//     Sensor que precisa de 12 V: alimentacao pela fonte de 12 V, GND
//     comum com o Arduino, e SO o fio de sinal no pino (saida NPN).
//   Botao de CONFIGURACAO (entra e sai do menu do operador): A0 e GND
//   Botao START: A1 e GND
//   Moedeiro / FICHA (sem moedeiro, ligue "Modo livre" no menu): sinal
//     (COIN) em A2, GND no GND. Cada
//     pulso = 1 ficha (quantas fichas por partida: no menu do operador).
//     Moedeiro NA ou NF: os dois funcionam.
//   PORTA DAS BOLAS - use uma (ou mais) destas; todas recebem o mesmo
//   comando:
//     PONTE H (motor DC ou atuador linear; L298N, L9110S, BTS7960, TB6612,
//            DRV8871...):
//            IN1 = D10   IN2 = D12   ENA = D11 (Mega: D9)
//            ENA so para mudar a velocidade (PONTE_VELOCIDADE_*). No L298N
//            pode deixar o jumper do ENA (velocidade maxima) e nao ligar
//            o D11. BTS7960: RPWM = D10, LPWM = D12, R_EN e L_EN juntos no
//            D11 (ou no 5 V). TB6612: STBY no 5 V, PWMA = D11.
//            O motor nas saidas da ponte (OUT1/OUT2), a ponte na fonte de
//            12 V (nunca no 5 V do Arduino) e o GND da ponte no GND do
//            Arduino.
//            FIM DE CURSO (opcional; micro-switch com o C no GND e o NA no
//            pino): porta ABERTA = A3, porta FECHADA = A4. Com eles, ligue
//            PONTE_FIM_DE_CURSO: o motor anda ate o micro-switch. Sem eles
//            o motor anda o tempo de PONTE_TEMPO_ABRE_MS / _FECHA_MS (o
//            atuador linear para sozinho no fim do curso).
//            Abriu quando devia fechar? Inverta os fios do motor (OUT1 e
//            OUT2) ou troque PONTE_INVERTIDA. Com fim de curso NAO use a
//            "Porta invertida" do menu do jogo (ela trocaria os
//            micro-switches tambem).
//     SERVO: sinal no D9 (Mega: D11). Alimente o servo por uma fonte
//            de 5-6 V propria (servo grande puxa muita corrente) e ligue o
//            GND dessa fonte no GND do Arduino.
//     RELE:  D8 -> entrada (IN) de um modulo rele (para trava solenoide
//            ou eletroima). Veja RELE_LIGA_EM_LOW e RELE_TEMPO_MAX_S.
//   Fita de LED WS2812 (NeoPixel), opcional: D6
//   LED da placa (D13): pisca a cada sensor/botao acionado.
//
// A PORTA comeca FECHADA (ao ligar, a ponte H fecha a porta). O jogo manda
// ABRIR quando a partida comeca (START com ficha; no modo livre, so o
// START) e FECHAR no fim da partida; com outra ficha + START (ou outro
// START no modo livre) ela abre de novo. Se o jogo sumir (cabo solto, TV
// Box desligada) com a porta aberta, ela fecha sozinha em 15 s.
// (No modo livre com "comecar rolando a bola" ligado no menu do operador a
// porta fica aberta tambem na espera: rolar a bola e o START.)
//
// Sem fita de LED (ou sem a biblioteca "Adafruit NeoPixel" instalada):
// troque USAR_LEDS para 0.

#include <EEPROM.h>

// ------------------------------------------------------------ AJUSTES
#define USAR_LEDS 1          // 1 = tem fita de LED no D6 (biblioteca Adafruit NeoPixel)
#define LED_PINO 6
#define NUM_LEDS 60
#define BRILHO 180

const uint8_t SENSOR_PINOS[5] = {2, 3, 4, 5, 7};
#define CONFIG_PINO A0
#define START_PINO A1
#define FICHA_PINO A2
#define RELE_PINO 8
#define LED_PLACA 13

// Ponte H da porta (motor DC ou atuador linear).
#define USAR_PONTE_H 1            // 0 = sem ponte H (so servo/rele)
#define PONTE_IN1 10
#define PONTE_IN2 12
#define PONTE_INVERTIDA 0         // a porta abre quando devia fechar? troque para 1
#define PONTE_VELOCIDADE_ABRE 255 // 0 a 255 - so vale com o ENA ligado no pino (sem o jumper)
#define PONTE_VELOCIDADE_FECHA 255 // fechar mais devagar (ex.: 180) e mais seguro para as maos
#define PONTE_FIM_DE_CURSO 0      // 1 = tem os micro-switches de fim de curso (A3 e A4)
#define FC_ABERTA_PINO A3         // fim de curso da porta ABERTA
#define FC_FECHADA_PINO A4        // fim de curso da porta FECHADA
#define FC_NF 0                   // 1 = micro-switch ligado no NF (normalmente fechado)
#define PONTE_TEMPO_ABRE_MS 1500UL  // SEM fim de curso: tempo para abrir tudo (ponha um pouco a mais)
#define PONTE_TEMPO_FECHA_MS 1500UL // SEM fim de curso: tempo para fechar tudo
#define PONTE_TEMPO_MAX_MS 6000UL   // COM fim de curso: nao chegou neste tempo -> para o motor
#define PONTE_TENTATIVAS 3          // ... e tenta de novo, ate 3 vezes, a cada 3 s
#define PONTE_ESPERA_TRAVOU_MS 3000UL
#define PONTE_FREIO_MS 150UL      // ao parar: freia o motor este tempo e depois solta (0 = so solta)
#define PONTE_PAUSA_MS 300UL      // parada antes de inverter o sentido (protege a ponte e a caixa de reducao)

// Servo da trava: angulo com a porta FECHADA e ABERTA (0 a 180).
#define SERVO_FECHADA 0
#define SERVO_ABERTA 90
#define SERVO_GRAUS_POR_S 240     // velocidade do movimento
#define SERVO_SOLTA_PARADO 0      // 1 = para de segurar 1 s depois de chegar

// Rele da trava.
#define RELE_LIGA_EM_LOW 1        // modulo rele comum liga com LOW; o seu liga com HIGH? troque para 0
#define RELE_LIGADO_ABERTA 1      // 1 = rele ligado com a porta aberta; 0 = ligado com ela fechada
#define RELE_TEMPO_MAX_S 0        // 0 = sem limite. Trava SOLENOIDE comum nao pode ficar
                                  // ligada muito tempo: ponha 5 (liga 5 s e desliga)

#define FILTRO_SENSOR_MS 2        // o sinal precisa ficar assim este tempo
#define REARME_SENSOR_MS 120      // a mesma bola balancando no sensor nao conta 2 vezes
#define FILTRO_BOTAO_MS 25
#define FILTRO_FICHA_MS 5         // moedeiro manda pulsos curtos (20 ms ou mais)
#define SEM_JOGO_FECHA_MS 15000UL // sem noticia do jogo: fecha a porta
#define VERSAO "2"

#if USAR_LEDS
#include <Adafruit_NeoPixel.h>
Adafruit_NeoPixel fita(NUM_LEDS, LED_PINO, NEO_GRB + NEO_KHZ800);
#endif

#if defined(__AVR_ATmega2560__) || defined(__AVR_ATmega1280__)
#define SERVO_PINO 11             // OC1A no Mega
#define PONTE_EN 9                // PWM do Timer2 no Mega
#else
#define SERVO_PINO 9              // OC1A no Uno/Nano/Pro Mini/Leonardo
#define PONTE_EN 11               // PWM do Timer2 (Uno/Nano) ou Timer0 (Leonardo)
#endif
#define PASSOS_POR_US (F_CPU / 8000000UL)  // Timer1 com prescaler 8

// ------------------------------------------------------------ tipos
// TODOS os tipos ficam aqui no comeco, antes da primeira funcao: a IDE do
// Arduino escreve sozinha a "declaracao" de cada funcao logo antes da
// primeira funcao do arquivo; um tipo declarado mais abaixo ainda nao
// existiria ali (era o erro "'Efeito' was not declared in this scope").
enum Efeito { MENU, IDLE, STRIKE, MISS, SPARE, NORMAL, BLUE, RED, OFF };

// ------------------------------------------------------------ entradas
struct Entrada {
  uint8_t pino;
  uint8_t repouso;      // nivel lido sem nada acionando (HIGH com pull-up)
  bool leitura;         // acionado agora (sem filtro)
  bool estavel;         // acionado (filtrado)
  unsigned long mudouEm;
  unsigned long ultimoEvento;
  uint8_t filtro;
  uint16_t rearme;
  const char *evento;
};

Entrada sensores[5];
Entrada botoes[3];
const char *const NOMES_SENSOR[5] = {"S:1", "S:2", "S:3", "S:4", "S:5"};

bool streamSensores = false;   // TESTE:1 -> manda SENS: a cada mudanca
unsigned long ultimoComando = 0;
unsigned long piscarAte = 0;

// ------------------------------------------------------------ porta
bool portaAberta = false;
int16_t servoAtual10 = SERVO_FECHADA * 10;  // decimos de grau
int16_t servoAlvo10 = SERVO_FECHADA * 10;
unsigned long servoPasso = 0;
unsigned long servoChegou = 0;
bool servoSegurando = true;
unsigned long releLigouEm = 0;
bool releLigado = false;

// ponte H: o motor anda ate a porta chegar e so entao confirma ao jogo
bool ponteAlvoAberta = false;   // para onde a porta vai
int8_t ponteMov = 0;            // 1 = abrindo, -1 = fechando, 0 = parado
bool ponteNoAlvo = false;       // chegou (e ja confirmou)
uint8_t ponteFalhas = 0;        // com fim de curso: vezes seguidas que nao chegou
unsigned long ponteDesde = 0;   // comecou a andar / parou
unsigned long ponteFreioAte = 0;
int16_t pontePos = 1000;        // sem fim de curso: 0 = fechada ... 1000 = aberta, contado
int16_t ponteInicioPos = 1000;  // pelo tempo (ao ligar nao se sabe: conta aberta e fecha tudo)

// ------------------------------------------------------------ serial
char buf[24];
uint8_t bufLen = 0;

void enviarSensores();

// ============================================================ EEPROM
// endereco 0: 0x5B (marca)  1: niveis de repouso dos sensores (bit 1 = HIGH)
#define EE_MARCA 0
#define EE_REPOUSO 1

void carregarRepouso() {
  uint8_t mascara = 0x1F;  // padrao: todos em HIGH (pull-up, aciona em LOW)
  if (EEPROM.read(EE_MARCA) == 0x5B) mascara = EEPROM.read(EE_REPOUSO);
  for (uint8_t i = 0; i < 5; i++) sensores[i].repouso = (mascara >> i) & 1 ? HIGH : LOW;
}

void calibrar() {
  // le cada sensor varias vezes: o nivel de agora (pista vazia) e o repouso
  uint8_t mascara = 0;
  for (uint8_t i = 0; i < 5; i++) {
    uint8_t altos = 0;
    for (uint8_t k = 0; k < 15; k++) {
      if (digitalRead(sensores[i].pino) == HIGH) altos++;
      delay(2);
    }
    if (altos >= 8) mascara |= (1 << i);
  }
  EEPROM.update(EE_MARCA, 0x5B);
  EEPROM.update(EE_REPOUSO, mascara);
  carregarRepouso();
  for (uint8_t i = 0; i < 5; i++) {
    sensores[i].leitura = false;
    sensores[i].estavel = false;
  }
  Serial.print(F("CAL:OK:"));
  for (uint8_t i = 0; i < 5; i++) Serial.print(sensores[i].repouso == HIGH ? 'H' : 'L');
  Serial.println();
  enviarSensores();
}

// ============================================================ porta
uint16_t pulsoUs(int16_t graus10) {
  // como a biblioteca Servo: 544 us = 0 grau, 2400 us = 180 graus
  return 544 + (int32_t)graus10 * (2400 - 544) / 1800;
}

void servoIniciar() {
  digitalWrite(SERVO_PINO, LOW);
  pinMode(SERVO_PINO, OUTPUT);
  // Timer1 no modo 14 (PWM rapido, topo = ICR1), prescaler 8: o pulso sai
  // do proprio Timer1, firme mesmo quando a fita de LED segura o processador.
  TCCR1A = _BV(COM1A1) | _BV(WGM11);
  TCCR1B = _BV(WGM13) | _BV(WGM12) | _BV(CS11);
  ICR1 = 20000UL * PASSOS_POR_US - 1;  // 20 ms
  OCR1A = pulsoUs(servoAtual10) * PASSOS_POR_US;
  servoSegurando = true;
}

void servoSegurar(bool sim) {
  if (sim == servoSegurando) return;
  servoSegurando = sim;
  if (sim) {
    TCCR1A |= _BV(COM1A1);
  } else {
    TCCR1A &= ~_BV(COM1A1);
    digitalWrite(SERVO_PINO, LOW);
  }
}

void tickServo(unsigned long agora) {
  if (servoAtual10 != servoAlvo10) {
    if (agora - servoPasso >= 10) {
      int16_t passo = (int16_t)((uint32_t)SERVO_GRAUS_POR_S * 10UL * (agora - servoPasso) / 1000UL);
      if (passo < 1) passo = 1;
      servoPasso = agora;
      if (servoAtual10 < servoAlvo10) {
        servoAtual10 = min((int16_t)(servoAtual10 + passo), servoAlvo10);
      } else {
        servoAtual10 = max((int16_t)(servoAtual10 - passo), servoAlvo10);
      }
      servoSegurar(true);
      OCR1A = pulsoUs(servoAtual10) * PASSOS_POR_US;
      if (servoAtual10 == servoAlvo10) servoChegou = agora;
    }
  } else if (SERVO_SOLTA_PARADO && servoSegurando && agora - servoChegou > 1000) {
    servoSegurar(false);
  }
}

void releEscrever(bool ligado) {
  releLigado = ligado;
  bool nivel = ligado ? !RELE_LIGA_EM_LOW : RELE_LIGA_EM_LOW;
  digitalWrite(RELE_PINO, nivel ? HIGH : LOW);
}

void tickRele(unsigned long agora) {
  if (RELE_TEMPO_MAX_S > 0 && releLigado && agora - releLigouEm > (unsigned long)RELE_TEMPO_MAX_S * 1000UL) {
    releEscrever(false);  // protege a solenoide; a porta continua "aberta" para o jogo
  }
}

void confirmarPorta() {
  Serial.println(portaAberta ? F("PORTA:ABERTA") : F("PORTA:FECHADA"));
}

// ------------------------------------------------------------ ponte H
#if USAR_PONTE_H
// sentido: 1 = abre, -1 = fecha, 0 = solta o motor, 2 = freia
void ponteEscrever(int8_t sentido) {
  bool in1 = sentido == 2, in2 = sentido == 2;
  uint8_t vel = sentido == 2 ? 255 : 0;
  if (sentido == 1 || sentido == -1) {
    in1 = (sentido == 1) != (PONTE_INVERTIDA != 0);
    in2 = !in1;
    vel = sentido == 1 ? PONTE_VELOCIDADE_ABRE : PONTE_VELOCIDADE_FECHA;
  }
  digitalWrite(PONTE_IN1, in1 ? HIGH : LOW);
  digitalWrite(PONTE_IN2, in2 ? HIGH : LOW);
  analogWrite(PONTE_EN, vel);  // 0 = LOW, 255 = HIGH
}

#if PONTE_FIM_DE_CURSO
bool fcAcionado(uint8_t pino) {
  bool baixo = digitalRead(pino) == LOW;
  return FC_NF ? !baixo : baixo;
}

// le 3 vezes: um ruido do motor no fio nao para a porta no meio
bool fimDeCurso(bool aberta) {
  uint8_t pino = aberta ? FC_ABERTA_PINO : FC_FECHADA_PINO;
  for (uint8_t k = 0; k < 3; k++) {
    if (!fcAcionado(pino)) return false;
    delayMicroseconds(100);
  }
  return true;
}
#endif

bool ponteChegou(bool aberta) {
#if PONTE_FIM_DE_CURSO
  return fimDeCurso(aberta);
#else
  return pontePos == (aberta ? 1000 : 0);
#endif
}

void ponteParar(unsigned long agora) {
  ponteMov = 0;
  ponteDesde = agora;
  ponteEscrever(PONTE_FREIO_MS > 0 ? 2 : 0);
  ponteFreioAte = PONTE_FREIO_MS > 0 ? agora + PONTE_FREIO_MS : 0;
}

void ponteConfirmar() {
  ponteNoAlvo = true;
  ponteFalhas = 0;
  confirmarPorta();
}

// o jogo pediu: a porta vai para la (o tickPonte anda com o motor)
void ponteIr(bool abrir) {
  if (abrir == ponteAlvoAberta) {
    if (ponteNoAlvo) {
      confirmarPorta();   // ja esta la
      return;
    }
    if (ponteMov != 0) return;   // ja esta indo
  }
  ponteAlvoAberta = abrir;
  ponteNoAlvo = false;
  ponteFalhas = 0;   // pedido novo: tenta de novo mesmo se tinha travado
}

void tickPonte(unsigned long agora) {
  if (ponteFreioAte && (long)(agora - ponteFreioAte) >= 0) {
    ponteFreioAte = 0;
    ponteEscrever(0);   // acabou o freio: solta o motor
  }
  if (ponteMov != 0) {
    bool abrindo = ponteMov > 0;
    unsigned long passou = agora - ponteDesde;
#if !PONTE_FIM_DE_CURSO
    long andou = (long)(passou * 1000UL / (abrindo ? PONTE_TEMPO_ABRE_MS : PONTE_TEMPO_FECHA_MS));
    pontePos = (int16_t)constrain(ponteInicioPos + (abrindo ? andou : -andou), 0L, 1000L);
#endif
    if (abrindo != ponteAlvoAberta) {
      ponteParar(agora);   // o jogo mudou de ideia: para, espera e volta
    } else if (ponteChegou(abrindo)) {
      ponteParar(agora);
      ponteConfirmar();
    } else if (PONTE_FIM_DE_CURSO && passou >= PONTE_TEMPO_MAX_MS) {
      ponteParar(agora);   // o micro-switch nao chegou: algo segurando a porta?
      ponteFalhas++;
      Serial.println(F("AVISO:PORTA_TRAVOU"));
    }
    return;
  }
  if (ponteNoAlvo || ponteFalhas >= PONTE_TENTATIVAS) return;
  if (agora - ponteDesde < (ponteFalhas ? PONTE_ESPERA_TRAVOU_MS : PONTE_PAUSA_MS)) return;
  if (ponteChegou(ponteAlvoAberta)) {   // ja esta la (fim de curso acionado)
    ponteConfirmar();
    return;
  }
  ponteMov = ponteAlvoAberta ? 1 : -1;
  ponteDesde = agora;
  ponteInicioPos = pontePos;
  ponteFreioAte = 0;
  ponteEscrever(ponteMov);
}
#endif

void porta(bool abrir) {
  portaAberta = abrir;
  servoAlvo10 = (abrir ? SERVO_ABERTA : SERVO_FECHADA) * 10;
  servoPasso = millis();
  bool ligar = RELE_LIGADO_ABERTA ? abrir : !abrir;
  if (ligar && !releLigado) releLigouEm = millis();
  releEscrever(ligar);
#if USAR_PONTE_H
  ponteIr(abrir);    // confirma (PORTA:ABERTA/FECHADA) quando a porta chegar
#else
  confirmarPorta();
#endif
}

// ============================================================ entradas
void iniciarEntrada(Entrada &e, uint8_t pino, uint8_t filtro, uint16_t rearme, const char *evento) {
  e.pino = pino;
  e.repouso = HIGH;
  e.leitura = false;
  e.estavel = false;
  e.mudouEm = 0;
  e.ultimoEvento = 0;
  e.filtro = filtro;
  e.rearme = rearme;
  e.evento = evento;
  pinMode(pino, INPUT_PULLUP);
}

// o que ja esta acionado ao ligar (moedeiro NF, botao preso, bola parada no
// sensor) nao conta: so conta quando soltar e acionar de novo. (O Uno e o
// Nano reiniciam cada vez que o jogo abre o USB: antes, um moedeiro NF dava
// uma ficha de graca a cada vez.)
void assentarEntrada(Entrada &e) {
  e.leitura = digitalRead(e.pino) != e.repouso;
  e.estavel = e.leitura;
  e.mudouEm = millis();
}

// devolve true quando o estado filtrado mudou
bool tickEntrada(Entrada &e, unsigned long agora, bool &disparou) {
  disparou = false;
  bool ativo = digitalRead(e.pino) != e.repouso;
  if (ativo != e.leitura) {
    e.leitura = ativo;
    e.mudouEm = agora;
  }
  if (e.leitura != e.estavel && agora - e.mudouEm >= e.filtro) {
    e.estavel = e.leitura;
    if (e.estavel && (e.ultimoEvento == 0 || agora - e.ultimoEvento >= e.rearme)) {
      e.ultimoEvento = agora;
      disparou = true;
    }
    return true;
  }
  return false;
}

void enviarSensores() {
  Serial.print(F("SENS:"));
  for (uint8_t i = 0; i < 5; i++) Serial.print(sensores[i].estavel ? '1' : '0');
  Serial.println();
}

void piscar(unsigned long agora) {
  digitalWrite(LED_PLACA, HIGH);
  piscarAte = agora + 60;
}

// ============================================================ LEDs
#if USAR_LEDS
Efeito efeito = MENU;
unsigned long ledUltimo = 0;
uint16_t menuOffset = 0;
int ondaPos = 0, ondaDir = 1, ondaVolta = 0, ondaMax = 0, ondaMs = 38, ondaRastro = 8;
uint8_t cR = 0, cG = 60, cB = 220, rR = 0, rG = 0, rB = 140;
unsigned long strikeSegura = 0;

void setTodos(uint32_t cor) {
  for (int i = 0; i < NUM_LEDS; i++) fita.setPixelColor(i, cor);
  fita.show();
}

void setOnda(Efeito e, int maxV, int ms, int rastro, uint8_t cr, uint8_t cg, uint8_t cb, uint8_t rr, uint8_t rg, uint8_t rb) {
  efeito = e;
  strikeSegura = 0;
  ondaPos = 0; ondaDir = 1; ondaVolta = 0;
  ondaMax = maxV; ondaMs = ms; ondaRastro = rastro;
  cR = cr; cG = cg; cB = cb; rR = rr; rG = rg; rB = rb;
  ledUltimo = 0;
}

void ledComando(const char *c) {
  if (!strcmp(c, "MENU")) { efeito = MENU; menuOffset = 0; ledUltimo = 0; }
  else if (!strcmp(c, "BLUE")) { efeito = BLUE; setTodos(fita.Color(0, 40, 255)); }
  else if (!strcmp(c, "RED")) { efeito = RED; setTodos(fita.Color(255, 0, 0)); }
  else if (!strcmp(c, "IDLE")) setOnda(IDLE, 0, 42, 8, 0, 45, 180, 0, 0, 100);
  else if (!strcmp(c, "MISS")) setOnda(MISS, 5, 15, 14, 255, 0, 0, 170, 0, 0);
  else if (!strcmp(c, "SPARE")) setOnda(SPARE, 5, 24, 11, 255, 170, 0, 180, 95, 0);
  else if (!strcmp(c, "NORMAL")) setOnda(NORMAL, 3, 22, 10, 0, 180, 255, 0, 90, 190);
  else if (!strcmp(c, "STRIKE")) setOnda(STRIKE, 8, 6, 22, 0, 80, 255, 0, 0, 255);
  else if (!strcmp(c, "OFF")) { efeito = OFF; setTodos(0); }
}

void tickLeds(unsigned long agora) {
  if (strikeSegura && (long)(agora - strikeSegura) >= 0) {
    strikeSegura = 0;
    ledComando("IDLE");
  }
  if (efeito == MENU) {
    if (agora - ledUltimo < 20) return;
    ledUltimo = agora;
    // arco-iris andando (contas inteiras: rapido, nao atrasa os sensores)
    for (int i = 0; i < NUM_LEDS; i++) {
      uint16_t h = menuOffset + (uint16_t)((uint32_t)i * 65536UL / NUM_LEDS);
      fita.setPixelColor(i, fita.gamma32(fita.ColorHSV(h, 255, 255)));
    }
    fita.show();
    menuOffset += 420;
    return;
  }
  if (efeito == OFF || efeito == BLUE || efeito == RED) return;
  if (agora - ledUltimo < (unsigned long)ondaMs) return;
  ledUltimo = agora;
  fita.clear();
  for (int r = 0; r < ondaRastro; r++) {
    int pos = ondaPos - r * ondaDir;
    if (pos < 0 || pos >= NUM_LEDS) continue;
    if (r == 0) {
      fita.setPixelColor(pos, fita.Color(cR, cG, cB));
    } else {
      uint16_t t = 256 - (uint16_t)r * 256 / ondaRastro;
      fita.setPixelColor(pos, fita.Color((uint16_t)rR * t >> 8, (uint16_t)rG * t >> 8, (uint16_t)rB * t >> 8));
    }
  }
  fita.show();
  if (efeito == STRIKE) {
    ondaPos += ondaDir;
    if (ondaPos >= NUM_LEDS - 1) { ondaPos = NUM_LEDS - 1; ondaDir = -1; ondaVolta++; }
    else if (ondaPos <= 0) { ondaPos = 0; ondaDir = 1; ondaVolta++; }
  } else {
    ondaPos++;
    if (ondaPos >= NUM_LEDS + ondaRastro) { ondaPos = 0; ondaVolta++; }
  }
  if (ondaMax > 0 && ondaVolta >= ondaMax) {
    if (efeito == STRIKE) {
      setTodos(fita.Color(0, 50, 255));
      efeito = BLUE;
      strikeSegura = agora + 750;
      return;
    }
    ledComando("IDLE");
  }
}
#endif

// ============================================================ comandos
void executar(const char *c) {
  ultimoComando = millis();
  if (!strcmp(c, "PING")) {
    Serial.println(F("PONG:STRIKE_KIDS:" VERSAO));
  } else if (!strcmp(c, "PORTA:ABRE")) {
    porta(true);
  } else if (!strcmp(c, "PORTA:FECHA")) {
    porta(false);
  } else if (!strcmp(c, "PORTA?")) {
    Serial.println(portaAberta ? F("PORTA:ABERTA") : F("PORTA:FECHADA"));
  } else if (!strcmp(c, "SENS?")) {
    enviarSensores();
  } else if (!strcmp(c, "TESTE:1")) {
    streamSensores = true;
    enviarSensores();
  } else if (!strcmp(c, "TESTE:0")) {
    streamSensores = false;
  } else if (!strcmp(c, "CALIBRA")) {
    calibrar();
  } else if (!strcmp(c, "INFO?")) {
    Serial.print(F("INFO:STRIKE_KIDS:" VERSAO ":LEDS="));
    Serial.print(USAR_LEDS);
    Serial.print(F(":SERVO="));
    Serial.print(SERVO_PINO);
    Serial.print(F(":RELE="));
    Serial.print(RELE_PINO);
    Serial.print(F(":PONTE_H="));
    if (!USAR_PONTE_H) Serial.print('0');
    else Serial.print(PONTE_FIM_DE_CURSO ? F("FIM_DE_CURSO") : F("TEMPO"));
    Serial.print(F(":REPOUSO="));
    for (uint8_t i = 0; i < 5; i++) Serial.print(sensores[i].repouso == HIGH ? 'H' : 'L');
    Serial.println();
  } else {
#if USAR_LEDS
    ledComando(c);
#endif
  }
}

void lerSerial() {
  uint8_t n = 0;
  while (Serial.available() && n++ < 32) {
    char c = Serial.read();
    if (c == '\n' || c == '\r') {
      if (bufLen) {
        buf[bufLen] = '\0';
        executar(buf);
        bufLen = 0;
      }
      continue;
    }
    if (c >= 'a' && c <= 'z') c -= 32;
    if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == ':' || c == '?' || c == '_') {
      if (bufLen < sizeof(buf) - 1) buf[bufLen++] = c;
      else bufLen = 0;  // linha comprida demais: descarta
    }
  }
}

// ============================================================ setup / loop
void setup() {
  // a porta nasce fechada: rele desligado e motor parado ja antes de virar saida
  releLigado = true;
  releEscrever(false);
  pinMode(RELE_PINO, OUTPUT);
#if USAR_PONTE_H
  ponteEscrever(0);
  pinMode(PONTE_IN1, OUTPUT);
  pinMode(PONTE_IN2, OUTPUT);
  pinMode(PONTE_EN, OUTPUT);
#if PONTE_FIM_DE_CURSO
  pinMode(FC_ABERTA_PINO, INPUT_PULLUP);
  pinMode(FC_FECHADA_PINO, INPUT_PULLUP);
#endif
  ponteDesde = millis() - PONTE_PAUSA_MS;  // ja no 1o loop o motor fecha a porta
#endif
  servoIniciar();
  pinMode(LED_PLACA, OUTPUT);
  for (uint8_t i = 0; i < 5; i++) iniciarEntrada(sensores[i], SENSOR_PINOS[i], FILTRO_SENSOR_MS, REARME_SENSOR_MS, NOMES_SENSOR[i]);
  iniciarEntrada(botoes[0], CONFIG_PINO, FILTRO_BOTAO_MS, 250, "CFG");
  iniciarEntrada(botoes[1], START_PINO, FILTRO_BOTAO_MS, 250, "START");
  iniciarEntrada(botoes[2], FICHA_PINO, FILTRO_FICHA_MS, 40, "FICHA");
  carregarRepouso();
  Serial.begin(9600);
#if USAR_LEDS
  fita.begin();
  fita.setBrightness(BRILHO);
  fita.show();
#endif
  delay(50);
  // depois do pull-up assentar (e do repouso dos sensores carregado)
  for (uint8_t i = 0; i < 5; i++) assentarEntrada(sensores[i]);
  for (uint8_t i = 0; i < 3; i++) assentarEntrada(botoes[i]);
  Serial.println(F("OLA:STRIKE_KIDS:" VERSAO));
}

void loop() {
  unsigned long agora = millis();
  lerSerial();
  bool mudouSensor = false;
  bool disparou;
  for (uint8_t i = 0; i < 5; i++) {
    if (tickEntrada(sensores[i], agora, disparou)) mudouSensor = true;
    if (disparou) {
      Serial.println(sensores[i].evento);
      piscar(agora);
    }
  }
  if (mudouSensor && streamSensores) enviarSensores();
  for (uint8_t i = 0; i < 3; i++) {
    tickEntrada(botoes[i], agora, disparou);
    if (disparou) {
      Serial.println(botoes[i].evento);
      piscar(agora);
    }
  }
  tickServo(agora);
  tickRele(agora);
#if USAR_PONTE_H
  tickPonte(agora);
#endif
  if (portaAberta && agora - ultimoComando > SEM_JOGO_FECHA_MS) {
    porta(false);
    Serial.println(F("AVISO:SEM_JOGO"));
  }
  if (piscarAte && (long)(agora - piscarAte) >= 0) {
    piscarAte = 0;
    digitalWrite(LED_PLACA, LOW);
  }
#if USAR_LEDS
  tickLeds(agora);
#endif
}
