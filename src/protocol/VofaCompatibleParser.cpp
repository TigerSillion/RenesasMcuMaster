#include "protocol/VofaCompatibleParser.h"
namespace rf {
void VofaCompatibleParser::feed(QByteArrayView bytes) {
    buffer_.append(bytes.data(), static_cast<int>(bytes.size()));
    while (true) {
        const int end = buffer_.indexOf('\n');
        if (end < 0) return;
        QByteArray line = buffer_.left(end).trimmed();
        buffer_.remove(0, end + 1);
        if (line.isEmpty()) continue;
        queue_.enqueue(Frame{CommandId::StreamData, 0, line});
    }
}
bool VofaCompatibleParser::tryPopFrame(Frame& out) { if (queue_.isEmpty()) return false; out = queue_.dequeue(); return true; }
QByteArray VofaCompatibleParser::buildCommand(CommandId, QByteArrayView payload) const { QByteArray out(payload.data(), static_cast<int>(payload.size())); out.append('\n'); return out; }
void VofaCompatibleParser::reset() { buffer_.clear(); queue_.clear(); }
}
