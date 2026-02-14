#include "core/RecordEngine.h"
#include <QtCore/QTextStream>
namespace rf {
RecordEngine::RecordEngine(QObject* parent) : QObject(parent) {}
bool RecordEngine::start(const QString& path) { if(file_.isOpen()) file_.close(); file_.setFileName(path); if(!file_.open(QIODevice::WriteOnly)) return false; file_.write("RFR1",4); return true; }
void RecordEngine::stop() { if(file_.isOpen()) file_.close(); }
bool RecordEngine::isRecording() const { return file_.isOpen(); }
bool RecordEngine::appendChunk(const RecordChunk& chunk) {
    if(!file_.isOpen()) return false;
    file_.write(reinterpret_cast<const char*>(&chunk.start_ts), sizeof(chunk.start_ts));
    file_.write(reinterpret_cast<const char*>(&chunk.end_ts), sizeof(chunk.end_ts));
    uint32_t sz = static_cast<uint32_t>(chunk.packed_samples.size());
    file_.write(reinterpret_cast<const char*>(&sz), sizeof(sz));
    file_.write(chunk.packed_samples);
    return file_.flush();
}
bool RecordEngine::exportCsv(const QString& path, const QVector<DataFrame>& frames) const {
    QFile out(path); if(!out.open(QIODevice::WriteOnly|QIODevice::Truncate|QIODevice::Text)) return false;
    QTextStream ts(&out); ts << "timestamp_us,channel_id,value\n";
    for(const DataFrame& df:frames){ for(const ChannelValue& cv:df.channels){ ts<<df.timestamp_us<<","<<cv.channel_id<<","<<cv.value<<"\n"; } }
    return true;
}
}
