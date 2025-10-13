# Archived Files - 已归档文件

## 说明
此目录包含未使用的模块化架构文件，已从主项目中移除。

这些文件保留用于将来可能的模块化重构（步骤5）。

## 归档时间
2025-01-13

## 归档文件清单

### 核心文件
- **init.lua** - 模块化架构初始化文件
- **Core.lua** - 核心显示框架
- **Utils.lua** - 工具函数库

### 模块目录
- **Modules/** - 包含所有数据模块
  - Player.lua - 玩家数据模块
  - Target.lua - 目标数据模块
  - Combat.lua - 战斗数据模块
  - Inventory.lua - 背包数据模块
  - Status.lua - 状态标志模块

## 当前使用的架构
- **DataToText.lua** - 简化版主逻辑（正在使用）
- **DataToText.xml** - UI定义文件（正在使用）

## 恢复说明
如果需要恢复模块化架构，将此目录中的文件移回主目录，并更新 DataToText.toc 文件。
