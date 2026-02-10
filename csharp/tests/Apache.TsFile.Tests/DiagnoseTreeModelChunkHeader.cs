using System;
using System.IO;
using Xunit;

namespace Apache.TsFile.Tests
{
    /// <summary>
    /// 诊断 Tree Model V4 文件的 Chunk Header
    /// </summary>
    public class DiagnoseTreeModelChunkHeader
    {
        [Fact]
        public void DiagnoseChunkHeaderAtOffset4()
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

                // Chunk offset 是 4
                var chunkOffset = 4L;

                Console.WriteLine($"=== 在 offset {chunkOffset} 读取 Chunk Header ===");
                fs.Position = chunkOffset;
                Console.WriteLine($"当前位置: {fs.Position}");
                Console.WriteLine();

                // 读取前 50 字节，看看实际内容
                var headerBytes = reader.ReadBytes(50);
                Console.WriteLine("前 50 字节 (十六进制):");
                Console.WriteLine(BitConverter.ToString(headerBytes).Replace("-", " "));
                Console.WriteLine();

                // 重新定位到 offset 4
                fs.Position = chunkOffset;

                // 读取 marker
                var marker = reader.ReadByte();
                Console.WriteLine($"Marker: 0x{marker:X2} (十进制: {marker})");
                Console.WriteLine($"是否为 CHUNK_HEADER (0x01): {marker == 0x01}");
                Console.WriteLine($"是否为 ONLY_ONE_PAGE_CHUNK_HEADER (0x05): {marker == 0x05}");
                Console.WriteLine($"当前位置: {fs.Position}");
                Console.WriteLine();

                if (marker == 0x01 || marker == 0x05)
                {
                    Console.WriteLine("=== 读取 Chunk Header 字段 ===");

                    // 读取 measurementId
                    var nameLength = ReadVarInt(reader, fs);
                    Console.WriteLine($"MeasurementId length: {nameLength}");

                    if (nameLength > 0 && nameLength < 100)
                    {
                        var nameBytes = reader.ReadBytes(nameLength);
                        var measurementId = System.Text.Encoding.UTF8.GetString(nameBytes);
                        Console.WriteLine($"MeasurementId: {measurementId}");
                    }

                    // 读取 dataSize
                    var dataSize = ReadVarInt(reader, fs);
                    Console.WriteLine($"Data size: {dataSize}");

                    // 读取 dataType
                    var dataType = reader.ReadByte();
                    Console.WriteLine($"Data type: {dataType}");

                    // 读取 compression
                    var compression = reader.ReadByte();
                    Console.WriteLine($"Compression: {compression}");

                    // 读取 encoding
                    var encoding = reader.ReadByte();
                    Console.WriteLine($"Encoding: {encoding}");

                    Console.WriteLine($"当前位置: {fs.Position}");
                }
                else
                {
                    Console.WriteLine($"警告：Marker 不是预期的 chunk header marker！");
                    Console.WriteLine($"这可能是 Tree Model V4 使用了不同的格式");
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

        private static int ReadVarInt(BinaryReader reader, FileStream fs)
        {
            // Read unsigned VarInt first
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
