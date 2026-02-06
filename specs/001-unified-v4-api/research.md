# Research: 统一 V4 API 重构

**Date**: 2026-02-05
**Branch**: `001-unified-v4-api`

## 1. 现有代码分析

### 1.1 当前 API 结构

| 类 | 用途 | 状态 |
|---|------|------|
| `TsFileWriter` | V3 格式写入 | 将重构为统一入口 |
| `TsFileWriterV4` | V4 格式写入 | 将合并到 TsFileWriter |
| `TsFileReader` | V3 格式读取 | 将重构为统一入口 |
| `TsFileReaderV4` | V4 格式读取 | 将合并到 TsFileReader |

### 1.2 关键差异

| 特性 | V3 | V4 |
|------|----|----|
| 设备 ID | PlainDeviceId (字符串) | StringArrayDeviceId (数组) |
| 索引结构 | 单根节点 | 每表独立根节点 |
| TableSchema | 无 | 必需 |
| 对齐序列 | 不支持 | 支持（时间/值分离） |

## 2. 技术决策

### 2.1 API 统一策略

**Decision**: 使用策略模式，TsFileWriter 内部根据配置选择 V3 或 V4 写入逻辑

**Rationale**:
- 保持向后兼容
- 最小化 API 变更
- 便于测试和维护

**Alternatives considered**:
- 继承方式：增加复杂度，不推荐
- 完全重写：风险高，不推荐

### 2.2 默认版本切换

**Decision**: V4 作为默认版本，通过构造函数参数可选择 V3

**Rationale**:
- V4 功能更完整
- 与 Java 版本保持一致
- 新用户获得最佳体验

### 2.3 树模型兼容

**Decision**: 自动将树模型路径转换为 StringArrayDeviceId

**Rationale**:
- 遵循 Java 版本的转换规则
- DEFAULT_SEGMENT_NUM_FOR_TABLE_NAME = 3
- 例如: "root.db1.d1" → {"root.db1", "d1"}

## 3. 实现要点

### 3.1 TsFileWriter 重构

1. 添加 `FileVersion` 属性（默认 V4）
2. 内部使用 V4 写入逻辑
3. 保留 `RegisterDevice()` 方法（树模型）
4. 添加 `RegisterTable()` 方法（表模型）
5. 自动生成 LogicalTableSchema

### 3.2 TsFileReader 重构

1. 自动检测文件版本
2. 根据版本选择解析逻辑
3. 统一查询接口
4. V3 文件使用空字符串作为表名

### 3.3 删除文件

- `TsFileWriterV4.cs` → 合并到 `TsFileWriter.cs`
- `TsFileReaderV4.cs` → 合并到 `TsFileReader.cs`
- `TabletV4` 类 → 合并到 `Tablet.cs`

## 4. 风险评估

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| 破坏现有测试 | 中 | 保持 API 签名兼容 |
| Java 互操作问题 | 高 | 增加互操作测试 |
| 性能回归 | 中 | 基准测试对比 |

## 5. V4 查询实现研究（2026-02-06 补充）

### 5.1 当前实现状态

**问题**: `TsFileReader.Query()` 对 V4 文件返回空结果

**位置**: `TsFileReader.cs:79-123`，第 97 行直接 `return result`

### 5.2 V4 Chunk 定位流程

```
TsFileMetadata.tableIndexRoots[tableName]
    → MetadataIndexNode (InternalDevice)
        → MetadataIndexNode (LeafDevice)
            → MetadataIndexNode (InternalMeasurement)
                → MetadataIndexNode (LeafMeasurement)
                    → TimeseriesMetadataV4
                        → ChunkMetadataV4.OffsetOfChunkHeader
```

### 5.3 现有可复用组件

| 组件 | 位置 | 状态 |
|------|------|------|
| MetadataIndexNode.GetChildIndexEntry() | MetadataIndexNode.cs:108-121 | ✅ 已实现 |
| MetadataIndexNode.BinarySearchInChildren() | MetadataIndexNode.cs:123-146 | ✅ 已实现 |
| TimeseriesMetadataV4.Deserialize() | TimeseriesMetadataV4.cs | ✅ 已实现 |
| ChunkMetadataV4.OffsetOfChunkHeader | TimeseriesMetadataV4.cs | ✅ 已实现 |

### 5.4 需要新增的实现

1. **V4 查询入口** - 在 Query() 中添加 V4 分支
2. **索引树遍历** - 递归导航 MetadataIndexNode
3. **Chunk 读取** - 根据 offset 读取 Chunk 数据
4. **Page 解析** - 解压和解码 Page 数据

### 5.5 V4 Chunk 结构

```
ChunkHeader:
├── measurementId (VarInt string)
├── dataSize (VarInt)
├── dataType (byte)
├── compressionType (byte)
├── encodingType (byte)
└── mask (byte, V4 新增)

Pages[]:
├── PageHeader
│   ├── uncompressedSize (VarInt)
│   ├── compressedSize (VarInt)
│   └── statistics (可选)
└── PageData (压缩后)

ChunkSeparator: 0x05 (单页) 或 0x01 (多页)
```

### 5.6 统计信息优化

```csharp
// 时间范围过滤
if (startTime.HasValue && chunk.Statistics.MaxTime < startTime.Value)
    continue; // 跳过整个 Chunk
if (endTime.HasValue && chunk.Statistics.MinTime > endTime.Value)
    continue; // 跳过整个 Chunk
```
