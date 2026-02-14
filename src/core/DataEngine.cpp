#include "core/DataEngine.h"
#include <cstring>
namespace {
inline uint16_t readU16Le(const QByteArray& b, int i) { return static_cast<uint16_t>(static_cast<uint8_t>(b[i])) | static_cast<uint16_t>(static_cast<uint8_t>(b[i + 1]) << 8); }
inline uint64_t readU64Le(const QByteArray& b, int i) { uint64_t v=0; for(int k=0;k<8;++k){ v |= static_cast<uint64_t>(static_cast<uint8_t>(b[i+k])) << (8*k);} return v; }
inline float readF32Le(const QByteArray& b, int i) { uint32_t bits=0; for(int k=0;k<4;++k){ bits |= static_cast<uint32_t>(static_cast<uint8_t>(b[i+k])) << (8*k);} float f=0.0f; std::memcpy(&f,&bits,sizeof(float)); return f; }
}
namespace rf {
DataEngine::DataEngine(QObject* parent) : QObject(parent) {}
void DataEngine::appendFrame(const Frame& frame) {
    if (frame.cmd != CommandId::StreamData) return;
    if (frame.payload.size() >= 8 && ((frame.payload.size() - 8) % 6 == 0)) {
        DataFrame df; df.timestamp_us = readU64Le(frame.payload,0);
        for (int i=8;i+5<frame.payload.size();i+=6) df.channels.push_back(ChannelValue{readU16Le(frame.payload,i), static_cast<double>(readF32Le(frame.payload,i+2))});
        frames_.push_back(df); if (frames_.size()>max_frames_) frames_.remove(0, frames_.size()-max_frames_); emit dataFrameReady(df); return;
    }
    const QList<QByteArray> parts = frame.payload.split(',');
    DataFrame df; df.timestamp_us = nowUs(); uint16_t ch=0;
    for (const QByteArray& p : parts) { bool ok=false; double v=p.trimmed().toDouble(&ok); if(ok) df.channels.push_back(ChannelValue{ch++, v}); }
    if (df.channels.isEmpty()) return;
    frames_.push_back(df); if (frames_.size()>max_frames_) frames_.remove(0, frames_.size()-max_frames_); emit dataFrameReady(df);
}
QVector<DataFrame> DataEngine::recentFrames(int maxCount) const { if (maxCount<=0 || frames_.isEmpty()) return {}; int start=qMax(0, frames_.size()-maxCount); return frames_.mid(start); }
}
