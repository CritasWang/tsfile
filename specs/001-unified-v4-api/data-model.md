# Data Model: 统一 V4 API

**Date**: 2026-02-05
**Branch**: `001-unified-v4-api`

## 核心实体

### 1. TsFileWriter

统一的文件写入器，默认生成 V4 格式。

**属性**:
- `FilePath`: string - 文件路径
- `FileVersion`: byte - 文件版本（默认 4）
- `Schemas`: Dictionary<string, TableSchema> - 已注册的表结构

**方法**:
- `RegisterTimeseries(deviceName, measurements)` - 注册树模型时间序列
- `RegisterTable(tableSchema)` - 注册表模型结构
- `Write(tablet)` - 写入数据
- `WriteRecord(record)` - 写入单条记录
- `Close()` - 关闭文件

### 2. TsFileReader

统一的文件读取器，自动识别 V3/V4 格式。

**属性**:
- `FilePath`: string - 文件路径
- `FileVersion`: byte - 检测到的文件版本
- `Schemas`: Dictionary<string, TableSchema> - 表结构

**方法**:
- `Query(deviceName, measurements, timeRange)` - 树视图查询
- `QueryTable(tableName, columns, filters)` - 表视图查询
- `GetTableNames()` - 获取所有表名
- `Close()` - 关闭文件

### 3. IDeviceID

设备标识接口。

**实现**:
- `StringArrayDeviceID` - V4 格式（数组形式）

**转换规则**:
```
"root.db1.d1" → {"root.db1", "d1"}
"root.db1.d1.d2" → {"root.db1", "d1", "d2"}
```

### 4. TableSchema

表结构定义。

**属性**:
- `TableName`: string - 表名
- `ColumnSchemas`: List<ColumnSchema> - 列定义
- `Measurements`: List<MeasurementSchema> - 测点定义（兼容）

### 5. Tablet

批量数据容器。

**属性**:
- `DeviceName`: string - 设备名（树模型）
- `TableName`: string - 表名（表模型）
- `Timestamps`: long[] - 时间戳数组
- `Values`: object[][] - 值数组
- `BitMap`: BitMap - 空值标记

## 状态转换

```
[创建] → [注册Schema] → [写入数据] → [关闭]
                ↓
         [自动生成索引]
```

## 验证规则

1. 写入前必须注册 Schema
2. 数据类型必须与 Schema 匹配
3. 时间戳必须递增
4. V4 格式必须包含 TableSchema

## V4 查询相关实体（2026-02-06 补充）

### 6. MetadataIndexNode

索引树节点，用于定位 Chunk。

**属性**:
- `NodeType`: MetadataIndexNodeType - 节点类型
- `Entries`: List<IMetadataIndexEntry> - 子节点入口
- `EndOffset`: long - 结束偏移

**节点类型**:
- `InternalDevice` - 设备内部节点
- `LeafDevice` - 设备叶子节点
- `InternalMeasurement` - 测点内部节点
- `LeafMeasurement` - 测点叶子节点

### 7. TimeseriesMetadataV4

时间序列元数据。

**属性**:
- `MeasurementId`: string - 测点 ID
- `DataType`: TsDataType - 数据类型
- `ChunkMetadataList`: List<ChunkMetadataV4> - Chunk 元数据列表
- `HasMultiplePages`: bool - 是否有多页

### 8. ChunkMetadataV4

Chunk 元数据。

**属性**:
- `OffsetOfChunkHeader`: long - Chunk 头偏移
- `Statistics`: StatisticsV4 - 统计信息

### 9. StatisticsV4

统计信息，用于查询优化。

**属性**:
- `MinTime`: long - 最小时间
- `MaxTime`: long - 最大时间
- `MinValue`: object - 最小值
- `MaxValue`: object - 最大值
- `Count`: long - 数据点数量
