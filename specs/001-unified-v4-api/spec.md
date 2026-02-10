# Feature Specification: 统一 V4 API 重构

**Feature Branch**: `001-unified-v4-api`
**Created**: 2026-02-05
**Status**: Draft
**Input**: 基于 docs/TsFile_full_design.md 重构 C# TsFile 实现，V4 作为默认版本

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 使用统一 API 写入时序数据 (Priority: P1)

作为 C# 开发者，我希望使用统一的 `TsFileWriter` API 写入时序数据，系统默认使用 V4 格式。

**Why this priority**: 写入是核心功能，统一 API 简化学习曲线。

**Independent Test**: 创建 `TsFileWriter` 实例，写入数据并验证生成 V4 格式文件。

**Acceptance Scenarios**:

1. **Given** 创建 `TsFileWriter`, **When** 不指定版本, **Then** 默认生成 V4 格式
2. **Given** 写入树模型数据, **When** 注册时间序列并写入, **Then** 设备 ID 自动转换为 StringArrayDeviceId
3. **Given** 写入表模型数据, **When** 注册表并写入 Tablet, **Then** 包含完整 TableSchema

---

### User Story 2 - 使用统一 API 读取时序数据 (Priority: P1)

作为 C# 开发者，我希望使用统一的 `TsFileReader` API 读取数据，自动识别文件版本。

**Why this priority**: 读取与写入同等重要，需透明处理 V3/V4 格式。

**Independent Test**: 读取 V3 和 V4 格式测试文件，验证数据正确性。

**Acceptance Scenarios**:

1. **Given** V4 格式 TsFile, **When** 使用 `TsFileReader` 查询, **Then** 正确读取所有数据
2. **Given** V3 格式 TsFile, **When** 使用 `TsFileReader` 查询, **Then** 正确读取（向后兼容）

---

### User Story 3 - 树模型数据组织 (Priority: P2)

作为从 IoTDB 迁移的开发者，我希望继续使用树模型（Device/Measurement）组织时序数据。

**Why this priority**: 树模型是传统数据组织方式，需保持兼容性支持现有用户。

**Independent Test**: 注册设备和测点，写入数据并验证索引结构。

**Acceptance Scenarios**:

1. **Given** 注册 "root.db1.d1.s1", **When** 写入数据, **Then** 设备 ID 转换为 {"root.db1", "d1"}
2. **Given** 多设备数据, **When** 文件关闭, **Then** 生成正确的 MetadataIndexNode 树结构

---

### User Story 4 - 表模型数据组织 (Priority: P2)

作为需要处理结构化时序数据的开发者，我希望使用表模型（Table/Column）组织数据。

**Why this priority**: 表模型是 V4 核心新功能，提供更灵活的数据组织和更高效的查询。

**Independent Test**: 注册表结构，写入 Tablet 数据并验证 TableSchema。

**Acceptance Scenarios**:

1. **Given** 注册含 ID 列和 MEASUREMENT 列的表, **When** 写入 Tablet, **Then** 数据按设备分组，TableSchema 正确
2. **Given** 表模型数据, **When** 使用表视图查询, **Then** 能按标识列过滤、按测点列投影

---

### User Story 5 - Java 互操作性 (Priority: P3)

作为在混合环境中工作的开发者，我希望 C# 生成的 TsFile 能被 Java 正确读取，反之亦然。

**Why this priority**: 互操作性是 Apache TsFile 项目的核心价值。

**Independent Test**: C# 写入文件 Java 读取验证，Java 写入文件 C# 读取验证。

**Acceptance Scenarios**:

1. **Given** C# 写入的 V4 TsFile, **When** Java 读取, **Then** 数据完全一致
2. **Given** Java 写入的 V4 TsFile, **When** C# 读取, **Then** 数据完全一致

---

### Edge Cases

- 文件版本无法识别时，抛出明确异常说明支持的版本范围
- 树模型设备 ID 包含特殊字符（点号）时，正确处理转义
- 表模型 ID 列前缀不匹配时（合并场景），抛出明确错误
- 读取 V3 文件时 TableSchema 为空，应能正常处理
- Chunk 只有一个 Page 时分隔符为 0x05，多个 Page 时为 0x01

## Requirements *(mandatory)*

### Functional Requirements

#### API 统一化
- **FR-001**: 系统必须提供统一的 `TsFileWriter` 类，默认生成 V4 格式文件
- **FR-002**: 系统必须提供统一的 `TsFileReader` 类，能自动识别并读取 V3 和 V4 格式
- **FR-003**: 系统必须移除 `TsFileWriterV4` 和 `TsFileReaderV4` 后缀类
- **FR-004**: 系统必须保留 V3 格式的读取能力，但不再作为默认写入格式

#### 树模型支持
- **FR-005**: 系统必须支持通过 `registerTimeseries()` 注册时间序列
- **FR-006**: 系统必须支持通过 `writeRecord()` 和 `writeTablet()` 写入树模型数据
- **FR-007**: 系统必须将 PlainDeviceId 自动转换为 StringArrayDeviceId
- **FR-008**: 系统必须为树模型数据自动生成 LogicalTableSchema

#### 表模型支持
- **FR-009**: 系统必须支持通过 `registerTable()` 注册表结构
- **FR-010**: 系统必须支持 ID 列和 MEASUREMENT 列两种列类型
- **FR-011**: 系统必须支持通过 `writeTable()` 写入表模型数据
- **FR-012**: 系统必须在写入时按 DeviceId 对 Tablet 进行拆分

#### 索引结构
- **FR-013**: 系统必须生成 V4 格式的 MetadataIndexNode 树结构
- **FR-014**: 系统必须为每个表生成独立的索引树根节点
- **FR-015**: 系统必须在 TsFileMetadata 中记录 tableIndexRoots 映射

#### 查询支持
- **FR-016**: 系统必须支持树视图查询接口（QueryExpression）
- **FR-017**: 系统必须支持表视图查询接口
- **FR-018**: 系统必须支持设备顺序结果集

### Key Entities

- **TsFileWriter**: 统一的文件写入器，支持树模型和表模型，默认生成 V4 格式
- **TsFileReader**: 统一的文件读取器，自动识别 V3/V4 格式
- **TableSchema**: 表结构定义，包含表名、列定义列表、列类型列表
- **StringArrayDeviceId**: V4 格式设备标识，数组形式（表名 + 标识列值）
- **MetadataIndexNode**: 索引节点，支持四种类型
- **Tablet**: 批量数据容器，支持列式存储和空值标记

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 使用统一 API 完成基本读写操作的代码行数减少 30% 以上
- **SC-002**: C# 生成的 V4 文件能被 Java 版本 100% 正确读取
- **SC-003**: Java 生成的 V4 文件能被 C# 版本 100% 正确读取
- **SC-004**: V3 格式文件的读取兼容性保持 100%
- **SC-005**: 表模型查询相比树模型查询，查询耗时减少 70% 以上（基准：V4 格式，100 万数据点规模，单设备多测点查询场景，使用相同的编码和压缩配置）
- **SC-006**: 所有现有单元测试在 API 重构后继续通过
- **SC-007**: 新增的统一 API 测试覆盖率达到 80% 以上

## Assumptions

1. **DEFAULT_SEGMENT_NUM_FOR_TABLE_NAME**: 默认值为 3，即 "root.db1.d1" 转换为 {"root.db1", "d1"}
2. **maxDegree**: 索引树最大度数使用 Java 版本默认配置
3. **blockSize**: 查询结果批大小使用合理默认值
4. **编码/压缩**: 继续使用现有实现，不在本次重构范围内
5. **TimeOrderedBlockReader**: 时间顺序结果集暂不实现（与 Java 版本一致）

## Out of Scope

1. 新增编码类型（CHIMP, SPRINTZ, RLBE）的实现
2. LZMA2 压缩支持（.NET 10 不可用）
3. 文件加密功能
4. 跨空间合并功能
5. 时间顺序结果集（TimeOrderedBlockReader）
