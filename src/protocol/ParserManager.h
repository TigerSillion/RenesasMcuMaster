#pragma once
#include "common/types.h"
#include "protocol/RForgeBinaryParser.h"
#include "protocol/VofaCompatibleParser.h"
namespace rf {
class ParserManager {
public:
    void setMode(ParserType mode);
    ParserType mode() const { return mode_; }
    void feed(QByteArrayView bytes);
    bool tryPopFrame(Frame& out);
    QByteArray buildCommand(CommandId cmd, QByteArrayView payload) const;
private:
    ParserType autoDetect(QByteArrayView bytes) const;
    ParserType mode_ = ParserType::AutoDetect;
    mutable RForgeBinaryParser rforge_;
    mutable VofaCompatibleParser vofa_;
};
}
