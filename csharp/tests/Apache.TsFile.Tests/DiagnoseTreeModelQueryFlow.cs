using System;
using System.IO;
using System.Reflection;
using Apache.TsFile.IO;
using Xunit;

namespace Apache.TsFile.Tests
{
    /// <summary>
    /// 诊断 Tree Model V4 完整查询流程
    /// </summary>
    public class DiagnoseTreeModelQueryFlow
    {
        [Fact]
        public void DiagnoseCompleteQueryFlow()
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

                var deviceName = reader.Schemas.Keys.First();
                Console.WriteLine($"=== 查询设备: {deviceName} ===");
                Console.WriteLine();

                // 使用反射访问私有方法
                var readerType = typeof(TsFileReader);

                // 获取 _tableIndexNodes
                var tableIndexNodesField = readerType.GetField("_tableIndexNodes",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var tableIndexNodes = tableIndexNodesField?.GetValue(reader) as System.Collections.Generic.Dictionary<string, MetadataIndexNode>;

                if (tableIndexNodes == null || !tableIndexNodes.ContainsKey(deviceName))
                {
                    Console.WriteLine($"错误：找不到设备 {deviceName} 的索引节点");
                    return;
                }

                var rootNode = tableIndexNodes[deviceName];
                Console.WriteLine($"=== Root Node ===");
                Console.WriteLine($"NodeType: {rootNode.NodeType}");
                Console.WriteLine($"Entries 数量: {rootNode.Entries?.Count ?? 0}");
                Console.WriteLine();

                // 调用 NavigateToTimeseriesMetadata
                var navigateMethod = readerType.GetMethod("NavigateToTimeseriesMetadata",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (navigateMethod == null)
                {
                    Console.WriteLine("错误：找不到 NavigateToTimeseriesMetadata 方法");
                    return;
                }

                Console.WriteLine("=== 调用 NavigateToTimeseriesMetadata ===");
                var timeseriesMetadataList = navigateMethod.Invoke(reader, new object[] { rootNode, deviceName, null });

                if (timeseriesMetadataList is System.Collections.IList list)
                {
                    Console.WriteLine($"返回的 TimeseriesMetadata 数量: {list.Count}");

                    if (list.Count > 0)
                    {
                        Console.WriteLine("成功！找到了 timeseries metadata");
                        for (int i = 0; i < list.Count; i++)
                        {
                            var tsMetadata = list[i];
                            Console.WriteLine($"  #{i + 1}: {tsMetadata}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("警告：NavigateToTimeseriesMetadata 返回空列表");
                        Console.WriteLine();
                        Console.WriteLine("=== 手动追踪问题 ===");

                        // 检查 root node 的 entries
                        if (rootNode.Entries != null && rootNode.Entries.Count > 0)
                        {
                            Console.WriteLine($"Root node 有 {rootNode.Entries.Count} 个 entries");

                            foreach (var entry in rootNode.Entries)
                            {
                                Console.WriteLine($"Entry 类型: {entry.GetType().Name}");
                                Console.WriteLine($"Entry.IsDeviceLevel: {entry.IsDeviceLevel}");

                                if (entry is DeviceMetadataIndexEntry deviceEntry)
                                {
                                    Console.WriteLine($"  DeviceID: {deviceEntry.DeviceID}");
                                    Console.WriteLine($"  Offset: {deviceEntry.Offset}");
                                }
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("错误：NavigateToTimeseriesMetadata 返回类型不正确");
                }

                Console.WriteLine();
                Console.WriteLine("诊断完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
                Console.WriteLine($"堆栈: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"内部异常: {ex.InnerException.Message}");
                    Console.WriteLine($"内部堆栈: {ex.InnerException.StackTrace}");
                }
            }
        }
    }
}
