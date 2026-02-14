#pragma once
#include <QtCore/QByteArrayView>
#include <cstdint>
namespace rf { class Crc16Ccitt { public: static uint16_t compute(QByteArrayView bytes, uint16_t init = 0xFFFF); }; }
