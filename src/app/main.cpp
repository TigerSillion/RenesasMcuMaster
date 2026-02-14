#include "core/ConnectionManager.h"
#include "core/DataEngine.h"
#include "core/RecordEngine.h"
#include "core/VarEngine.h"
#include "transport/SerialTransport.h"
#include "viewmodel/ConnectionVM.h"
#include "viewmodel/RecorderVM.h"
#include "viewmodel/VariableVM.h"
#include "viewmodel/WaveformVM.h"
#include <QtGui/QGuiApplication>
#include <QtQml/QQmlApplicationEngine>
#include <QtQml/QQmlContext>
int main(int argc, char* argv[]) {
    QGuiApplication app(argc, argv);
    rf::SerialTransport transport;
    rf::ConnectionManager conn; conn.setTransport(&transport);
    rf::DataEngine dataEngine;
    rf::VarEngine varEngine;
    rf::RecordEngine recordEngine;
    QObject::connect(&conn, &rf::ConnectionManager::frameReceived, &dataEngine, &rf::DataEngine::appendFrame);
    rf::ConnectionVM connectionVM(&conn);
    rf::WaveformVM waveformVM(&dataEngine);
    rf::VariableVM variableVM(&varEngine);
    rf::RecorderVM recorderVM(&recordEngine);
    QQmlApplicationEngine engine;
    engine.rootContext()->setContextProperty("connectionVM", &connectionVM);
    engine.rootContext()->setContextProperty("waveformVM", &waveformVM);
    engine.rootContext()->setContextProperty("variableVM", &variableVM);
    engine.rootContext()->setContextProperty("recorderVM", &recorderVM);
    QObject::connect(&engine, &QQmlApplicationEngine::objectCreationFailed, &app, [] { QCoreApplication::exit(-1); }, Qt::QueuedConnection);
    engine.loadFromModule("RenesasForge", "Main");
    return app.exec();
}
