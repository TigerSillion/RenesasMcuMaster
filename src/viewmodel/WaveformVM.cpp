#include "viewmodel/WaveformVM.h"
namespace rf {
WaveformVM::WaveformVM(DataEngine* engine, QObject* parent) : QObject(parent) {
    QObject::connect(engine, &DataEngine::dataFrameReady, this, [this](const DataFrame&){ ++frame_count_; emit frameCountChanged(); });
}
}
