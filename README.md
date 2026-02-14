# RenesasMcuMaster (RenesasForge MVP)

当前主线已迁移到 `WPF + .NET`，用于交付 Windows 首发的 UART 商用 MVP：

1. 软件示波器
2. 全局变量读写
3. 录制回放与 CSV 导出
4. 串口稳定性与协议鲁棒性

## 当前状态

1. 主线代码目录：`src-dotnet/`
2. 入口方案文件：`RenesasForge.slnx`
3. 旧 Qt 目录：`src/`（legacy 冻结，不再新增功能）

## 快速开始

```powershell
dotnet build RenesasForge.slnx -c Debug
dotnet run --project src-dotnet/RenesasForge.App
dotnet test src-dotnet/RenesasForge.Tests.Unit/RenesasForge.Tests.Unit.csproj -c Debug
```

## 文档入口（最新）

1. MVP 实施总览：`mvp_implementation_plan.md`
2. Phase 1 方案：`Phase1_实施方案_RX_UART.md`
3. 平台规划：`mcu_platform_plan.md`
4. WPF 架构：`docs/architecture/wpf_mvp_architecture.md`
5. Qt->WPF 迁移差异：`docs/migration/qt_to_wpf_gap_analysis.md`
6. Legacy 冻结策略：`docs/migration/legacy_qt_freeze_policy.md`
7. UART MCU 模拟器：`docs/uart_simulator_guide.md`

## 说明

已删除历史无效文档（编码损坏的旧版高速通信文档），仓库文档以以上清单为准。
