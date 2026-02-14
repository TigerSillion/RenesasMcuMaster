#include "rforge_protocol.h"
uint16_t rforge_crc16_ccitt(const uint8_t* data, uint16_t len) {
    uint16_t crc = 0xFFFF;
    for (uint16_t i = 0; i < len; ++i) {
        crc ^= (uint16_t)data[i] << 8;
        for (uint8_t b = 0; b < 8; ++b) crc = (crc & 0x8000) ? (uint16_t)((crc << 1) ^ 0x1021) : (uint16_t)(crc << 1);
    }
    return crc;
}
int rforge_build_frame(uint8_t cmd, uint16_t seq, const uint8_t* payload, uint16_t len, uint8_t* out, uint16_t out_cap) {
    if (!out || len > RFORGE_MAX_PAYLOAD) return -1;
    const uint16_t total = (uint16_t)(8 + len + 2);
    if (out_cap < total) return -2;
    out[0]=RFORGE_SOF0; out[1]=RFORGE_SOF1; out[2]=RFORGE_VERSION; out[3]=cmd;
    out[4]=(uint8_t)(seq & 0xFF); out[5]=(uint8_t)((seq>>8)&0xFF);
    out[6]=(uint8_t)(len & 0xFF); out[7]=(uint8_t)((len>>8)&0xFF);
    for(uint16_t i=0;i<len;++i) out[8+i] = payload ? payload[i] : 0;
    uint16_t crc = rforge_crc16_ccitt(&out[2], (uint16_t)(1+1+2+2+len));
    out[8+len]=(uint8_t)(crc & 0xFF); out[9+len]=(uint8_t)((crc>>8)&0xFF);
    return total;
}
