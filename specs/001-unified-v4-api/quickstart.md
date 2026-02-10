# Quick Start: 统一 V4 API

## 安装

```bash
dotnet add package Apache.TsFile
```

## 写入数据

### 树模型（传统方式）

```csharp
using Apache.TsFile.IO;
using Apache.TsFile.Schema;
using Apache.TsFile.Enums;

// 创建写入器（默认 V4 格式）
using var writer = new TsFileWriter("data.tsfile");

// 注册时间序列
var measurements = new List<MeasurementSchema>
{
    new("temperature", TsDataType.Float, TsEncoding.Gorilla),
    new("humidity", TsDataType.Float, TsEncoding.Gorilla)
};
writer.RegisterTimeseries("root.db1.device1", measurements);

// 写入数据
var tablet = new Tablet("root.db1.device1", measurements, 100);
tablet.AddRow(1704067200000, 25.5f, 60.0f);
tablet.AddRow(1704067201000, 25.6f, 59.8f);
writer.Write(tablet);

writer.Close();
```

### 表模型（V4 新方式）

```csharp
// 创建表结构
var schema = new TableSchema("sensor_data");
schema.ColumnSchemas = new List<ColumnSchema>
{
    new ColumnSchema("region", ColumnCategory.Tag, TsDataType.String, TsEncoding.Plain, CompressionType.Uncompressed),
    new ColumnSchema("device", ColumnCategory.Tag, TsDataType.String, TsEncoding.Plain, CompressionType.Uncompressed),
    new ColumnSchema("temperature", ColumnCategory.Field, TsDataType.Float, TsEncoding.Gorilla, CompressionType.Lz4)
};

// 为 Field 列添加 MeasurementSchema
foreach (var col in schema.ColumnSchemas.Where(c => c.Category == ColumnCategory.Field))
{
    schema.AddMeasurement(new MeasurementSchema(col.Name, col.DataType, col.Encoding, col.Compression));
}

using var writer = new TsFileWriter("data.tsfile");
writer.RegisterTable(schema);

// 写入表数据
var tablet = new Tablet(schema, 100);
tablet.AddRow(1704067200000, 25.5f);
writer.WriteTable(tablet);

writer.Close();
```

## 读取数据

```csharp
using var reader = new TsFileReader("data.tsfile");

// 自动识别 V3/V4 格式
Console.WriteLine($"File version: {reader.FileVersion}");

// 获取所有表/设备
foreach (var tableName in reader.Schemas.Keys)
{
    Console.WriteLine($"Table: {tableName}");
}

// 查询数据
var result = reader.Query("root.db1.device1");
Console.WriteLine($"Rows: {result.Count}");

// 访问数据
for (int i = 0; i < result.Count; i++)
{
    var timestamp = result.Timestamps[i];
    var temp = result.GetColumn("temperature")[i];
    Console.WriteLine($"Time: {timestamp}, Temp: {temp}");
}
```

## 版本兼容

```csharp
// 强制使用 V3 格式（仅用于兼容旧系统）
using var writer = new TsFileWriter("legacy.tsfile", 3);

// 读取器自动兼容所有版本
using var reader = new TsFileReader("any_version.tsfile");
Console.WriteLine($"Detected version: {reader.FileVersion}");
```
