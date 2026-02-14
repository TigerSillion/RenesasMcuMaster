#pragma once
#include "transport/ITransport.h"
#include <QtSerialPort/QSerialPort>
namespace rf {
class SerialTransport final : public ITransport {
    Q_OBJECT
public:
    explicit SerialTransport(QObject* parent = nullptr);
    bool open(const TransportConfig& config) override;
    void close() override;
    bool isOpen() const override;
    qint64 write(const QByteArray& data) override;
    qint64 read(uint8_t* buffer, qint64 maxSize) override;
    qint64 bytesAvailable() const override;
private:
    QSerialPort serial_;
};
}
