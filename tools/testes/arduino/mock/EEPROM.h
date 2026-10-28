#pragma once
struct EEPROMMock {
  uint8_t m[1024];
  EEPROMMock() { memset(m, 0xFF, sizeof m); }
  uint8_t read(int a) { return m[a]; }
  void update(int a, uint8_t v) { m[a] = v; }
};
extern EEPROMMock EEPROM;
