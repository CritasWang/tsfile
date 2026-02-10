using System;
using System.IO;
using Apache.TsFile.IO;
using Xunit;

namespace Apache.TsFile.Tests
{
    /// <summary>
    /// 诊断 Tree Model V4 文件的查询流程
    /// </summary>
    public class DiagnoseTreeModelQuery
    {
        [Fact]
        public void DiagnoseTreeModelV4Query()
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

                Console.WriteLine("=== 文件信息 ===");
                Console.WriteLine($"文件版本: {reader.FileVersion}");
                Console.WriteLine($"Schemas 数量: {reader.Schemas?.Count ?? 0}");
                Console.WriteLine();

                if (reader.Schemas == null || reader.Schemas.Count == 0)
                {
                    Console.WriteLine("错误：Schemas 为空");
                    return;
                }

                Console.WriteLine("=== Schemas ===");
                foreach (var kvp in reader.Schemas)
                {
                    Console.WriteLine($"Key: {kvp.Key}");
                    Console.WriteLine($"  Measurements: {kvp.Value.Measurements?.Count ?? 0}");
                    if (kvp.Value.Measurements != null)
                    {
                        foreach (var m in kvp.Value.Measurements)
                        {
                            Console.WriteLine($"    - {m.MeasurementName} ({m.DataType})");
                        }
                    }
                }
                Console.WriteLine();

                // 尝试查询
                Console.WriteLine("=== 查询测试 ===");
                var deviceName = reader.Schemas.Keys.First();
                Console.WriteLine($"查询设备: {deviceName}");

                var result = reader.Query(deviceName);
                Console.WriteLine($"查询结果: {result}");
                Console.WriteLine($"  Timestamps 数量: {result.Timestamps?.Count ?? 0}");
                Console.WriteLine($"  MeasurementData 数量: {result.MeasurementData?.Count ?? 0}");
                Console.WriteLine();

                if (result.Timestamps == null || result.Timestamps.Count == 0)
                {
                    Console.WriteLine("警告：查询返回空结果");
                }
                else
                {
                    Console.WriteLine($"成功读取 {result.Timestamps.Count} 条记录");
                    foreach (var kvp in result.MeasurementData)
                    {
                        Console.WriteLine($"  {kvp.Key}: {kvp.Value?.Count ?? 0} 个值");
                    }
                }

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
