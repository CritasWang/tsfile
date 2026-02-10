using System;
using System.IO;
using Apache.TsFile.IO;
using Xunit;

namespace Apache.TsFile.Tests
{
    /// <summary>
    /// 诊断查询后的 Schema 和数据状态
    /// </summary>
    public class DiagnoseTreeModelAfterQuery
    {
        [Fact]
        public void DiagnoseSchemaAfterQuery()
        {
            var testFile = "/tmp/interop-tests/java-comprehensive/boolean_plain_gzip_alternating.tsfile";

            if (!File.Exists(testFile))
            {
                Console.WriteLine($"文件不存在: {testFile}");
                return;
            }

            Console.WriteLine($"分析文件: {testFile}");
            Console.WriteLine();

            try
            {
                using var reader = new TsFileReader(testFile);

                var deviceName = reader.Schemas.Keys.First();
                var schema = reader.Schemas[deviceName];

                Console.WriteLine("=== 查询前 ===");
                Console.WriteLine($"Schema Measurements 数量: {schema.Measurements?.Count ?? 0}");
                Console.WriteLine();

                Console.WriteLine("=== 执行查询 ===");
                var result = reader.Query(deviceName);
                Console.WriteLine("查询完成");
                Console.WriteLine();

                Console.WriteLine("=== 查询后 ===");
                Console.WriteLine($"Schema Measurements 数量: {schema.Measurements?.Count ?? 0}");
                if (schema.Measurements != null && schema.Measurements.Count > 0)
                {
                    Console.WriteLine("Measurements:");
                    foreach (var m in schema.Measurements)
                    {
                        Console.WriteLine($"  - {m.MeasurementName} ({m.DataType})");
                    }
                }
                Console.WriteLine();

                Console.WriteLine("=== 查询结果 ===");
                Console.WriteLine($"Timestamps 数量: {result.Timestamps?.Count ?? 0}");
                Console.WriteLine($"MeasurementData 数量: {result.MeasurementData?.Count ?? 0}");

                if (result.Timestamps != null && result.Timestamps.Count > 0)
                {
                    Console.WriteLine($"前 5 个时间戳:");
                    for (int i = 0; i < Math.Min(5, result.Timestamps.Count); i++)
                    {
                        Console.WriteLine($"  {i + 1}. {result.Timestamps[i]}");
                    }
                }

                if (result.MeasurementData != null && result.MeasurementData.Count > 0)
                {
                    Console.WriteLine("MeasurementData 内容:");
                    foreach (var kvp in result.MeasurementData)
                    {
                        Console.WriteLine($"  {kvp.Key}: {kvp.Value?.Count ?? 0} 个值");
                        if (kvp.Value != null && kvp.Value.Count > 0)
                        {
                            Console.WriteLine($"    前 5 个值: {string.Join(", ", kvp.Value.Take(5))}");
                        }
                    }
                }

                Console.WriteLine();
                Console.WriteLine("诊断完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
                Console.WriteLine($"堆栈: {ex.StackTrace}");
            }
        }
    }
}
