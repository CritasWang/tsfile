# C# TsFile 未实现功能 - 实现计划

**日期**: 2026-02-12  
**基于**: `FEATURE_COMPARISON.md` 差异分析

---

## 差异总览

从 `FEATURE_COMPARISON.md` 中识别出以下 C# 未完全实现但可以实现的功能：

| # | 功能 | 当前状态 | 可行性 | 优先级 | 预估工时 |
|---|------|----------|--------|--------|----------|
| 1 | CAMEL 编码 | ✅ 已实现 | ✅ 高 | 中 | ✅ 已完成 |
| 2 | LZMA2 压缩写入 | ⚠️ 延迟 | ⚠️ 中 | 低 | 无纯 C# XZ 写入库 |
| 3 | Gorilla Int64 编码器修复 | ✅ 已修复 | ✅ 高 | 高 | ✅ 已完成 |
| 4 | 查询过滤器（时间+测量+值） | ✅ 已实现 | ✅ 高 | 高 | ✅ 已完成 |
| 5 | 加密支持 | ❌ 未实现 | ⚠️ 中 | 低 | 3-5 天 |
| 6 | 统计信息读取 | ✅ 已暴露 | ✅ 高 | 中 | ✅ 已完成 |
| 7 | 聚合查询 | ✅ 已实现 | ✅ 高 | 中 | ✅ 已完成 |

---

## 1. CAMEL 编码（优先级：中）

### 现状
- C# 编码器和解码器均未实现
- Java 参考实现：`CamelEncoder.java`（298 行）+ `CamelDecoder.java`（270 行）= 568 行

### 技术分析
CAMEL 是一种专门针对 Double 类型的混合编码：
- **核心思想**：将 double 拆分为整数部分 + 小数部分分别编码
- **整数部分**：使用 VarLong 差分编码（类似 TS_2DIFF）
- **小数部分**：使用位级编码 + XOR 压缩
- **回退机制**：当值不适合 CAMEL 编码时，回退到内嵌的 Gorilla 编码
- **依赖**：`BitOutputStream`/`BitInputStream`（C# 已有 `BitWriter`/`BitReader`）

### 实现步骤

1. **创建 `CamelEncoder.cs`**（~200 行）
   - 移植 `CamelEncoder.java` 的逻辑
   - 内嵌 GorillaEncoder 作为回退
   - 实现 `addValue(double)` → 判断 CAMEL/Gorilla 路径
   - 实现 `compressIntegerValue()` 和 `compressDecimalValue()`
   - 使用现有 `BitWriter` 替代 Java 的 `BitOutputStream`

2. **创建 `CamelDecoder.cs`**（~200 行）
   - 移植 `CamelDecoder.java` 的逻辑
   - 内嵌 GorillaDecoder 作为回退
   - 实现 `next()` → 读取 sign + type bit → 分发到 CAMEL/Gorilla 路径
   - 实现 `readLong()`（VarLong 差分）和 `readDecimal()`（位级解码）

3. **注册到工厂**
   - `EncoderFactory.cs`: 添加 `TsEncoding.Camel` → `CamelEncoder`
   - `DecoderFactory.cs`: 添加 `TsEncoding.Camel` → `CamelDecoder`

4. **测试**
   - 单元测试：编码/解码往返
   - 互操作测试：读取 Java 生成的 CAMEL 编码文件
   - 边界情况：0.0, NaN, Infinity, 非常大/小的值

### 风险
- 位级操作需要精确匹配 Java 的 `BitOutputStream`/`BitInputStream` 行为
- VarLong 编码格式需要与 Java 完全一致

---

## 2. LZMA2 压缩写入（优先级：低）

### 现状
- 解压缩已通过 SharpCompress 实现
- 压缩写入未实现（SharpCompress 不支持 LZMA2 压缩）

### 技术分析
- **SharpCompress**：仅支持 LZMA2 解压缩，不支持压缩
- **替代方案**：
  - `LZMA-SDK`（纯 C#）：支持 LZMA 压缩，但 LZMA2 需要额外封装
  - `FastLZMA2Net`：Windows-only（不跨平台）
  - `SevenZipSharp`：依赖 7z.dll（不跨平台）

### 实现步骤

1. **评估 LZMA-SDK**
   - 检查是否可以用 LZMA-SDK 实现 LZMA2 压缩
   - LZMA2 = LZMA + 分块 + 未压缩块回退

2. **实现 `Lzma2Compressor.Compress()`**
   - 使用 LZMA-SDK 的 `LzmaEncoder`
   - 添加 LZMA2 分块封装
   - 确保输出格式与 Java 的 LZMA2 兼容

3. **测试**
   - 压缩/解压缩往返
   - C# 压缩 → Java 解压缩互操作

### 风险
- **跨平台兼容性**：纯 C# LZMA2 压缩库可能不存在
- **格式兼容性**：LZMA2 的分块格式需要精确匹配 Java 实现
- **建议**：如果没有可靠的跨平台方案，保持现状（仅解压缩），推荐用户使用 ZSTD 替代

---

## 3. Gorilla Int64 编码器修复（优先级：高）

### 现状
- 解码器正常工作（360/360 互操作文件通过）
- 编码器存在已知 bug（测试跳过）

### 技术分析
- Gorilla Int64 编码器的问题可能在于：
  - 位宽常量（`LEADING_ZEROS_BITS`=6, `SIGNIFICANT_BITS`=6 for Int64）
  - 结束标记（`Long.MIN_VALUE`）
  - 首值编码（64 位）

### 实现步骤

1. **诊断**
   - 启用跳过的测试，观察具体失败
   - 对比 Java GorillaEncoder 的 Int64 编码路径
   - 检查 `BitWriter` 的 64 位写入是否正确

2. **修复**
   - 修正编码器中的位宽/位序问题
   - 确保结束标记正确写入

3. **测试**
   - 启用并通过 `GorillaEncoder_Int64Timestamp_SuccessfulRoundTrip`
   - 添加更多 Int64 边界值测试
   - C# 编码 → Java 解码互操作验证

### 风险
- 低风险，解码器已经正确实现，编码器只需对齐

---

## 4. 查询过滤器（优先级：高）

### 现状
- 当前 `Query()` 返回设备/表的全部数据
- 无时间范围过滤、值过滤、测量选择

### 技术分析
Java 的查询引擎非常复杂（包含 Filter 表达式树、SeriesReader、BatchData 等），完整移植不现实。但可以实现实用的子集：

### 实现步骤

#### Phase 4a: 时间范围过滤（1-2 天）

1. **扩展 `Query()` 签名**
   ```csharp
   // 现有
   QueryResult Query(string deviceOrTable);
   
   // 新增
   QueryResult Query(string deviceOrTable, long? startTime = null, long? endTime = null);
   ```

2. **利用 ChunkMetadata 统计信息跳过不相关的 Chunk**
   - ChunkMetadata 已包含 `startTime`/`endTime` 统计
   - 在 `ReadChunkV4` / `ReadChunk` 中添加时间范围检查
   - 跳过 `endTime < queryStartTime` 或 `startTime > queryEndTime` 的 Chunk

3. **在页面级别过滤**
   - 解码后过滤不在范围内的行
   - 或在 Page 级别利用 PageHeader 统计信息跳过

#### Phase 4b: 测量选择（1 天）

1. **扩展 `Query()` 签名**
   ```csharp
   QueryResult Query(string deviceOrTable, string[]? measurements = null,
                     long? startTime = null, long? endTime = null);
   ```

2. **在 TimeseriesMetadata 级别过滤**
   - 仅读取指定测量的 TimeseriesMetadata
   - 跳过不需要的 Chunk

#### Phase 4c: 值过滤（2 天，可选）

1. **定义简单过滤器接口**
   ```csharp
   public interface IFilter
   {
       bool Evaluate(object? value);
   }
   
   public class ComparisonFilter : IFilter { ... }  // >, <, >=, <=, ==, !=
   public class AndFilter : IFilter { ... }
   public class OrFilter : IFilter { ... }
   ```

2. **在解码后应用过滤**
   - 逐行评估过滤条件
   - 构建过滤后的 QueryResult

### 风险
- 时间范围过滤相对简单，风险低
- 值过滤需要设计过滤器 API，复杂度较高

---

## 5. 加密支持（优先级：低）

### 现状
- C# 完全未实现加密
- Java 实现：~672 行（8 个文件），使用 HMAC-SHA256 + AES

### 技术分析
- Java 加密是可选的，默认 `UNENCRYPTED`
- 加密应用在 Chunk 数据级别
- .NET 有完整的加密 API（`System.Security.Cryptography`）

### 实现步骤

1. **定义加密接口**（~50 行）
   ```csharp
   public interface IEncryptor
   {
       byte[] Encrypt(byte[] data);
       byte[] Decrypt(byte[] data);
       string EncryptionType { get; }
   }
   ```

2. **实现 `NoEncryptor`**（~20 行）
   - 直接返回原始数据

3. **实现 `AesEncryptor`**（~100 行）
   - 使用 `System.Security.Cryptography.Aes`
   - 密钥派生使用 PBKDF2（与 Java 的 HMAC-SHA256 兼容）

4. **集成到 Reader/Writer**
   - Writer: 在压缩后、写入前加密
   - Reader: 在读取后、解压缩前解密
   - 在文件元数据中记录加密类型

5. **测试**
   - 加密/解密往返
   - Java 加密文件 → C# 解密互操作

### 风险
- 密钥派生算法需要与 Java 完全一致
- 加密集成需要修改 Reader/Writer 的核心路径
- 建议在其他功能完成后再实现

---

## 6. 统计信息读取（优先级：中）

### 现状
- `StatisticsV4` 已实现反序列化（用于元数据导航）
- 但统计信息未暴露给用户 API

### 实现步骤

1. **定义统计信息类**（~50 行）
   ```csharp
   public class ChunkStatistics
   {
       public long Count { get; }
       public long StartTime { get; }
       public long EndTime { get; }
       public object? MinValue { get; }
       public object? MaxValue { get; }
       public object? FirstValue { get; }
       public object? LastValue { get; }
   }
   ```

2. **扩展 QueryResult**
   ```csharp
   public class QueryResult
   {
       // 现有字段...
       public Dictionary<string, ChunkStatistics> Statistics { get; }
   }
   ```

3. **在读取时填充统计信息**
   - 从已解析的 `StatisticsV4` / `Statistics` 中提取
   - 聚合多个 Chunk 的统计信息

4. **测试**
   - 验证统计信息与实际数据一致
   - 验证与 Java 生成的统计信息一致

---

## 推荐实施顺序

```
Phase 1（立即）:
  3. Gorilla Int64 编码器修复        [0.5-1 天]

Phase 2（短期）:
  4a. 时间范围过滤                   [1-2 天]
  4b. 测量选择                       [1 天]
  6. 统计信息读取                    [1-2 天]

Phase 3（中期）:
  1. CAMEL 编码                      [2-3 天]
  4c. 值过滤（可选）                 [2 天]

Phase 4（长期/可选）:
  5. 加密支持                        [3-5 天]
  2. LZMA2 压缩写入                  [1-2 天，取决于库可用性]
```

**总预估工时**: 12-19 天

---

## 不建议实现的功能

| 功能 | 原因 |
|------|------|
| PageReader / ChunkReader（独立类） | 当前内嵌在 TsFileReader 中，功能已完整，拆分仅是架构重构 |
| Builder 模式 | 当前 API 已足够简洁，Builder 模式增加复杂度但无功能增益 |
| 完整查询引擎 | Java 的查询引擎极其复杂，C# 实现实用子集即可 |
| Spark/Flink 集成 | 需要额外的生态系统支持，超出文件格式库范围 |
