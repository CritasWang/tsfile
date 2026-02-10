using System;
using System.IO;
using Apache.TsFile.Enums;
using Xunit;

namespace Apache.TsFile.Tests
{
    /// <summary>
    /// 诊断 Tree Model V4 文件的 Measurement Index Node 读取
    /// </summary>
    public class DiagnoseTreeModelMeasurementNode
    {
        [Fact]
        public void DiagnoseMeasurementNodeReading()
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
                using var fs = new FileStream(testFile, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(fs);

                // 从诊断结果得知：DeviceMetadataIndexEntry 的 offset 是 106
                var measurementNodeOffset = 106L;

                Console.WriteLine($"=== 在 offset {measurementNodeOffset} 读取 Measurement Index Node ===");
                fs.Position = measurementNodeOffset;
                Console.WriteLine($"当前位置: {fs.Position}");
                Console.WriteLine();

                // 读取 entry count
                var entryCount = ReadUnsignedVarInt(reader);
                Console.WriteLine($"Entry 数量: {entryCount}");
                Console.WriteLine($"当前位置: {fs.Position}");
                Console.WriteLine();

                if (entryCount > 0)
                {
                    Console.WriteLine($"=== Measurement Entries ===");
                    for (int i = 0; i < entryCount; i++)
                    {
                        Console.WriteLine($"Entry #{i + 1}:");

                        // 读取 measurement name
                        var nameLength = ReadVarInt(reader, fs);
                        Console.WriteLine($"  Name length: {nameLength}");

                        if (nameLength > 0)
                        {
                            var nameBytes = reader.ReadBytes(nameLength);
                            var name = System.Text.Encoding.UTF8.GetString(nameBytes);
                            Console.WriteLine($"  Name: {name}");
                        }

                        // 读取 offset
                        var offset = ReadInt64BigEndian(reader);
                        Console.WriteLine($"  Offset: {offset}");
                        Console.WriteLine($"  当前位置: {fs.Position}");
                        Console.WriteLine();
                    }
                }

                // 读取 endOffset 和 nodeType
                var endOffset = ReadInt64BigEndian(reader);
                var nodeType = reader.ReadByte();
                Console.WriteLine($"End Offset: {endOffset}");
                Console.WriteLine($"Node Type: {nodeType} ({(MetadataIndexNodeType)nodeType})");
                Console.WriteLine($"当前位置: {fs.Position}");
                Console.WriteLine();

                Console.WriteLine("诊断完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
                Console.WriteLine($"堆栈: {ex.StackTrace}");
            }
        }

        private static int ReadUnsignedVarInt(BinaryReader reader)
        {
            int value = 0;
            int shift = 0;
            byte b;

            do
            {
                b = reader.ReadByte();
                value |= (b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);

            return value;
        }

        private static int ReadVarInt(BinaryReader reader, FileStream fs)
        {
            // Read unsigned VarInt first
            int value = ReadUnsignedVarInt(reader);

            // Apply ZigZag decoding
            int x = (int)((uint)value >> 1);
            if ((value & 1) != 0)
            {
                x = ~x;
            }
            return x;
        }

        private static long ReadInt64BigEndian(BinaryReader reader)
        {
            var bytes = reader.ReadBytes(8);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return BitConverter.ToInt64(bytes, 0);
        }
    }
}
