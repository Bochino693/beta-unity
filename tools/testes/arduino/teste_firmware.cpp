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

int main(int argc, char **argv) {
  std::string c = argc > 1 ? argv[1] : "";
  iniciar();
  if (c == "tempo") {
    setup(); saida += Serial.out; Serial.out.clear();
    CONFERE(saida.find("OLA:STRIKE_KIDS:2") != std::string::npos, "OLA ao ligar");
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
  } else if (c == "sem_ponte") {
    setup(); pega();
    manda("PORTA:ABRE");
    rodar(2);
    CONFERE(conta("PORTA:ABERTA") == 1, "sem ponte H: confirma na hora");
    manda("INFO?");
    rodar(2);
    printf("  %s", pega().c_str());
  } else {
    printf("cenario?\n");
    return 2;
  }
  printf("%s %s\n", c.c_str(), falhas ? "FALHOU" : "OK");
  return falhas ? 1 : 0;
}
