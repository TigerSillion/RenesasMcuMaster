#pragma once
#include "core/ConnectionManager.h"
#include <QtCore/QObject>
namespace rf {
class ConnectionVM : public QObject {
    Q_OBJECT
    Q_PROPERTY(QString status READ status NOTIFY statusChanged)
public:
    explicit ConnectionVM(ConnectionManager* mgr, QObject* parent = nullptr);
    QString status() const { return status_; }
public slots:
    void connectPort(const QString& portName, int baudRate);
    void disconnectPort();
signals:
    void statusChanged();
private:
    ConnectionManager* mgr_ = nullptr;
    QString status_ = "Disconnected";
};
}
