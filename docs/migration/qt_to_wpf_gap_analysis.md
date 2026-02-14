# Qt -> WPF 迁移差异分析

## 1. 变更范围
1. UI 层：QML -> XAML
2. 并发模型：QThread -> Task/Channel + Dispatcher
3. 串口层：QSerialPort -> System.IO.Ports
4. 构建：CMake -> dotnet SDK
5. 测试：QtTest/GTest -> xUnit/MSTest

## 2. 影响评估
1. 接口语义保持不变，可平移 `ITransport/IProtocolParser`
2. 线程调度与错误恢复逻辑需重写
3. 渲染栈替换后性能目标要重新基准化

## 3. 风险与缓解
1. 文档残留旧术语：引入关键词扫描门禁
2. 双栈混用：冻结 Qt 目录，新增仅走 src-dotnet
3. 迁移期回归风险：先保障协议与传输层单测
