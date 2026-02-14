#pragma once
#include "common/types.h"
#include <QtCore/QObject>
namespace rf {
class ITransport : public QObject {
    Q_OBJECT
public:
    using QObject::QObject;
    ~ITransport() override = default;
    virtual bool open(const TransportConfig& config) = 0;
    virtual void close() = 0;
    virtual bool isOpen() const = 0;
    virtual qint64 write(const QByteArray& data) = 0;
    virtual qint64 read(uint8_t* buffer, qint64 maxSize) = 0;
    virtual qint64 bytesAvailable() const = 0;
signals:
    void dataReceived();
    void errorOccurred(const QString& message);
    void stateChanged(rf::ConnectionState state);
};
}
