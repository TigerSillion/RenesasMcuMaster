#pragma once
#include <stdint.h>
#ifdef __cplusplus
extern "C" {
#endif
#define RFORGE_SOF0 0xAA
#define RFORGE_SOF1 0x55
#define RFORGE_VERSION 0x01
#define RFORGE_MAX_PAYLOAD 1024
typedef enum {
    RF_CMD_PING = 0x01, RF_CMD_ACK = 0x02, RF_CMD_STREAM_START = 0x03, RF_CMD_STREAM_STOP = 0x04,
    RF_CMD_SET_STREAM_CONFIG = 0x05, RF_CMD_GET_VAR_TABLE = 0x10, RF_CMD_READ_MEM_BATCH = 0x11,
    RF_CMD_WRITE_MEM = 0x12, RF_CMD_STREAM_DATA = 0x20
} rforge_cmd_t;
uint16_t rforge_crc16_ccitt(const uint8_t* data, uint16_t len);
int rforge_build_frame(uint8_t cmd, uint16_t seq, const uint8_t* payload, uint16_t len, uint8_t* out, uint16_t out_cap);
#ifdef __cplusplus
}
#endif
