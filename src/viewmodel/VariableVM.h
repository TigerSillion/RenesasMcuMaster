#pragma once
#include "core/VarEngine.h"
#include <QtCore/QObject>
namespace rf {
class VariableVM : public QObject {
    Q_OBJECT
    Q_PROPERTY(int variableCount READ variableCount NOTIFY variableCountChanged)
public:
    explicit VariableVM(VarEngine* engine, QObject* parent = nullptr);
    int variableCount() const { return variable_count_; }
public slots:
    void refreshCount();
signals:
    void variableCountChanged();
private:
    VarEngine* engine_ = nullptr;
    int variable_count_ = 0;
};
}
