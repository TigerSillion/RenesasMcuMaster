#include "protocol/Crc16Ccitt.h"
namespace rf {
uint16_t Crc16Ccitt::compute(QByteArrayView bytes, uint16_t init) {
    uint16_t crc = init;
    for (char c : bytes) {
        crc ^= static_cast<uint16_t>(static_cast<uint8_t>(c)) << 8;
        for (int i = 0; i < 8; ++i) crc = (crc & 0x8000) ? static_cast<uint16_t>((crc << 1) ^ 0x1021) : static_cast<uint16_t>(crc << 1);
    }
    return crc;
}
}
