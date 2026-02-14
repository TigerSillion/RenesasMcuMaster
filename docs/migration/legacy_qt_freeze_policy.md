# Legacy Qt Freeze Policy

## 范围
- `src/` 与 `CMakeLists.txt` 视为 legacy 资产。

## 允许变更
1. 阻塞构建问题修复（仅为了历史可读性）
2. 注释与文档链接修复

## 禁止变更
1. 新增功能
2. 架构级重构
3. 新模块引入

## 主线要求
- 所有新功能与新架构只允许在 `src-dotnet/` 落地。
