#include "protocol/RForgeBinaryParser.h"
#include "protocol/Crc16Ccitt.h"
namespace {
inline uint16_t readU16Le(const QByteArray& b, int i) {
    return static_cast<uint16_t>(static_cast<uint8_t>(b[i])) | static_cast<uint16_t>(static_cast<uint8_t>(b[i + 1]) << 8);
}
inline void appendU16Le(QByteArray& out, uint16_t v) {
    out.append(static_cast<char>(v & 0xFF));
    out.append(static_cast<char>((v >> 8) & 0xFF));
}
}
namespace rf {
void RForgeBinaryParser::feed(QByteArrayView bytes) {
    buffer_.append(bytes.data(), static_cast<int>(bytes.size()));
    while (buffer_.size() >= kHeaderSize + kTailSize) {
        int sof = -1;
        for (int i = 0; i + 1 < buffer_.size(); ++i) {
            if (static_cast<uint8_t>(buffer_[i]) == kSof0 && static_cast<uint8_t>(buffer_[i + 1]) == kSof1) { sof = i; break; }
        }
        if (sof < 0) { buffer_.clear(); return; }
        if (sof > 0) buffer_.remove(0, sof);
        if (buffer_.size() < kHeaderSize + kTailSize) return;

        const uint16_t payload_len = readU16Le(buffer_, 6);
        if (payload_len > kMaxPayload) { buffer_.remove(0, 1); ++crc_error_count_; continue; }

        const int total = kHeaderSize + payload_len + kTailSize;
        if (buffer_.size() < total) return;

        const QByteArray crc_input = buffer_.mid(2, 1 + 1 + 2 + 2 + payload_len);
        const uint16_t expect = readU16Le(buffer_, kHeaderSize + payload_len);
        const uint16_t actual = Crc16Ccitt::compute(QByteArrayView(crc_input));
        if (expect != actual) { buffer_.remove(0, 1); ++crc_error_count_; continue; }

        Frame frame;
        frame.cmd = static_cast<CommandId>(static_cast<uint8_t>(buffer_[3]));
        frame.seq = readU16Le(buffer_, 4);
        frame.payload = buffer_.mid(kHeaderSize, payload_len);
        queue_.enqueue(frame);
        buffer_.remove(0, total);
    }
}
bool RForgeBinaryParser::tryPopFrame(Frame& out) {
    if (queue_.isEmpty()) return false;
    out = queue_.dequeue();
    return true;
}
QByteArray RForgeBinaryParser::buildCommand(CommandId cmd, QByteArrayView payload) const {
    QByteArray out;
    out.reserve(kHeaderSize + static_cast<int>(payload.size()) + kTailSize);
    out.append(static_cast<char>(kSof0));
    out.append(static_cast<char>(kSof1));
    out.append(static_cast<char>(1));
    out.append(static_cast<char>(cmd));
    appendU16Le(out, 0);
    appendU16Le(out, static_cast<uint16_t>(payload.size()));
    out.append(payload.data(), static_cast<int>(payload.size()));
    const uint16_t crc = Crc16Ccitt::compute(QByteArrayView(out.mid(2)));
    appendU16Le(out, crc);
    return out;
}
void RForgeBinaryParser::reset() { buffer_.clear(); queue_.clear(); crc_error_count_ = 0; }
}
