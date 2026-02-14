#include "core/ConnectionManager.h"
namespace rf {
ConnectionManager::ConnectionManager(QObject* parent) : QObject(parent) {}
void ConnectionManager::setTransport(ITransport* transport) {
    if (transport_ == transport) return;
    if (transport_) disconnect(transport_, nullptr, this, nullptr);
    transport_ = transport;
    if (transport_) {
        connect(transport_, &ITransport::dataReceived, this, &ConnectionManager::onTransportData);
        connect(transport_, &ITransport::errorOccurred, this, &ConnectionManager::errorOccurred);
        connect(transport_, &ITransport::stateChanged, this, &ConnectionManager::stateChanged);
    }
}
bool ConnectionManager::open(const TransportConfig& cfg) { if(!transport_){ emit errorOccurred("Transport is not set"); return false;} return transport_->open(cfg); }
void ConnectionManager::close() { if(transport_) transport_->close(); }
void ConnectionManager::setParserMode(ParserType type) { parser_.setMode(type); }
ParserType ConnectionManager::parserMode() const { return parser_.mode(); }
bool ConnectionManager::sendCommand(CommandId cmd, QByteArrayView payload) {
    if(!transport_ || !transport_->isOpen()) return false;
    QByteArray p = parser_.buildCommand(cmd, payload);
    return transport_->write(p) == p.size();
}
void ConnectionManager::onTransportData() {
    if(!transport_) return;
    QByteArray bytes; bytes.resize(static_cast<int>(transport_->bytesAvailable())); if(bytes.isEmpty()) return;
    qint64 n = transport_->read(reinterpret_cast<uint8_t*>(bytes.data()), bytes.size()); if(n<=0) return; bytes.resize(static_cast<int>(n));
    parser_.feed(bytes);
    Frame f; while(parser_.tryPopFrame(f)) emit frameReceived(f);
}
}
