#!/bin/bash
# TESTE DO FIRMWARE DO ARDUINO NO PC (sem placa): compila o
# arduino/strike_kids/strike_kids.ino com um Arduino de mentira (mock/) e
# simula a porta (ponte H pelo tempo e com fim de curso, porta presa,
# inversao), o moedeiro NA e NF, o START e os sensores.
#
#     bash tools/testes/arduino/testar_firmware.sh
#
# Precisa so do g++. Cada variante troca um ajuste do .ino (sed) e compila.
set -e
AQUI="$(cd "$(dirname "$0")" && pwd)"
INO="$AQUI/../../../arduino/strike_kids/strike_kids.ino"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
variante() {  # nome, sed
  sed "$2" "$INO" > "$TMP/fw_$1.ino"
  g++ -std=gnu++17 -Wall -Wextra -Wno-unused-parameter -I"$AQUI/mock" -I"$TMP" \
    -DFIRMWARE="\"fw_$1.ino\"" "$AQUI/teste_firmware.cpp" -o "$TMP/t_$1"
}
variante padrao 's/x/x/'
variante fc 's/^#define PONTE_FIM_DE_CURSO 0/#define PONTE_FIM_DE_CURSO 1/'
variante fcnf 's/^#define PONTE_FIM_DE_CURSO 0/#define PONTE_FIM_DE_CURSO 1/; s/^#define FC_NF 0/#define FC_NF 1/'
variante inv 's/^#define PONTE_INVERTIDA 0/#define PONTE_INVERTIDA 1/'
variante semponte 's/^#define USAR_PONTE_H 1/#define USAR_PONTE_H 0/; s/^#define USAR_LEDS 1/#define USAR_LEDS 0/'
falhou=0
for t in "padrao tempo" "padrao ficha_nf" "padrao ficha_na" "fc fim_de_curso" "fc travou" \
         "fcnf fim_de_curso" "inv invertida" "semponte sem_ponte"; do
  set -- $t
  "$TMP/t_$1" "$2" || falhou=1
done
[ $falhou = 0 ] && echo "FIRMWARE OK" || { echo "FIRMWARE COM ERRO"; exit 1; }
