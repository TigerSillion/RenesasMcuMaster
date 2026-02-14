#include "viewmodel/RecorderVM.h"
namespace rf {
RecorderVM::RecorderVM(RecordEngine* engine, QObject* parent) : QObject(parent), engine_(engine) {}
bool RecorderVM::recording() const { return engine_ && engine_->isRecording(); }
bool RecorderVM::startRecording(const QString& path) { if(!engine_) return false; bool ok = engine_->start(path); emit recordingChanged(); return ok; }
void RecorderVM::stopRecording() { if(engine_){ engine_->stop(); emit recordingChanged(); } }
}
