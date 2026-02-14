#include "viewmodel/VariableVM.h"
namespace rf {
VariableVM::VariableVM(VarEngine* engine, QObject* parent) : QObject(parent), engine_(engine) { refreshCount(); }
void VariableVM::refreshCount() { if(!engine_) return; variable_count_ = engine_->descriptors().size(); emit variableCountChanged(); }
}
