#pragma once
#include "common/types.h"
#include <QtCore/QFile>
#include <QtCore/QObject>
namespace rf {
class RecordEngine : public QObject {
    Q_OBJECT
public:
    explicit RecordEngine(QObject* parent = nullptr);
    bool start(const QString& path);
    void stop();
    bool isRecording() const;
    bool appendChunk(const RecordChunk& chunk);
    bool exportCsv(const QString& path, const QVector<DataFrame>& frames) const;
private:
    QFile file_;
};
}
