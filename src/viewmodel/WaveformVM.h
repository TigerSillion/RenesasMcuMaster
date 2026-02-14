#pragma once
#include "core/DataEngine.h"
#include <QtCore/QObject>
namespace rf {
class WaveformVM : public QObject {
    Q_OBJECT
    Q_PROPERTY(int frameCount READ frameCount NOTIFY frameCountChanged)
public:
    explicit WaveformVM(DataEngine* engine, QObject* parent = nullptr);
    int frameCount() const { return frame_count_; }
signals:
    void frameCountChanged();
private:
    int frame_count_ = 0;
};
}
