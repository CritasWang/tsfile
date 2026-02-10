using System;
using System.IO;
using Apache.TsFile.IO;
using Apache.TsFile.Enums;
using Xunit;

namespace Apache.TsFile.Tests
{
    /// <summary>
    /// 诊断 Tree Model V4 文件的导航逻辑
    /// </summary>
    public class DiagnoseTreeModelNavigation
    {
        [Fact]
        public void DiagnoseNavigationLogic()
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

                // 获取第一个设备名
                var deviceName = reader.Schemas.Keys.First();
                Console.WriteLine($"=== 测试设备: {deviceName} ===");
                Console.WriteLine();

                // 使用反射访问私有字段和方法
                var readerType = typeof(TsFileReader);
                var tableIndexNodesField = readerType.GetField("_tableIndexNodes",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var tableIndexNodes = tableIndexNodesField?.GetValue(reader) as System.Collections.Generic.Dictionary<string, MetadataIndexNode>;

                if (tableIndexNodes == null || !tableIndexNodes.ContainsKey(deviceName))
                {
                    Console.WriteLine($"错误：找不到设备 {deviceName} 的索引节点");
                    return;
                }

                var rootNode = tableIndexNodes[deviceName];
                Console.WriteLine($"=== Root Node 信息 ===");
                Console.WriteLine($"NodeType: {rootNode.NodeType} (值: {(byte)rootNode.NodeType})");
                Console.WriteLine($"EndOffset: {rootNode.EndOffset}");
                Console.WriteLine($"Entries 数量: {rootNode.Entries?.Count ?? 0}");
                Console.WriteLine();

                if (rootNode.Entries != null && rootNode.Entries.Count > 0)
                {
                    Console.WriteLine($"=== Entries 详情 ===");
                    for (int i = 0; i < rootNode.Entries.Count; i++)
                    {
                        var entry = rootNode.Entries[i];
                        Console.WriteLine($"Entry #{i + 1}:");
                        Console.WriteLine($"  类型: {entry.GetType().Name}");
                        Console.WriteLine($"  Offset: {entry.Offset}");

                        if (entry is DeviceMetadataIndexEntry deviceEntry)
                        {
                            Console.WriteLine($"  DeviceID: {deviceEntry.DeviceID}");
                            Console.WriteLine($"  Segments: {string.Join("/", deviceEntry.DeviceID.GetSegments())}");
                        }
                        else if (entry is MeasurementMetadataIndexEntry measurementEntry)
                        {
                            Console.WriteLine($"  Name: {measurementEntry.Name}");
                        }
                    }
                    Console.WriteLine();
                }

                // 检查 NodeType 是否匹配
                Console.WriteLine($"=== NodeType 检查 ===");
                Console.WriteLine($"rootNode.NodeType == MetadataIndexNodeType.LeafDevice: {rootNode.NodeType == MetadataIndexNodeType.LeafDevice}");
                Console.WriteLine($"rootNode.NodeType == MetadataIndexNodeType.InternalDevice: {rootNode.NodeType == MetadataIndexNodeType.InternalDevice}");
                Console.WriteLine($"rootNode.NodeType == MetadataIndexNodeType.LeafMeasurement: {rootNode.NodeType == MetadataIndexNodeType.LeafMeasurement}");
                Console.WriteLine($"rootNode.NodeType == MetadataIndexNodeType.InternalMeasurement: {rootNode.NodeType == MetadataIndexNodeType.InternalMeasurement}");
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
