#pragma once
#include "protocol/IProtocolParser.h"
#include <QtCore/QQueue>
namespace rf {
class VofaCompatibleParser final : public IProtocolParser {
public:
    void feed(QByteArrayView bytes) override;
    bool tryPopFrame(Frame& out) override;
    QByteArray buildCommand(CommandId cmd, QByteArrayView payload) const override;
    ParserType type() const override { return ParserType::VofaCompatible; }
    void reset() override;
private:
    QByteArray buffer_;
    QQueue<Frame> queue_;
};
}
