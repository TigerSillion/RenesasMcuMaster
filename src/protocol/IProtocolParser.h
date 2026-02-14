#pragma once
#include "common/types.h"
#include <QtCore/QByteArrayView>
namespace rf {
class IProtocolParser {
public:
    virtual ~IProtocolParser() = default;
    virtual void feed(QByteArrayView bytes) = 0;
    virtual bool tryPopFrame(Frame& out) = 0;
    virtual QByteArray buildCommand(CommandId cmd, QByteArrayView payload) const = 0;
    virtual ParserType type() const = 0;
    virtual void reset() = 0;
};
}
