# RenesasForge 全面规划（WPF/.NET 版本）

## 一、定位与策略
- 保持“一站式 MCU 调试平台”目标不变。
- PC 技术路线统一为 `C# + .NET + WPF`，优先 Windows 商用效率。

## 二、产品架构（五层）
1. Presentation：WPF UI + MVVM
2. Analysis：规则引擎（MVP 后）
3. Data：缓冲、录制、回放、导出
4. Communication：UART/USB CDC/SWD/RTT 适配层
5. HAL：探针与设备驱动接口

## 三、技术选型（更新）
| 层面 | 方案 | 说明 |
|---|---|---|
| UI 框架 | WPF + XAML | Windows 首发、开发效率高 |
| 语言 | C# (.NET) | 迭代速度与维护性更优 |
| 构建 | dotnet SDK + slnx | 标准化 CI/CD |
| 串口 | System.IO.Ports | 无额外重型依赖 |
| 图形渲染 | ScottPlot.WPF（MVP） | 快速达成可用性 |
| 测试 | xUnit + MSTest UI smoke | 单测 + 最小 UI 保障 |

## 四、核心模块（目标不变）
1. 高性能示波器
2. 存档回放
3. 全局变量高速读写
4. 控件系统
5. MCU 深度诊断（后续）
6. 崩溃分析（后续）
7. 电机专项（后续）
8. 触摸专项（后续）

## 五、路线图（更新）
### Phase 1（0-6 月）
1. WPF MVP：示波器 + 变量 + UART + 录制
2. 完成商用首版 v1.0

### Phase 2（6-12 月）
1. USB CDC/混合链路
2. 诊断与性能增强

### Phase 3（12-18 月）
1. 领域智能能力
2. 插件生态建设

## 六、团队画像（更新）
| 角色 | 人数 | 能力 |
|---|---|---|
| 技术负责人/架构师 | 1 | 嵌入式 + .NET 桌面架构 |
| PC 高级开发（WPF/.NET） | 2 | MVVM/并发/可视化 |
| 嵌入式工具开发 | 1 | RX 协议与调试链路 |
| 测试与发布 | 1 | 自动化回归与发布 |

## 七、跨平台策略说明
- 本期不追求跨平台 GUI。
- 后续评估 Avalonia/MAUI，但不影响当前接口设计。

## 八、工程文档入口（2026-02-15）
1. 总览：`README.md`
2. MVP 方案：`mvp_implementation_plan.md`
3. Phase 1：`Phase1_实施方案_RX_UART.md`
4. 架构：`docs/architecture/wpf_mvp_architecture.md`
5. UART 仿真：`docs/uart_simulator_guide.md`
