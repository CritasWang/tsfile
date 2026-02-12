# C# TsFile 与 Java 功能对比

本文档详细对比 C# TsFile 库与 Java 参考实现的功能差异。

**最后更新**: 2026-02-12

## 总览

| 组件 | Java | C# | 状态 |
|------|------|-----|------|
| 数据类型 | 13 种 | 13 种 | ✅ 完全一致 |
| 编码算法 | 15 种 | 15 种 | ✅ 完全一致 |
| 压缩算法 | 6 种 | 5+1 种 | ✅ LZMA2 仅读取 |
| V4 表模型 | ✅ 完整 | ✅ 完整 | 读写均支持 |
| V4 树模型 | ✅ 完整 | ✅ 完整 | 读写均支持 |
| V3 格式 | ✅ 完整 | ✅ 读取 | 可读 Java V3 文件 |
| 查询引擎 | ✅ 完整 | ✅ 基础 | 按设备/表名查询，支持时间范围+测量选择过滤 |
| 加密 | ✅ 完整 | ❌ 缺失 | 未实现 |

## 数据类型（13/13 = 100%）

| 数据类型 | Java | C# | 说明 |
|----------|------|-----|------|
| Boolean | ✅ | ✅ | 位压缩存储 |
| Int32 | ✅ | ✅ | 32 位有符号整数 |
| Int64 | ✅ | ✅ | 64 位有符号整数 |
| Float | ✅ | ✅ | IEEE 754 单精度 |
| Double | ✅ | ✅ | IEEE 754 双精度 |
| Text | ✅ | ✅ | UTF-8 字符串，BinaryStatistics（first+last） |
| String | ✅ | ✅ | UTF-8 字符串，StringStatistics（first+last+min+max） |
| Blob | ✅ | ✅ | 二进制数据 |
| Timestamp | ✅ | ✅ | 64 位毫秒时间戳 |
| Date | ✅ | ✅ | epoch day 表示 |
| Vector | ✅ | ✅ | 对齐时间序列的时间块类型 |
| Unknown | ✅ | ✅ | 动态类型 |
| Object | ✅ | ✅ | 对象类型 |

## 编码算法（14/15 = 93%）

| 编码 | Java | C# 编码器 | C# 解码器 | 适用类型 | 说明 |
|------|------|-----------|-----------|----------|------|
| Plain | ✅ | ✅ | ✅ | 全部 | 默认编码 |
| RLE | ✅ | ✅ | ✅ | Boolean, Int32, Int64 | 游程编码+位打包 |
| ZigZag | ✅ | ✅ | ✅ | Int32, Int64 | ZigZag VarInt |
| Gorilla | ✅ | ✅ | ✅ | Float, Double, Int32 | XOR 压缩（V2） |
| GorillaV1 | ✅ | ✅ | ✅ | Float, Double | 旧版 Gorilla |
| Dictionary | ✅ | ✅ | ✅ | Text, String | 字典编码 |
| TS_2DIFF | ✅ | ✅ | ✅ | Int32, Int64, Float, Double | 二阶差分 |
| Diff | ✅ | ✅ | ✅ | Int32, Int64 | 一阶差分 |
| Bitmap | ✅ | ✅ | ✅ | Boolean | 位图编码 |
| Regular | ✅ | ✅ | ✅ | Int64 | 规律间隔 |
| CHIMP | ✅ | ✅ | ✅ | Float, Double, Int32, Int64 | 高级 XOR 压缩 |
| SPRINTZ | ✅ | ✅ | ✅ | Int32, Int64, Float, Double | 传感器优化 |
| RLBE | ✅ | ✅ | ✅ | Int32, Int64 | 游程字节编码 |
| Freq | ⚠️ 已弃用 | ⚠️ 已弃用 | ⚠️ 已弃用 | - | 映射到 Plain |
| CAMEL | ✅ | ✅ | ✅ | Double | 整数+小数拆分 + Gorilla 回退 |

## 压缩算法（5/6）

| 压缩 | Java | C# 压缩 | C# 解压 | 库 | 说明 |
|------|------|---------|---------|-----|------|
| Uncompressed | ✅ | ✅ | ✅ | - | 无压缩 |
| Snappy | ✅ | ✅ | ✅ | IronSnappy | 纯 C#，跨平台 |
| Gzip | ✅ | ✅ | ✅ | System.IO.Compression | 高压缩比 |
| LZ4 | ✅ | ✅ | ✅ | K4os.Compression.LZ4 | 最快 |
| Zstd | ✅ | ✅ | ✅ | ZstdSharp.Port | 最佳压缩比 |
| LZMA2 | ✅ | ❌ | ✅ | SharpCompress | 仅解压缩 |

## V4 表模型

| 功能 | Java | C# | 说明 |
|------|------|-----|------|
| TableSchema 定义 | ✅ | ✅ | 表名 + 列定义 |
| ColumnSchema | ✅ | ✅ | 列名 + 数据类型 |
| ColumnCategory | ✅ | ✅ | TAG / FIELD / TIMESTAMP |
| 多设备写入 | ✅ | ✅ | 按 TAG 值自动分设备 |
| 表模型查询 | ✅ | ✅ | 按表名查询所有设备 |
| StringArrayDeviceID | ✅ | ✅ | V4 设备 ID 序列化 |
| MetadataIndexNode | ✅ | ✅ | 索引树导航 |
| TimeseriesMetadataV4 | ✅ | ✅ | 含嵌入式 ChunkMetadata |
| 过滤查询 | ✅ | ✅ | 时间范围 + 测量选择（Chunk 级别跳过） |

## V4 树模型

| 功能 | Java | C# | 说明 |
|------|------|-----|------|
| 非对齐时间序列 | ✅ | ✅ | 每测量独立时间块 |
| 对齐时间序列 | ✅ | ✅ | 共享时间块 + 独立值块 |
| 设备注册 | ✅ | ✅ | registerTimeseries / registerAlignedTimeseries |
| 按设备路径查询 | ✅ | ✅ | 如 `root.db1.d1` |
| 设备路径前缀匹配 | ✅ | ✅ | 自动解析表名（如 `root.db1`） |
| 设备级过滤 | ✅ | ✅ | NavigateNode 中按 deviceFilter 过滤 |

## 读写组件

| 组件 | Java | C# | 说明 |
|------|------|-----|------|
| TsFileWriter（统一） | ✅ | ✅ | V3/V4 统一写入器 |
| TsFileReader（统一） | ✅ | ✅ | V3/V4 统一读取器，自动检测版本 |
| Tablet | ✅ | ✅ | 批量数据容器 |
| QueryResult | ✅ | ✅ | 查询结果（Timestamps + MeasurementData） |
| Builder 模式 | ✅ | ❌ | 未实现 |
| PageReader | ✅ | ❌ | 内嵌在 TsFileReader 中 |
| ChunkReader | ✅ | ❌ | 内嵌在 TsFileReader 中 |

## 互操作测试覆盖

### 编码 × 压缩 × 数据类型（360 文件）

| 数据类型 | 编码 | 压缩 |
|----------|------|------|
| INT32 | PLAIN, RLE, TS_2DIFF, GORILLA, ZIGZAG | UNCOMPRESSED, GZIP, LZ4, SNAPPY, ZSTD |
| INT64 | PLAIN, RLE, TS_2DIFF, GORILLA, ZIGZAG | 同上 |
| FLOAT | PLAIN, GORILLA, GORILLA_V1, TS_2DIFF | 同上 |
| DOUBLE | PLAIN, GORILLA, GORILLA_V1, TS_2DIFF | 同上 |
| BOOLEAN | PLAIN, RLE | 同上 |
| TEXT | PLAIN, DICTIONARY | 同上 |

### 表模型（90 文件）

各编码/压缩组合的表模型文件。

### 综合互操作

- 3 个表 × 10 FIELD 列 × 8 设备 × 20 行
- 5 个树模型设备（对齐 + 非对齐）× 多种数据类型
- C# → Java 反向验证（3 文件）

### 其他测试场景

- 混合数据类型
- 混合压缩类型
- 多种数据模式（递增、重复、交替）
- 不同行数（1, 10, 100, 1000）
- 大字符串值
- 多设备（多 TAG 组合）
