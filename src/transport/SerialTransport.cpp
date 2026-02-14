#include "transport/SerialTransport.h"
namespace rf {
SerialTransport::SerialTransport(QObject* parent) : ITransport(parent) {
    QObject::connect(&serial_, &QSerialPort::readyRead, this, &SerialTransport::dataReceived);
    QObject::connect(&serial_, &QSerialPort::errorOccurred, this, [this](QSerialPort::SerialPortError err) {
        if (err == QSerialPort::NoError) return;
        emit errorOccurred(serial_.errorString());
        emit stateChanged(ConnectionState::Error);
    });
}
bool SerialTransport::open(const TransportConfig& config) {
    if (serial_.isOpen()) serial_.close();
    serial_.setPortName(config.portName);
    serial_.setBaudRate(config.baudRate);
    serial_.setDataBits(static_cast<QSerialPort::DataBits>(config.dataBits));
    serial_.setStopBits(config.stopBits == 2 ? QSerialPort::TwoStop : QSerialPort::OneStop);
    serial_.setParity(static_cast<QSerialPort::Parity>(config.parity));
    emit stateChanged(ConnectionState::Connecting);
    if (!serial_.open(QIODevice::ReadWrite)) {
        emit errorOccurred(serial_.errorString());
        emit stateChanged(ConnectionState::Error);
        return false;
    }
    emit stateChanged(ConnectionState::Connected);
    return true;
}
void SerialTransport::close() { if (serial_.isOpen()) serial_.close(); emit stateChanged(ConnectionState::Disconnected); }
bool SerialTransport::isOpen() const { return serial_.isOpen(); }
qint64 SerialTransport::write(const QByteArray& data) { return serial_.write(data); }
qint64 SerialTransport::read(uint8_t* buffer, qint64 maxSize) { return serial_.read(reinterpret_cast<char*>(buffer), maxSize); }
qint64 SerialTransport::bytesAvailable() const { return serial_.bytesAvailable(); }
}
