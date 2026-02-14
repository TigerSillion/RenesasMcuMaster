#include "viewmodel/ConnectionVM.h"
namespace rf {
ConnectionVM::ConnectionVM(ConnectionManager* mgr, QObject* parent) : QObject(parent), mgr_(mgr) {
    QObject::connect(mgr_, &ConnectionManager::stateChanged, this, [this](ConnectionState st) {
        switch(st){
        case ConnectionState::Disconnected: status_="Disconnected"; break;
        case ConnectionState::Connecting: status_="Connecting"; break;
        case ConnectionState::Connected: status_="Connected"; break;
        case ConnectionState::Error: status_="Error"; break;
        }
        emit statusChanged();
    });
}
void ConnectionVM::connectPort(const QString& portName, int baudRate) {
    if(!mgr_) return;
    TransportConfig cfg; cfg.portName = portName; cfg.baudRate = baudRate; mgr_->open(cfg);
}
void ConnectionVM::disconnectPort() { if(mgr_) mgr_->close(); }
}
