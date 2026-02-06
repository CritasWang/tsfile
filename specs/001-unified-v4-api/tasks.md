# Tasks: 统一 V4 API 重构

**Input**: Design documents from `/specs/001-unified-v4-api/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1-US5)

## Path Conventions

- **Source**: `csharp/src/Apache.TsFile/`
- **Tests**: `csharp/tests/Apache.TsFile.Tests/`

---

## Phase 1: Setup

**Purpose**: 准备重构环境

- [x] T001 创建功能分支备份 `git branch backup-before-unified-api`
- [x] T002 [P] 运行现有测试确保基线通过 `dotnet test csharp/tests/Apache.TsFile.Tests/`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: 增强共享组件，为统一 API 做准备

**⚠️ CRITICAL**: 所有用户故事依赖此阶段完成

- [x] T003 增强 TableSchema 支持树模型兼容 in `csharp/src/Apache.TsFile/Schema/TableSchema.cs`
- [x] T004 [P] 合并 TabletV4 到 Tablet 类 in `csharp/src/Apache.TsFile/Tablet.cs`
- [x] T005 [P] 添加 TsFileConstants.DefaultFileVersion = 4 in `csharp/src/Apache.TsFile/Common/TsFileConstants.cs`

**Checkpoint**: 基础组件就绪，可开始用户故事实现

---

## Phase 3: User Story 1 - 统一写入 API (Priority: P1) 🎯 MVP

**Goal**: 提供统一的 TsFileWriter，默认生成 V4 格式

**Independent Test**: 创建 TsFileWriter 实例，写入数据，验证生成 V4 格式文件

### Implementation for User Story 1

- [x] T006 [US1] 添加 FileVersion 属性到 TsFileWriter in `csharp/src/Apache.TsFile/IO/TsFileWriter.cs`
- [x] T007 [US1] 重构 TsFileWriter 构造函数支持版本参数 in `csharp/src/Apache.TsFile/IO/TsFileWriter.cs`
- [x] T008 [US1] 合并 TsFileWriterV4 的 V4 写入逻辑到 TsFileWriter in `csharp/src/Apache.TsFile/IO/TsFileWriter.cs`
- [x] T009 [US1] 实现 WriteHeader 支持 V4 版本号 in `csharp/src/Apache.TsFile/IO/TsFileWriter.cs`
- [x] T010 [US1] 实现 WriteTsFileMetadata 支持 V4 索引结构 in `csharp/src/Apache.TsFile/IO/TsFileWriter.cs`
- [x] T011 [US1] 删除 TsFileWriterV4.cs 文件 `rm csharp/src/Apache.TsFile/IO/TsFileWriterV4.cs`
- [x] T012 [US1] 更新 TsFileWriter 单元测试 in `csharp/tests/Apache.TsFile.Tests/TsFileWriterTests.cs`

**Checkpoint**: TsFileWriter 统一完成，可独立测试写入功能 ✅

---

## Phase 4: User Story 2 - 统一读取 API (Priority: P1)

**Goal**: 提供统一的 TsFileReader，自动识别 V3/V4 格式

**Independent Test**: 读取 V3 和 V4 格式测试文件，验证数据正确性

### Implementation for User Story 2

- [x] T013 [US2] 添加 FileVersion 属性到 TsFileReader in `csharp/src/Apache.TsFile/IO/TsFileReader.cs`
- [x] T014 [US2] 实现自动版本检测逻辑 in `csharp/src/Apache.TsFile/IO/TsFileReader.cs`
- [x] T015 [US2] 合并 TsFileReaderV4 的 V4 读取逻辑到 TsFileReader in `csharp/src/Apache.TsFile/IO/TsFileReader.cs`
- [x] T016 [US2] 实现 V3 兼容读取（空字符串表名） in `csharp/src/Apache.TsFile/IO/TsFileReader.cs`
- [x] T017 [US2] 删除 TsFileReaderV4.cs 文件 `rm csharp/src/Apache.TsFile/IO/TsFileReaderV4.cs`
- [x] T018 [US2] 更新 TsFileReader 单元测试 in `csharp/tests/Apache.TsFile.Tests/TsFileReaderTests.cs`

**Checkpoint**: TsFileReader 统一完成，可独立测试读取功能 ✅

---

## Phase 5: User Story 3 - 树模型数据组织 (Priority: P2)

**Goal**: 支持传统树模型（Device/Measurement）数据组织

**Independent Test**: 注册 "root.db1.d1.s1"，写入数据，验证设备 ID 转换正确

### Implementation for User Story 3

- [x] T019 [US3] 实现 RegisterTimeseries 方法 in `csharp/src/Apache.TsFile/IO/TsFileWriter.cs`
- [x] T020 [US3] 实现 PlainDeviceId 到 StringArrayDeviceId 自动转换 in `csharp/src/Apache.TsFile/IO/IDeviceID.cs`
- [x] T021 [US3] 实现 LogicalTableSchema 自动生成 in `csharp/src/Apache.TsFile/Schema/TableSchema.cs`
- [x] T022 [US3] 添加树模型写入测试 in `csharp/tests/Apache.TsFile.Tests/TreeModelTests.cs`

**Checkpoint**: 树模型数据组织完成，可独立测试 ✅

---

## Phase 6: User Story 4 - 表模型数据组织 (Priority: P2)

**Goal**: 支持表模型（Table/Column）数据组织

**Independent Test**: 注册表结构，写入 Tablet，验证 TableSchema 正确

### Implementation for User Story 4

- [x] T023 [US4] 实现 RegisterTable 方法 in `csharp/src/Apache.TsFile/IO/TsFileWriter.cs`
- [x] T024 [US4] 实现 WriteTable 方法 in `csharp/src/Apache.TsFile/IO/TsFileWriter.cs`
- [x] T025 [US4] 实现 QueryTable 表视图查询 in `csharp/src/Apache.TsFile/IO/TsFileReader.cs`
- [x] T026 [US4] 添加表模型写入测试 in `csharp/tests/Apache.TsFile.Tests/TableModelTests.cs`

**Checkpoint**: 表模型数据组织完成，可独立测试 ✅

---

## Phase 7: User Story 5 - Java 互操作性 (Priority: P3)

**Goal**: 确保 C# 和 Java 版本的 TsFile 完全互操作

**Independent Test**: C# 写入文件 Java 读取，Java 写入文件 C# 读取

### Implementation for User Story 5

- [x] T027 [US5] 生成 Java V4 测试文件 `java/examples/Tablet.tsfile`
- [x] T028 [US5] 添加 C# 读取 Java V4 文件测试 in `csharp/tests/Apache.TsFile.Tests/TsFileV4InteropTests.cs`
- [x] T029 [US5] 添加 Java 读取 C# V4 文件测试 in `java/interop-tests/` (CSharpFileValidator 已创建)
- [x] T030 [US5] 验证二进制格式完全兼容 (C# 互操作测试 14/14 通过，CI 配置已修复)

**Checkpoint**: Java 互操作性验证完成 ✅

---

## Phase 8: V4 查询实现 (Priority: P1 - 关键缺失功能)

**Goal**: 实现 V4 文件的数据查询功能（当前 Query() 返回空结果）

**Independent Test**: 写入 V4 文件后使用 Query() 读取数据，验证返回正确结果

**背景**: 根据 plan.md 分析，以下组件已完成但未集成：
- MetadataIndexNode.GetChildIndexEntry() ✅
- MetadataIndexNode.BinarySearchInChildren() ✅
- TimeseriesMetadataV4.Deserialize() ✅
- ChunkMetadataV4.OffsetOfChunkHeader ✅

### Implementation for V4 Query

- [x] T036 [US2] 实现 NavigateToDevice() 方法遍历设备索引树 in `csharp/src/Apache.TsFile/IO/TsFileReader.cs`
- [x] T037 [US2] 实现 NavigateToMeasurement() 方法遍历测点索引树 in `csharp/src/Apache.TsFile/IO/TsFileReader.cs`
- [x] T038 [US2] 实现 ReadChunkV4() 方法根据 offset 读取 Chunk in `csharp/src/Apache.TsFile/IO/TsFileReader.cs`
- [x] T039 [US2] 实现 ReadPageV4() 方法解析 Page 数据 in `csharp/src/Apache.TsFile/IO/TsFileReader.cs`
- [x] T040 [US2] 修改 Query() 方法集成 V4 查询逻辑（替换 L97 空返回） in `csharp/src/Apache.TsFile/IO/TsFileReader.cs`
- [x] T041 [US2] 添加时间范围过滤（使用 Statistics.MinTime/MaxTime） in `csharp/src/Apache.TsFile/IO/TsFileReader.cs`
- [x] T042 [US2] 添加 V4 查询单元测试 in `csharp/tests/Apache.TsFile.Tests/TsFileV4QueryTests.cs`
- [x] T043 [US5] 添加 Java V4 文件查询互操作测试 in `csharp/tests/Apache.TsFile.Tests/TsFileV4InteropTests.cs`

**Checkpoint**: V4 查询功能完成，Query() 返回正确数据 ✅

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: 清理和文档更新

- [x] T031 [P] 更新 CLAUDE.md 移除 V4 后缀说明 in `CLAUDE.md`
- [x] T032 [P] 更新 csharp/STATUS.md 反映统一 API in `csharp/STATUS.md`
- [x] T033 [P] 更新 csharp/USER_MANUAL.md 使用示例 in `csharp/USER_MANUAL.md`
- [x] T034 运行完整测试套件验证 `dotnet test csharp/` (182 pass, 1 skip)
- [x] T035 运行 quickstart.md 验证示例代码
- [x] T044 更新文档说明 V4 查询功能 in `csharp/USER_MANUAL.md`
- [x] T045 运行完整测试套件验证 V4 查询 `dotnet test csharp/` (187 pass, 1 skip)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: 无依赖，立即开始
- **Foundational (Phase 2)**: 依赖 Setup 完成，阻塞所有用户故事
- **User Stories (Phase 3-7)**: 依赖 Foundational 完成
- **V4 Query (Phase 8)**: 依赖 US2 基础完成，是 US2 的关键补充
- **Polish (Phase 9)**: 依赖所有用户故事和 V4 查询完成

### User Story Dependencies

| Story | 依赖 | 可并行 | 状态 |
|-------|------|--------|------|
| US1 (写入) | Foundational | 是 | ✅ 完成 |
| US2 (读取) | Foundational | 是 | ✅ 完成（含 V4 查询） |
| US3 (树模型) | US1 | 否 | ✅ 完成 |
| US4 (表模型) | US1 | 否 | ✅ 完成 |
| US5 (互操作) | US1, US2 | 否 | ✅ 完成 |

### Parallel Opportunities

```bash
# Phase 2 可并行任务
Task T004: 合并 TabletV4
Task T005: 添加 DefaultFileVersion

# US1 和 US2 可并行（不同文件）
Task T006-T012: TsFileWriter 重构
Task T013-T018: TsFileReader 重构
```

---

## Implementation Strategy

### MVP First (仅 User Story 1)

1. 完成 Phase 1: Setup
2. 完成 Phase 2: Foundational
3. 完成 Phase 3: User Story 1 (统一写入)
4. **验证**: 测试 TsFileWriter 生成 V4 文件
5. 可部署/演示

### Incremental Delivery

1. Setup + Foundational → 基础就绪
2. US1 (写入) → 独立测试 → MVP
3. US2 (读取) → 独立测试 → 完整读写
4. US3 + US4 → 树/表模型支持
5. US5 → Java 互操作验证

---

## Summary

| 指标 | 数量 |
|------|------|
| 总任务数 | 45 |
| Setup 任务 | 2 |
| Foundational 任务 | 3 |
| US1 任务 | 7 |
| US2 任务 | 6 |
| US3 任务 | 4 |
| US4 任务 | 4 |
| US5 任务 | 4 |
| **V4 查询任务** | **8** |
| Polish 任务 | 7 |
| 可并行任务 | 8 |
| **已完成任务** | **45** |
| **待完成任务** | **0** |

## 关键缺失功能

~~根据 plan.md 分析，以下功能是 **关键缺失**：~~

| 功能 | 任务 | 状态 |
|------|------|--------|
| V4 数据查询 | T036-T040 | ✅ 已完成 |
| 时间范围过滤 | T041 | ✅ 已完成 |
| V4 查询测试 | T042 | ✅ 已完成 |
| Java V4 互操作测试 | T043 | ✅ 已完成 |

**当前状态**: TsFileReader.Query() 对 V4 文件返回正确数据 ✅
