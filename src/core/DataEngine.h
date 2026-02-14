#pragma once
#include "common/types.h"
#include <QtCore/QObject>
#include <QtCore/QVector>
namespace rf {
class DataEngine : public QObject {
    Q_OBJECT
public:
    explicit DataEngine(QObject* parent = nullptr);
    void appendFrame(const Frame& frame);
    QVector<DataFrame> recentFrames(int maxCount) const;
signals:
    void dataFrameReady(const rf::DataFrame& frame);
private:
    QVector<DataFrame> frames_;
    int max_frames_ = 4096;
};
}
