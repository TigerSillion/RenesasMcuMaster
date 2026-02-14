# WPF MVP 架构说明

## 分层
1. RenesasForge.App: WPF Shell + MVVM
2. RenesasForge.Core: 类型、引擎、抽象接口
3. RenesasForge.Protocol: 双协议解析与命令构建
4. RenesasForge.Transport.Serial: 串口实现

## 数据流
1. SerialPort 接收字节
2. ParserManager 解帧
3. DataEngine/VarEngine 消费
4. ViewModel 通过 Dispatcher 投影到 UI

## 性能基线
1. 16 通道 >= 60 FPS（典型负载）
2. UART 2Mbps 级稳定采集
3. 8 小时稳定运行
