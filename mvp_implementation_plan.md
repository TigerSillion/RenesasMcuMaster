# RenesasForge MVP 实施方案（WPF + .NET 主线）
## 聚焦范围：软件示波器 + 全局变量读写 | RX 平台 | UART 接口 | 可扩展框架

## 一、迁移公告
- 自 2026-02-14 起，PC 主线技术栈由 `Qt6/QML/C++/CMake` 迁移为 `WPF/XAML/C#/.NET SDK`。
- `src/` 与 `CMakeLists.txt` 进入 legacy 冻结状态，仅允许修复阻塞缺陷。
- 新增主线目录：`src-dotnet/`，以 `RenesasForge.slnx` 为唯一执行入口。

## 二、MVP 目标与边界
### 2.1 版本切分
1. v1.0：UART 商用版（稳定、可交付）
2. v1.1：高速版（USB CDC/混合链路，8Mbps 目标）

### 2.2 v1.0 范围
1. Windows 首发
2. WPF 示波器 + 变量读写 + 录制回放 + 串口稳定性
3. 双协议：自研二进制 + VOFA 兼容
4. 交付物：PC 程序 + 最小 RX 参考 SDK

### 2.3 非范围
1. AI/智能诊断
2. SWD/RTT/USB CDC 实现（仅接口预留）
3. 跨平台 GUI（后续评估 Avalonia/MAUI）

## 三、架构（WPF + MVVM）
### 3.1 分层
1. UI 层（WPF/XAML）：WavePanel、VariablePanel、ConnectionPanel、RecorderPanel
2. ViewModel 层：INotifyPropertyChanged + ICommand
3. Core 层：DataEngine、VarEngine、RecordEngine
4. Protocol 层：RForgeBinaryParser、VofaCompatibleParser、ParserManager
5. Transport 层：ITransport、SerialTransport

### 3.2 线程模型
1. Serial I/O：System.IO.Ports 事件线程
2. Parser：后台 Task + Channel
3. Core Engine：后台处理与缓冲
4. UI：Dispatcher 只做状态投影与渲染调度

## 四、核心接口（C#）
### 4.1 ITransport
- Task<bool> OpenAsync(TransportConfig cfg, CancellationToken ct)
- Task CloseAsync()
- bool IsOpen { get; }
- Task<int> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
- int Read(Span<byte> buffer)
- int BytesAvailable { get; }
- events: DataReceived, ErrorOccurred, StateChanged

### 4.2 IProtocolParser
- void Feed(ReadOnlySpan<byte> bytes)
- bool TryPopFrame(out Frame frame)
- byte[] BuildCommand(CommandId cmd, ReadOnlySpan<byte> payload)
- ParserType Type { get; }
- void Reset()

### 4.3 类型
- DataFrame, ChannelValue, VariableDescriptor, MemoryRequest, RecordChunk, Frame

## 五、协议固定规范（不变）
1. 字节序：Little Endian
2. 帧头：AA 55
3. 帧字段：ver(1) | cmd(1) | seq(2) | len(2) | payload | crc16(2)
4. CRC：CRC16-CCITT
5. 最大 payload：1024 bytes
6. 恢复策略：CRC 错误后从 SOF 重同步

## 六、目录结构（新）
```text
src-dotnet/
  RenesasForge.App
  RenesasForge.Core
  RenesasForge.Protocol
  RenesasForge.Transport.Serial
  RenesasForge.Tests.Unit
  RenesasForge.Tests.UiSmoke
mcu_sdk/rx_minimal
```

## 七、测试栈与验收
### 7.1 测试栈
1. Unit：xUnit + FluentAssertions
2. UI Smoke：MSTest
3. Integration：串口回环/重连脚本（PowerShell）

### 7.2 v1.0 验收
1. 16 通道典型负载 >= 60 FPS
2. UART 2Mbps 级稳定采集，无明显丢帧
3. 变量读写典型 < 10ms
4. 连续录制 >= 1 小时，可回放与 CSV 导出
5. 8 小时运行无崩溃，内存增长受控

## 八、里程碑（10 周）
1. Week 1-2：工程骨架 + 串口闭环（PING/ACK）
2. Week 3-4：双协议 + 自动探测/手动覆盖
3. Week 5-6：波形引擎 + 基础测量
4. Week 7-8：变量读写 + 录制回放
5. Week 9-10：稳定性 + 打包发布

## 九、执行入口
1. 构建：`dotnet build RenesasForge.slnx -c Debug`
2. 运行：`dotnet run --project src-dotnet/RenesasForge.App`
3. 测试：`dotnet test RenesasForge.slnx`
4. UART 仿真联调：`python tools/uart_mcu_sim.py --port COMx --protocol rforge --auto-stream`

## 十、文档基线（2026-02-15）
1. 本文档 + `Phase1_实施方案_RX_UART.md` + `mcu_platform_plan.md` 为产品主文档。
2. 架构细节见 `docs/architecture/wpf_mvp_architecture.md`。
3. 迁移与 legacy 策略见 `docs/migration/*`。
4. 历史损坏文档已清理，仓库 README 为统一入口。
