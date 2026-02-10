using System;
using System.IO;
using Xunit;

namespace Apache.TsFile.Tests
{
    /// <summary>
    /// 诊断 TimeseriesMetadata 的原始字节和 offset 编码
    /// </summary>
    public class DiagnoseTimeseriesMetadataBytes
    {
        [Fact]
        public void DiagnoseTimeseriesMetadataAtOffset65()
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

                // TimeseriesMetadata 在 offset 65
                var tsMetadataOffset = 65L;

                Console.WriteLine($"=== 在 offset {tsMetadataOffset} 读取 TimeseriesMetadata ===");
                fs.Position = tsMetadataOffset;

                // 读取前 100 字节
                var bytes = reader.ReadBytes(100);
                Console.WriteLine("前 100 字节 (十六进制):");
                for (int i = 0; i < bytes.Length; i += 16)
                {
                    var lineOffset = tsMetadataOffset + i;
                    var hex = BitConverter.ToString(bytes, i, Math.Min(16, bytes.Length - i)).Replace("-", " ");
                    Console.WriteLine($"{lineOffset,4:D4}: {hex}");
                }
                Console.WriteLine();

                // 重新定位并逐字段读取
                fs.Position = tsMetadataOffset;

                Console.WriteLine("=== 逐字段解析 ===");

                // TimeSeriesMetadataType
                var metadataType = reader.ReadByte();
                Console.WriteLine($"TimeSeriesMetadataType: 0x{metadataType:X2} ({metadataType})");
                Console.WriteLine($"  HasMultiplePages (0x01): {(metadataType & 0x01) != 0}");
                Console.WriteLine($"  TimeChunkMask (0x80): {(metadataType & 0x80) != 0}");
                Console.WriteLine($"  ValueChunkMask (0x40): {(metadataType & 0x40) != 0}");

                // MeasurementId
                var nameLength = ReadVarInt(reader, fs);
                Console.WriteLine($"MeasurementId length: {nameLength}");
                var nameBytes = reader.ReadBytes(nameLength);
                var measurementId = System.Text.Encoding.UTF8.GetString(nameBytes);
                Console.WriteLine($"MeasurementId: {measurementId}");

                // DataType
                var dataType = reader.ReadByte();
                Console.WriteLine($"DataType: {dataType}");

                // ChunkMetadataListDataSize
                var chunkMetadataListDataSize = ReadVarInt(reader, fs);
                Console.WriteLine($"ChunkMetadataListDataSize: {chunkMetadataListDataSize}");
                Console.WriteLine($"当前位置: {fs.Position}");
                Console.WriteLine();

                // 跳过 Statistics（根据 DataType 计算大小）
                Console.WriteLine("=== 跳过 Statistics ===");
                // Boolean statistics: count(8) + startTime(8) + endTime(8) + min(1) + max(1) + first(1) + last(1) + sum(8) = 36 bytes
                var statsSize = 36;
                fs.Position += statsSize;
                Console.WriteLine($"跳过 {statsSize} 字节的 Statistics");
                Console.WriteLine($"当前位置: {fs.Position}");
                Console.WriteLine();

                // 读取 ChunkMetadata
                Console.WriteLine("=== ChunkMetadata ===");
                var chunkMetadataStart = fs.Position;
                Console.WriteLine($"ChunkMetadata 开始位置: {chunkMetadataStart}");

                // 读取 offset (unsigned var long)
                Console.WriteLine("读取 offset (unsigned var long):");
                var offsetBytes = new System.Collections.Generic.List<byte>();
                byte b;
                do
                {
                    b = reader.ReadByte();
                    offsetBytes.Add(b);
                    Console.WriteLine($"  Byte: 0x{b:X2} ({b})");
                } while ((b & 0x80) != 0);

                // 解码 unsigned var long
                long offset = 0;
                int shift = 0;
                foreach (var by in offsetBytes)
                {
                    offset |= (long)(by & 0x7F) << shift;
                    shift += 7;
                }
                Console.WriteLine($"解码后的 offset: {offset}");
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

        private static int ReadVarInt(BinaryReader reader, FileStream fs)
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

            // Apply ZigZag decoding
            int x = (int)((uint)value >> 1);
            if ((value & 1) != 0)
            {
                x = ~x;
            }
            return x;
        }
    }
}
