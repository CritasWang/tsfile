# C# TSFile 实现路线图

**最后更新**: 2026-02-12

## 已完成

### ✅ Phase 1: 核心功能
- [x] 13/13 数据类型（与 Java 100% 一致）
- [x] 5/6 压缩算法（LZMA2 仅读取）
- [x] 14/15 编码算法（仅缺 CAMEL）
- [x] V3 格式读写
- [x] V4 树模型读写（对齐 + 非对齐）
- [x] V4 表模型读写（TAG/FIELD 列）
- [x] 跨平台 Snappy（IronSnappy 纯 C#）

### ✅ Phase 2: Java 互操作
- [x] 360 编码/压缩组合文件验证
- [x] 90 表模型文件验证
- [x] Java V3 文件读取（4/4）
- [x] 综合互操作测试（表模型 3 表 + 树模型 5 设备）
- [x] C# → Java 反向验证（3 文件）
- [x] `run-java-interop-tests.sh` 自动化脚本

### ✅ Phase 3: 编码器/解码器修复
- [x] PlainDecoder: ZigZag VarInt 格式
- [x] ZigZagDecoder: bytesCacheSize 前缀
- [x] GorillaDecoder (V2): 无长度前缀，结束标记
- [x] GorillaV1Decoder: 小端首值，预读取
- [x] DictionaryDecoder: ZigZag VarInt + IntRleDecoder
- [x] RLE 位打包: 大端 MSB-first
- [x] TS_2DIFF/RLE float/double: FloatWrapper 支持
- [x] Ts2DiffDecoder: 整数溢出修复
- [x] 对应编码器全部更新以匹配 Java 二进制格式

### ✅ Phase 4: 读取器修复
- [x] StatisticsV4: String 类型 4 值（first+last+min+max）
- [x] ChunkMetadataV4: 大端 Int64 偏移
- [x] 树模型页面格式: timeBuffer + valueBuffer
- [x] LZ4 解压: 无 4 字节大小前缀
- [x] ZSTD 解压: 按压缩类型判断（非大小比较）
- [x] ReadTimeseriesMetadataFromNode: 范围内读取所有 TSMetadata
- [x] V4 树模型设备路径前缀匹配
- [x] 解码器产生多余值的截断处理

### ✅ Phase 5: CI/CD
- [x] GitHub Actions 工作流（csharp-ci.yml）
- [x] 跨平台构建测试（Ubuntu, Windows, macOS）
- [x] Java 互操作测试集成
- [x] 代码覆盖率
- [x] 性能基准测试
- [x] 安全扫描

---

## 待完成

### 📝 Phase 6: 剩余功能
- [ ] CAMEL 编码（Double 专用，低优先级）
- [ ] LZMA2 压缩写入
- [ ] Gorilla Int64 编码器修复

### 📝 Phase 7: 高级查询
- [ ] 时间范围过滤
- [ ] 值过滤表达式
- [ ] 聚合查询（min, max, count, avg）
- [ ] 统计信息读取

### 📝 Phase 8: 性能优化
- [ ] Async/await I/O
- [ ] ArrayPool / Span<T> 内存优化
- [ ] 大文件流式读取
- [ ] 并行读写
- [ ] SIMD 加速

### 📝 Phase 9: 生态集成
- [ ] NuGet 包发布
- [ ] 云存储适配器（Azure, AWS, GCP）
- [ ] 文件检查工具
- [ ] Schema 迁移工具

---

## 当前指标

| 指标 | 值 |
|------|-----|
| 测试总数 | 196 |
| 通过率 | 99.5%（195/196） |
| 编码覆盖 | 93%（14/15） |
| 压缩覆盖 | 83%（5/6） |
| 数据类型覆盖 | 100%（13/13） |
| 互操作文件验证 | 450+ |
| 目标框架 | .NET 9/10 |
