#include "core/VarEngine.h"
namespace rf {
VarEngine::VarEngine(QObject* parent) : QObject(parent) {}
void VarEngine::setDescriptors(const QVector<VariableDescriptor>& descriptors) { descriptors_ = descriptors; }
QVector<VariableDescriptor> VarEngine::descriptors() const { return descriptors_; }
void VarEngine::updateValue(uint32_t address, const QByteArray& raw) { value_raw_[address] = raw; }
QByteArray VarEngine::valueRaw(uint32_t address) const { return value_raw_.value(address); }
}
