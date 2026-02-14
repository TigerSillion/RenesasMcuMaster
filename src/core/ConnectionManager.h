#pragma once
#include "protocol/ParserManager.h"
#include "transport/ITransport.h"
#include <QtCore/QByteArrayView>
#include <QtCore/QObject>
namespace rf {
class ConnectionManager : public QObject {
    Q_OBJECT
public:
    explicit ConnectionManager(QObject* parent = nullptr);
    void setTransport(ITransport* transport);
    bool open(const TransportConfig& cfg);
    void close();
    void setParserMode(ParserType type);
    ParserType parserMode() const;
    bool sendCommand(CommandId cmd, QByteArrayView payload = {});
signals:
    void frameReceived(const rf::Frame& frame);
    void errorOccurred(const QString& error);
    void stateChanged(rf::ConnectionState state);
private slots:
    void onTransportData();
private:
    ITransport* transport_ = nullptr;
    ParserManager parser_;
};
}
