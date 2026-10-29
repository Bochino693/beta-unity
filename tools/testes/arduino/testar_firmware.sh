#!/bin/bash
# TESTE DO FIRMWARE DO ARDUINO NO PC (sem placa): compila as duas versoes
# - arduino/strike_kids (RELE/SERVO, sem ponte H) e
# arduino/strike_kids_ponte_h (PONTE H) - com um Arduino de mentira (mock/)
# e simula a porta (ponte H pelo tempo e com fim de curso, porta presa,
# inversao, o tempo e o fim de curso escolhidos no menu, so a ponte H sem
# servo e rele; o rele e o servo sem ponte H), o moedeiro NA e NF, o START
# e os sensores. Confere tambem que as duas versoes tem o MESMO codigo (so
# o comentario do comeco e o USAR_PONTE_H mudam).
#
#     bash tools/testes/arduino/testar_firmware.sh
#
# Precisa so do g++. Cada variante troca um ajuste do .ino (sed) e compila.
set -e
AQUI="$(cd "$(dirname "$0")" && pwd)"
RELE="$AQUI/../../../arduino/strike_kids/strike_kids.ino"
PONTE="$AQUI/../../../arduino/strike_kids_ponte_h/strike_kids_ponte_h.ino"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
falhou=0
# o mesmo codigo nas duas: tira o comentario do comeco e a linha do USAR_PONTE_H
corpo() { sed -n '/^#include <EEPROM.h>/,$p' "$1" | grep -v '^#define USAR_PONTE_H '; }
if ! diff <(corpo "$RELE") <(corpo "$PONTE") > "$TMP/diferenca"; then
  echo "  FALHOU: as duas versoes do .ino tem codigo diferente:"; cat "$TMP/diferenca"; falhou=1
fi
grep -q '^#define USAR_PONTE_H 0 ' "$RELE" || { echo "  FALHOU: strike_kids sem USAR_PONTE_H 0"; falhou=1; }
grep -q '^#define USAR_PONTE_H 1 ' "$PONTE" || { echo "  FALHOU: strike_kids_ponte_h sem USAR_PONTE_H 1"; falhou=1; }
variante() {  # nome, .ino, sed
  sed "$3" "$2" > "$TMP/fw_$1.ino"
  g++ -std=gnu++17 -Wall -Wextra -Wno-unused-parameter -I"$AQUI/mock" -I"$TMP" \
    -DFIRMWARE="\"fw_$1.ino\"" "$AQUI/teste_firmware.cpp" -o "$TMP/t_$1"
}
variante padrao "$PONTE" 's/x/x/'
variante fc "$PONTE" 's/^#define PONTE_FIM_DE_CURSO 0/#define PONTE_FIM_DE_CURSO 1/'
variante fcnf "$PONTE" 's/^#define PONTE_FIM_DE_CURSO 0/#define PONTE_FIM_DE_CURSO 1/; s/^#define FC_NF 0/#define FC_NF 1/'
variante inv "$PONTE" 's/^#define PONTE_INVERTIDA 0/#define PONTE_INVERTIDA 1/'
variante soponte "$PONTE" 's/^#define USAR_SERVO 1/#define USAR_SERVO 0/; s/^#define USAR_RELE 1/#define USAR_RELE 0/'
variante rele "$RELE" 's/x/x/'
variante rele5 "$RELE" 's/^#define RELE_TEMPO_MAX_S 0 /#define RELE_TEMPO_MAX_S 5 /'
variante semleds "$RELE" 's/^#define USAR_LEDS 1/#define USAR_LEDS 0/'
grep -q '^#define RELE_TEMPO_MAX_S 5 ' "$TMP/fw_rele5.ino" || { echo "  FALHOU: variante rele5"; falhou=1; }
for t in "padrao tempo" "padrao ficha_nf" "padrao ficha_na" "fc fim_de_curso" "fc travou" \
         "fcnf fim_de_curso" "inv invertida" "padrao menu" "soponte so_ponte" \
         "rele rele" "rele5 rele" "rele sem_ponte" "semleds sem_ponte" "rele ficha_nf" "rele ficha_na"; do
  set -- $t
  "$TMP/t_$1" "$2" || falhou=1
done
[ $falhou = 0 ] && echo "FIRMWARE OK" || { echo "FIRMWARE COM ERRO"; exit 1; }
