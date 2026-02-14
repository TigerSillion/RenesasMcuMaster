#pragma once
#include "core/RecordEngine.h"
#include <QtCore/QObject>
namespace rf {
class RecorderVM : public QObject {
    Q_OBJECT
    Q_PROPERTY(bool recording READ recording NOTIFY recordingChanged)
public:
    explicit RecorderVM(RecordEngine* engine, QObject* parent = nullptr);
    bool recording() const;
public slots:
    bool startRecording(const QString& path);
    void stopRecording();
signals:
    void recordingChanged();
private:
    RecordEngine* engine_ = nullptr;
};
}
