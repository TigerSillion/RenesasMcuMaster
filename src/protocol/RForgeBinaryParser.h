#pragma once
#include "protocol/IProtocolParser.h"
#include <QtCore/QQueue>
namespace rf {
class RForgeBinaryParser final : public IProtocolParser {
public:
    void feed(QByteArrayView bytes) override;
    bool tryPopFrame(Frame& out) override;
    QByteArray buildCommand(CommandId cmd, QByteArrayView payload) const override;
    ParserType type() const override { return ParserType::RForgeBinary; }
    void reset() override;
    int crcErrorCount() const { return crc_error_count_; }
private:
    static constexpr uint8_t kSof0 = 0xAA;
    static constexpr uint8_t kSof1 = 0x55;
    static constexpr int kHeaderSize = 8;
    static constexpr int kTailSize = 2;
    static constexpr int kMaxPayload = 1024;
    QByteArray buffer_;
    QQueue<Frame> queue_;
    int crc_error_count_ = 0;
};
}
