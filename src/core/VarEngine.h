#pragma once
#include "common/types.h"
#include <QtCore/QHash>
#include <QtCore/QObject>
namespace rf {
class VarEngine : public QObject {
    Q_OBJECT
public:
    explicit VarEngine(QObject* parent = nullptr);
    void setDescriptors(const QVector<VariableDescriptor>& descriptors);
    QVector<VariableDescriptor> descriptors() const;
    void updateValue(uint32_t address, const QByteArray& raw);
    QByteArray valueRaw(uint32_t address) const;
private:
    QVector<VariableDescriptor> descriptors_;
    QHash<uint32_t, QByteArray> value_raw_;
};
}
