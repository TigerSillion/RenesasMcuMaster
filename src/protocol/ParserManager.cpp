#include "protocol/ParserManager.h"
namespace rf {
void ParserManager::setMode(ParserType mode) { mode_ = mode; rforge_.reset(); vofa_.reset(); }
ParserType ParserManager::autoDetect(QByteArrayView bytes) const {
    if (bytes.size() >= 2 && static_cast<uint8_t>(bytes[0]) == 0xAA && static_cast<uint8_t>(bytes[1]) == 0x55) return ParserType::RForgeBinary;
    return ParserType::VofaCompatible;
}
void ParserManager::feed(QByteArrayView bytes) {
    ParserType active = (mode_ == ParserType::AutoDetect) ? autoDetect(bytes) : mode_;
    if (active == ParserType::RForgeBinary) rforge_.feed(bytes); else vofa_.feed(bytes);
}
bool ParserManager::tryPopFrame(Frame& out) {
    if (mode_ == ParserType::RForgeBinary) return rforge_.tryPopFrame(out);
    if (mode_ == ParserType::VofaCompatible) return vofa_.tryPopFrame(out);
    if (rforge_.tryPopFrame(out)) return true;
    return vofa_.tryPopFrame(out);
}
QByteArray ParserManager::buildCommand(CommandId cmd, QByteArrayView payload) const {
    if (mode_ == ParserType::VofaCompatible) return vofa_.buildCommand(cmd, payload);
    return rforge_.buildCommand(cmd, payload);
}
}
