# API Contracts: 统一 V4 API

## TsFileWriter API

### 构造函数

```csharp
// 默认 V4 格式
TsFileWriter(string filePath)

// 指定版本
TsFileWriter(string filePath, byte fileVersion = 4)
```

### 树模型 API

```csharp
// 注册时间序列
void RegisterTimeseries(string deviceName, List<MeasurementSchema> measurements)

// 写入 Tablet
void Write(Tablet tablet)

// 写入单条记录
void WriteRecord(string deviceName, long timestamp, params object[] values)
```

### 表模型 API

```csharp
// 注册表结构
void RegisterTable(TableSchema schema)

// 写入表数据
void WriteTable(Tablet tablet)
```

### 生命周期

```csharp
void Flush()
void Close()
void Dispose()
```

## TsFileReader API

### 构造函数

```csharp
TsFileReader(string filePath)
```

### 属性

```csharp
byte FileVersion { get; }
IReadOnlyDictionary<string, TableSchema> Schemas { get; }
```

### 树视图查询

```csharp
QueryResult Query(
    string deviceName,
    string[]? measurements = null,
    long? startTime = null,
    long? endTime = null)
```

### 表视图查询

```csharp
TableQueryResult QueryTable(
    string tableName,
    string[]? columns = null,
    string? idFilter = null,
    string? measurementFilter = null,
    long? startTime = null,
    long? endTime = null)
```

### 元数据

```csharp
IEnumerable<string> GetTableNames()
IEnumerable<string> GetDeviceNames()
TableSchema? GetTableSchema(string tableName)
```

## 数据结构

### Tablet

```csharp
class Tablet
{
    string DeviceName { get; }
    string? TableName { get; }
    long[] Timestamps { get; }
    int RowCount { get; }

    void AddRow(long timestamp, params object[] values)
    void Reset()
}
```

### QueryResult

```csharp
class QueryResult : IEnumerable<RowRecord>
{
    string DeviceName { get; }
    TableSchema Schema { get; }
    bool HasNext();
    RowRecord Next();
}
```

### TableQueryResult

```csharp
class TableQueryResult : IEnumerable<TsBlock>
{
    string TableName { get; }
    bool HasNext();
    TsBlock Next();
}
```
