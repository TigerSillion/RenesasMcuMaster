#include "core/ConnectionManager.h"
#include "viewmodel/ConnectionVM.h"
#include <QtTest/QtTest>
class ConnectionVMTest : public QObject {
    Q_OBJECT
private slots:
    void initialStatusIsDisconnected() {
        rf::ConnectionManager mgr;
        rf::ConnectionVM vm(&mgr);
        QCOMPARE(vm.status(), QString("Disconnected"));
    }
};
QTEST_MAIN(ConnectionVMTest)
#include "test_connection_vm.moc"
