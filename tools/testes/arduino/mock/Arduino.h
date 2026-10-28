// Arduino de mentira para compilar e simular o firmware no PC
#pragma once
#include <cstdint>
#include <cstring>
#include <cstdio>
#include <string>
#include <deque>
#define HIGH 1
#define LOW 0
#define INPUT 0
#define OUTPUT 1
#define INPUT_PULLUP 2
#define A0 14
#define A1 15
#define A2 16
#define A3 17
#define A4 18
#define A5 19
#define F_CPU 16000000UL
extern unsigned long t_ms;
extern int pinLevel[32], pinOut[32], pinModeV[32], pinPwm[32];
inline unsigned long millis() { return t_ms; }
inline void delay(unsigned long ms) { t_ms += ms; }
inline void delayMicroseconds(unsigned int) {}
inline int digitalRead(uint8_t p) { return pinLevel[p]; }
inline void digitalWrite(uint8_t p, uint8_t v) { pinOut[p] = v ? 1 : 0; pinPwm[p] = v ? 255 : 0; }
inline void pinMode(uint8_t p, uint8_t m) { pinModeV[p] = m; }
inline void analogWrite(uint8_t p, int v) { pinModeV[p] = OUTPUT; pinPwm[p] = v; pinOut[p] = v >= 128; }
#define _BV(b) (1 << (b))
#define COM1A1 7
#define WGM11 1
#define WGM13 4
#define WGM12 3
#define CS11 1
extern volatile uint8_t TCCR1A, TCCR1B;
extern volatile uint16_t ICR1, OCR1A;
#define min(a, b) ((a) < (b) ? (a) : (b))
#define max(a, b) ((a) > (b) ? (a) : (b))
#define constrain(amt, low, high) ((amt) < (low) ? (low) : ((amt) > (high) ? (high) : (amt)))
class __FlashStringHelper;
#define F(s) (reinterpret_cast<const __FlashStringHelper *>(s))
struct SerialMock {
  std::string out;
  std::deque<char> in;
  void begin(long) {}
  int available() { return (int)in.size(); }
  int read() { char c = in.front(); in.pop_front(); return c; }
  void print(const __FlashStringHelper *s) { out += reinterpret_cast<const char *>(s); }
  void print(const char *s) { out += s; }
  void print(char c) { out += c; }
  void print(int v) { out += std::to_string(v); }
  void println() { out += "\n"; }
  template <class T> void println(T v) { print(v); println(); }
};
extern SerialMock Serial;
