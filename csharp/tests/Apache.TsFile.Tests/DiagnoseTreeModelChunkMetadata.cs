using System;
using System.IO;
using System.Reflection;
using Apache.TsFile.IO;
using Xunit;

namespace Apache.TsFile.Tests
{
    /// <summary>
    /// 诊断 TimeseriesMetadata 的 ChunkMetadataList
    /// </summary>
    public class DiagnoseTreeModelChunkMetadata
    {
        [Fact]
        public void DiagnoseChunkMetadataList()
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
                Console.WriteLine($"=== 查询设备: {deviceName} ===");
                Console.WriteLine();

                // 使用反射访问私有方法和字段
                var readerType = typeof(TsFileReader);

                // 获取 _tableIndexNodes
                var tableIndexNodesField = readerType.GetField("_tableIndexNodes",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var tableIndexNodes = tableIndexNodesField?.GetValue(reader) as System.Collections.Generic.Dictionary<string, MetadataIndexNode>;

                if (tableIndexNodes == null || !tableIndexNodes.ContainsKey(deviceName))
                {
                    Console.WriteLine($"错误：找不到设备索引节点");
                    return;
                }

                var rootNode = tableIndexNodes[deviceName];

                // 调用 NavigateToTimeseriesMetadata
                var navigateMethod = readerType.GetMethod("NavigateToTimeseriesMetadata",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (navigateMethod == null)
                {
                    Console.WriteLine("错误：找不到 NavigateToTimeseriesMetadata 方法");
                    return;
                }

                var timeseriesMetadataList = navigateMethod.Invoke(reader, new object[] { rootNode, deviceName, null });

                if (timeseriesMetadataList is System.Collections.IList list && list.Count > 0)
                {
                    Console.WriteLine($"=== TimeseriesMetadata 详情 ===");
                    Console.WriteLine($"数量: {list.Count}");
                    Console.WriteLine();

                    for (int i = 0; i < list.Count; i++)
                    {
                        var tsMetadata = list[i];
                        var tsMetadataType = tsMetadata.GetType();

                        Console.WriteLine($"TimeseriesMetadata #{i + 1}:");

                        // 获取 MeasurementId
                        var measurementIdProp = tsMetadataType.GetProperty("MeasurementId");
                        var measurementId = measurementIdProp?.GetValue(tsMetadata);
                        Console.WriteLine($"  MeasurementId: {measurementId}");

                        // 获取 DataType
                        var dataTypeProp = tsMetadataType.GetProperty("DataType");
                        var dataType = dataTypeProp?.GetValue(tsMetadata);
                        Console.WriteLine($"  DataType: {dataType}");

                        // 获取 ChunkMetadataList
                        var chunkMetadataListProp = tsMetadataType.GetProperty("ChunkMetadataList");
                        var chunkMetadataList = chunkMetadataListProp?.GetValue(tsMetadata);

                        if (chunkMetadataList is System.Collections.IList chunkList)
                        {
                            Console.WriteLine($"  ChunkMetadataList 数量: {chunkList.Count}");

                            if (chunkList.Count > 0)
                            {
                                Console.WriteLine($"  Chunk 详情:");
                                for (int j = 0; j < chunkList.Count; j++)
                                {
                                    var chunk = chunkList[j];
                                    Console.WriteLine($"    Chunk #{j + 1}: {chunk}");

                                    // 获取 chunk 的详细信息
                                    var chunkType = chunk.GetType();
                                    var offsetProp = chunkType.GetProperty("OffsetOfChunkHeader");
                                    var offset = offsetProp?.GetValue(chunk);
                                    Console.WriteLine($"      OffsetOfChunkHeader: {offset}");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"  警告：ChunkMetadataList 为空！");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"  错误：ChunkMetadataList 类型不正确");
                        }

                        Console.WriteLine();
                    }
                }
                else
                {
                    Console.WriteLine("错误：NavigateToTimeseriesMetadata 返回空列表");
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
