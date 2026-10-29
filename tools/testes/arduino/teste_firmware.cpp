// Simula o firmware do Arduino no PC (tools/testes/arduino/testar_firmware.sh):
// tempo, pinos, serial e uma porta de verdade com os fins de curso.
#include "Arduino.h"
#include <cstdlib>
unsigned long t_ms = 0;
int pinLevel[32], pinOut[32], pinModeV[32], pinPwm[32];
volatile uint8_t TCCR1A, TCCR1B;
volatile uint16_t ICR1, OCR1A;
SerialMock Serial;
#include "EEPROM.h"
EEPROMMock EEPROM;
#include FIRMWARE

static int falhas = 0;
#define CONFERE(c, msg) do { if (!(c)) { printf("  FALHOU (t=%lu): %s\n", t_ms, msg); falhas++; } } while (0)

// porta de verdade: 0 = fechada ... 1000 = aberta, anda 1 por ms com o motor
int portaFisica = 500;
bool presa = false;
int motor() {  // 1 abrindo, -1 fechando, 0 parado/freado
  if (pinPwm[PONTE_EN] == 0 || pinOut[PONTE_IN1] == pinOut[PONTE_IN2]) return 0;
  int s = pinOut[PONTE_IN1] ? 1 : -1;
  return PONTE_INVERTIDA ? -s : s;
}
int motorBruto() { return pinOut[PONTE_IN1] * 10 + pinOut[PONTE_IN2]; }
void fisica() {
  if (!presa) portaFisica = constrain(portaFisica + motor(), 0, 1000);
  bool na_aberta = portaFisica >= 1000, na_fechada = portaFisica <= 0;
  pinLevel[FC_ABERTA_PINO] = (na_aberta != (FC_NF != 0)) ? LOW : HIGH;
  pinLevel[FC_FECHADA_PINO] = (na_fechada != (FC_NF != 0)) ? LOW : HIGH;
}
std::string saida;
void rodar(unsigned long ms) {
  for (unsigned long i = 0; i < ms; i++) { fisica(); loop(); t_ms++; }
  saida += Serial.out; Serial.out.clear();
}
void manda(const char *c) { for (const char *p = c; *p; p++) Serial.in.push_back(*p); Serial.in.push_back('\n'); }
int conta(const std::string &s) { int n = 0; size_t p = 0; while ((p = saida.find(s, p)) != std::string::npos) { n++; p += s.size(); } return n; }
std::string pega() { std::string s = saida; saida.clear(); return s; }
void iniciar() {
  for (int i = 0; i < 32; i++) pinLevel[i] = HIGH;
  fisica();
}
// roda ate aparecer a linha (ou estourar o tempo); devolve o tempo que levou
long ate(const char *linha, unsigned long max_ms) {
  unsigned long t0 = t_ms;
  while (t_ms - t0 < max_ms) { rodar(1); if (saida.find(linha) != std::string::npos) return (long)(t_ms - t0); }
  return -1;
}

// desliga e liga o Arduino (o Uno/Nano reinicia quando o jogo abre o USB):
// a memoria volta ao valor de fabrica, so a EEPROM fica
void religar() {
#if USAR_PONTE_H
  ponteAlvoAberta = false; ponteMov = 0; ponteNoAlvo = false; ponteFalhas = 0;
  ponteDesde = 0; ponteFreioAte = 0; pontePos = 1000; ponteInicioPos = 1000;
  ponteTempoAbre = PONTE_TEMPO_ABRE_MS; ponteTempoFecha = PONTE_TEMPO_FECHA_MS;
  ponteFC = PONTE_FIM_DE_CURSO;
#endif
  portaAberta = false; ultimoComando = 0; bufLen = 0;
  setup();
}

int main(int argc, char **argv) {
  std::string c = argc > 1 ? argv[1] : "";
  iniciar();
  if (c == "tempo") {
    setup(); saida += Serial.out; Serial.out.clear();
    CONFERE(saida.find("OLA:STRIKE_KIDS:3") != std::string::npos, "OLA ao ligar");
    rodar(2);
    CONFERE(motor() == -1, "ao ligar o motor fecha a porta");
    long t = ate("PORTA:FECHADA", 3000);
    printf("  ligou: fechou em %ld ms\n", t);
    CONFERE(t >= 1400 && t <= 1600, "fecha pelo tempo (1500 ms)");
    CONFERE(motorBruto() == 11, "freia ao parar");
    rodar(200);
    CONFERE(motorBruto() == 0 && motor() == 0, "solta o motor depois do freio");
    rodar(200);   // pausa de 300 ms depois de parar
    pega();
    manda("PORTA:ABRE");
    rodar(5);
    CONFERE(motor() == 1, "ABRE: motor abre");
    CONFERE(conta("PORTA:ABERTA") == 0, "so confirma quando chegar");
    t = ate("PORTA:ABERTA", 3000);
    printf("  abriu em %ld ms\n", t);
    CONFERE(t >= 1400 && t <= 1600, "abre pelo tempo");
    rodar(400); pega();
    manda("PORTA:ABRE");   // de novo: so confirma, nao mexe
    rodar(5);
    CONFERE(conta("PORTA:ABERTA") == 1 && motor() == 0, "ABRE repetido so confirma");
    // muda de ideia no meio
    pega();
    manda("PORTA:FECHA");
    rodar(700);
    CONFERE(motor() == -1, "fechando");
    manda("PORTA:ABRE");
    rodar(3);
    CONFERE(motor() == 0, "inverteu: para antes");
    rodar(250);
    CONFERE(motor() == 0, "pausa antes de inverter");
    t = ate("PORTA:ABERTA", 3000);
    printf("  voltou a abrir em %ld ms (andou ~700 fechando)\n", t);
    CONFERE(t >= 650 && t <= 850, "volta so o que andou (~700 ms)");
    CONFERE(conta("PORTA:FECHADA") == 0, "nao confirmou a fechada");
    // jogo sumiu com a porta aberta: fecha sozinha
    pega();
    t = ate("AVISO:SEM_JOGO", 20000);
    CONFERE(t > 0, "SEM_JOGO fecha a porta");
    t = ate("PORTA:FECHADA", 3000);
    CONFERE(t > 1300, "... e o motor fecha");
    manda("INFO?");
    rodar(5);
    printf("  %s", pega().c_str());
  } else if (c == "fim_de_curso") {
    portaFisica = 400; fisica();
    setup(); pega(); Serial.out.clear();
    long t = ate("PORTA:FECHADA", 8000);
    printf("  ligou no meio: fechou em %ld ms (porta=%d)\n", t, portaFisica);
    CONFERE(t > 0 && portaFisica == 0, "fecha ate o fim de curso");
    rodar(200);
    CONFERE(motor() == 0, "parado");
    manda("PORTA:ABRE");
    t = ate("PORTA:ABERTA", 8000);
    printf("  abriu em %ld ms (porta=%d)\n", t, portaFisica);
    CONFERE(t > 900 && portaFisica == 1000, "abre ate o fim de curso");
    rodar(200);
    CONFERE(motor() == 0, "parado aberta");
    pega();
    manda("PORTA:FECHA");
    rodar(500);
    manda("PORTA:ABRE");   // mudou de ideia
    t = ate("PORTA:ABERTA", 8000);
    CONFERE(t > 0 && portaFisica == 1000, "volta a abrir");
    CONFERE(conta("AVISO") == 0, "sem aviso");
  } else if (c == "travou") {
    portaFisica = 400; presa = true; fisica();
    setup(); pega(); Serial.out.clear();
    long t = ate("AVISO:PORTA_TRAVOU", 8000);
    printf("  presa: aviso em %ld ms\n", t);
    CONFERE(t >= 5900 && t <= 6100, "para em 6 s");
    rodar(200);
    CONFERE(motor() == 0, "motor parado depois de travar");
    rodar(30000);
    printf("  avisos em 30 s: %d\n", conta("AVISO:PORTA_TRAVOU"));
    CONFERE(conta("AVISO:PORTA_TRAVOU") == 3, "tenta 3 vezes e desiste");
    CONFERE(motor() == 0, "desistiu: motor parado");
    presa = false; pega();
    manda("PORTA:FECHA");   // pedido de novo: tenta outra vez
    t = ate("PORTA:FECHADA", 8000);
    CONFERE(t > 0 && portaFisica == 0, "pedido novo tenta de novo e fecha");
  } else if (c == "invertida") {
    setup(); pega();
    rodar(2);
    CONFERE(motorBruto() == 10, "invertida: fechar = IN1 ligado");
  } else if (c == "ficha_nf") {
    pinLevel[FICHA_PINO] = LOW;      // moedeiro NF: fechado em repouso
    pinLevel[START_PINO] = LOW;      // START preso ao ligar
    setup(); pega();
    rodar(1000);
    CONFERE(conta("FICHA") == 0, "moedeiro NF nao da ficha ao ligar");
    CONFERE(conta("START") == 0, "START preso ao ligar nao conta");
    for (int i = 0; i < 3; i++) { pinLevel[FICHA_PINO] = HIGH; rodar(50); pinLevel[FICHA_PINO] = LOW; rodar(100); }
    printf("  3 pulsos NF -> %d FICHA\n", conta("FICHA"));
    CONFERE(conta("FICHA") == 3, "1 ficha por pulso (NF)");
  } else if (c == "ficha_na") {
    setup(); pega();
    rodar(100);
    for (int i = 0; i < 4; i++) { pinLevel[FICHA_PINO] = LOW; rodar(30); pinLevel[FICHA_PINO] = HIGH; rodar(70); }
    printf("  4 pulsos NA -> %d FICHA\n", conta("FICHA"));
    CONFERE(conta("FICHA") == 4, "1 ficha por pulso (NA)");
    pinLevel[START_PINO] = LOW; rodar(60); pinLevel[START_PINO] = HIGH; rodar(300);
    CONFERE(conta("START") == 1, "START");
    // bola no sensor do centro, balancando
    pinLevel[4] = LOW; rodar(10); pinLevel[4] = HIGH; rodar(3); pinLevel[4] = LOW; rodar(20); pinLevel[4] = HIGH; rodar(200);
    CONFERE(conta("\nS:3\n") == 1, "uma bola = um S:3");
    pinLevel[3] = LOW; rodar(10); pinLevel[3] = HIGH; rodar(10);
    CONFERE(conta("\nS:2\n") == 1, "S:2");
  } else if (c == "menu") {
    // o MENU DO OPERADOR troca o tempo do motor e o fim de curso
    setup(); pega();
    ate("PORTA:FECHADA", 3000);
    rodar(400); pega();
    manda("PONTE?");
    rodar(2);
    CONFERE(conta("PONTE:TEMPO:1500:FC:0") == 1, "PONTE? de fabrica");
    manda("PONTE:TEMPO:800");
    rodar(2);
    CONFERE(conta("PONTE:TEMPO:800:FC:0") == 1, "PONTE:TEMPO responde");
    manda("PORTA:ABRE");
    long t = ate("PORTA:ABERTA", 3000);
    printf("  tempo 800: abriu em %ld ms\n", t);
    CONFERE(t >= 700 && t <= 900, "abre no tempo do menu");
    rodar(400); pega();
    manda("PONTE:TEMPO:50");       // fora do limite: ignora
    manda("PONTE:TEMPO:99999");
    manda("PONTE:FC:7");
    rodar(3);
    CONFERE(conta("PONTE:TEMPO:800:FC:0") == 3, "valor invalido nao muda");
    // muda o tempo com o motor andando: para e termina no tempo novo
    pega();
    manda("PORTA:FECHA");
    rodar(400);
    manda("PONTE:TEMPO:2000");
    t = ate("PORTA:FECHADA", 5000);
    printf("  trocou o tempo no meio: fechou %ld ms depois\n", t);
    // (andou 400 de 800 ms = metade; a outra metade em 2000 ms = 1000, + a pausa)
    CONFERE(t >= 1200 && t <= 1450, "termina no tempo novo (o que faltava)");
    // reinicia (o jogo abre o USB): o Arduino lembra o que o menu escolheu
    rodar(400); pega();
    portaFisica = 500; fisica();
    religar(); pega();
    manda("INFO?");
    rodar(2);
    std::string info = pega();
    printf("  %s", info.c_str());
    CONFERE(info.find("PONTE_H=TEMPO:TEMPO=2000") != std::string::npos, "guardou na EEPROM");
    t = ate("PORTA:FECHADA", 5000);
    CONFERE(t >= 1900 && t <= 2100, "ao ligar fecha no tempo guardado");
    // liga o fim de curso pelo menu
    rodar(400); pega();
    portaFisica = 300; fisica();   // (o tempo contou fechada; a porta de verdade nao)
    manda("PONTE:FC:1");
    rodar(2);
    CONFERE(conta("PONTE:TEMPO:2000:FC:1") == 1, "PONTE:FC:1 responde");
    manda("PORTA:FECHA");
    t = ate("PORTA:FECHADA", 8000);
    CONFERE(t > 0 && portaFisica == 0, "com o fim de curso do menu: fecha ate o micro-switch");
    rodar(400); pega();
    manda("PORTA:ABRE");
    t = ate("PORTA:ABERTA", 8000);
    CONFERE(t > 900 && portaFisica == 1000, "abre ate o micro-switch");
    // desliga de novo, com a porta parada aberta
    rodar(400); pega();
    manda("PONTE:FC:0");
    rodar(2);
    manda("PORTA:FECHA");
    t = ate("PORTA:FECHADA", 5000);
    CONFERE(t >= 1900 && t <= 2100 && portaFisica == 0, "sem fim de curso de novo: fecha pelo tempo");
    religar(); pega();
    manda("PONTE?");
    rodar(2);
    CONFERE(conta("PONTE:TEMPO:2000:FC:0") == 1, "guardou o FC:0");
  } else if (c == "so_ponte") {
    // sem servo e sem rele: so a ponte H
    setup(); pega();
    long t = ate("PORTA:FECHADA", 3000);
    CONFERE(t >= 1400 && t <= 1600, "fecha pela ponte H");
    CONFERE(TCCR1A == 0 && TCCR1B == 0, "sem servo: o Timer1 fica livre");
    CONFERE(pinModeV[RELE_PINO] != OUTPUT, "sem rele: o D8 fica livre");
    manda("INFO?");
    rodar(2);
    std::string info = pega();
    printf("  %s", info.c_str());
    CONFERE(info.find(":SERVO=0:RELE=0:PONTE_H=TEMPO") != std::string::npos, "INFO sem servo e sem rele");
  } else if (c == "sem_ponte") {
    setup(); pega();
    manda("PORTA:ABRE");
    rodar(2);
    CONFERE(conta("PORTA:ABERTA") == 1, "sem ponte H: confirma na hora");
    manda("PONTE:TEMPO:800");   // jogo novo, Arduino sem ponte: ignora calado
    rodar(2);
    CONFERE(conta("PONTE") == 0, "sem ponte H: PONTE: ignorado");
    manda("INFO?");
    rodar(2);
    printf("  %s", pega().c_str());
  } else if (c == "rele") {
    // a versao do RELE (arduino/1_RELE_principal) como ela e gravada: rele no
    // D8 e servo no D9, sem ponte H (D10-D12, A3 e A4 livres)
    setup(); pega();
    CONFERE(USAR_PONTE_H == 0 && USAR_RELE && USAR_SERVO, "versao do rele: rele e servo, sem ponte H");
    const int livres[5] = {PONTE_IN1, PONTE_IN2, PONTE_EN, FC_ABERTA_PINO, FC_FECHADA_PINO};
    for (int p : livres) CONFERE(pinModeV[p] == INPUT, "pinos da ponte H livres");
    bool desligado = RELE_LIGA_EM_LOW ? HIGH : LOW;
    CONFERE(pinModeV[RELE_PINO] == OUTPUT && pinOut[RELE_PINO] == desligado, "ao ligar: rele desligado (porta fechada)");
    manda("PORTA:ABRE");
    rodar(2);
    CONFERE(conta("PORTA:ABERTA") == 1, "abre: confirma na hora");
    CONFERE(pinOut[RELE_PINO] != desligado, "abre: rele liga");
    rodar(1000);
    CONFERE(servoAtual10 == SERVO_ABERTA * 10, "abre: servo no angulo da porta aberta");
    manda("PORTA:FECHA");
    rodar(2);
    CONFERE(conta("PORTA:FECHADA") == 1, "fecha: confirma na hora");
    CONFERE(pinOut[RELE_PINO] == desligado, "fecha: rele desliga");
    rodar(1000);
    CONFERE(servoAtual10 == SERVO_FECHADA * 10, "fecha: servo no angulo da porta fechada");
    manda("PONTE:TEMPO:800");
    manda("PONTE:FC:1");
    rodar(2);
    CONFERE(conta("PONTE") == 0, "PONTE: do menu ignorado calado");
    for (int p : livres) CONFERE(pinModeV[p] == INPUT && pinOut[p] == 0, "pinos da ponte H continuam livres");
    manda("INFO?");
    rodar(2);
    CONFERE(conta(":RELE=8") == 1 && conta(":PONTE_H=0:") == 1, "INFO: rele no D8 e sem ponte H");
    pega();
    manda("PORTA:ABRE");
    rodar(2);
    if (RELE_TEMPO_MAX_S > 0) {
      rodar(RELE_TEMPO_MAX_S * 1000UL + 100);
      CONFERE(pinOut[RELE_PINO] == desligado, "trava solenoide: rele solta depois do RELE_TEMPO_MAX_S");
      CONFERE(conta("PORTA:FECHADA") == 0, "trava solenoide: para o jogo a porta continua aberta");
    }
    rodar(SEM_JOGO_FECHA_MS + 100);
    CONFERE(conta("AVISO:SEM_JOGO") == 1 && conta("PORTA:FECHADA") == 1, "jogo sumiu: fecha sozinha");
    CONFERE(pinOut[RELE_PINO] == desligado, "jogo sumiu: rele desligado");
  } else {
    printf("cenario?\n");
    return 2;
  }
  printf("%s %s\n", c.c_str(), falhas ? "FALHOU" : "OK");
  return falhas ? 1 : 0;
}
