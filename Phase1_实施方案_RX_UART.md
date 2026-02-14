# Phase 1 实施方案（WPF + .NET / RX UART）
## 目标平台：Renesas RX / UART 接口 / 可扩展框架

## 一、Phase 1 范围定义
### 1.1 本阶段做什么
1. 软件示波器：WPF + ScottPlot，16 通道可视化
2. 全局变量读写：批量读写 + 回读校验
3. 通信接口：UART（兼容 VOFA + 自研协议）
4. 数据存档：录制、回放、CSV 导出

### 1.2 本阶段不做但预留
1. USB CDC / SWD / RTT 传输实现
2. 崩溃诊断与智能模块
3. 跨平台 UI

## 二、软件架构（WPF 主线）
### 2.1 分层架构
1. UI：WPF Pages/Controls
2. ViewModel：MVVM + ICommand + Dispatcher
3. Core：DataEngine、VarEngine、RecordEngine
4. Protocol：IProtocolParser + 双协议实现
5. Transport：ITransport + SerialTransport

### 2.2 目录结构
```text
src-dotnet/
  RenesasForge.App
  RenesasForge.Core
  RenesasForge.Protocol
  RenesasForge.Transport.Serial
  RenesasForge.Tests.Unit
  RenesasForge.Tests.UiSmoke
```

## 三、通信协议设计（RForge Protocol）
### 3.1 设计原则
1. 高吞吐：流式帧尽量减少头开销
2. 鲁棒：CRC + 重同步
3. 兼容：支持 VOFA 文本流
4. 可扩展：命令字保留区

### 3.2 帧格式
- `AA55 + ver + cmd + seq + len + payload + crc16-ccitt`
- `payload <= 1024`

### 3.3 命令集
1. PING/ACK
2. STREAM_START/STOP
3. SET_STREAM_CONFIG
4. GET_VAR_TABLE
5. READ_MEM_BATCH
6. WRITE_MEM
7. STREAM_DATA

## 四、MCU 最小 SDK（RX）
### 4.1 目标
1. 保持现有 `mcu_sdk/rx_minimal` 接口不破坏
2. 输出最小编解码参考代码
3. 保障与 PC 协议一致

### 4.2 资源预算
1. 协议与缓冲占用控制在可接受范围
2. 支持 921600~2Mbps UART 运行

## 五、PC 模块实现要点（WPF）
### 5.1 渲染
1. ScottPlot.WPF 作为 MVP 渲染栈
2. 后续根据性能决定是否替换自研引擎

### 5.2 并发模型
1. SerialPort 事件线程只做采集
2. Parser/Engine 在后台队列消费
3. UI 线程只投影状态

### 5.3 错误恢复
1. 断线自动重连
2. CRC 错误计数与告警
3. 解析器模式手动覆盖

## 六、分步计划（4 个月）
1. Sprint 1：.NET 工程基线 + 串口连接闭环
2. Sprint 2：双协议 + 数据引擎
3. Sprint 3：示波器与变量系统
4. Sprint 4：录制回放 + 稳定性打磨

## 七、迁移约束
1. Qt 目录冻结为 legacy
2. 仅 `src-dotnet` 接受新增功能
3. 文档与技能均以 WPF 方案为主

## 八、里程碑验收
1. 程序可启动并连接串口
2. 协议可解析并显示数据
3. 变量读写可回读验证
4. 录制回放与 CSV 导出可用
5. 长稳测试通过

## 九、联调基线（2026-02-15）
1. 首选使用虚拟串口 + `tools/uart_mcu_sim.py` 进行 PC 侧闭环验证。
2. 仿真脚本支持：RForge/VOFA、CRC 注入、丢包注入、MAP 变量导入。
3. 联调操作说明见：`docs/uart_simulator_guide.md`。
