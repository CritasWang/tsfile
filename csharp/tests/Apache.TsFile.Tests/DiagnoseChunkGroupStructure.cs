using System;
using System.IO;
using Xunit;

namespace Apache.TsFile.Tests
{
    /// <summary>
    /// 诊断 ChunkGroup 完整结构
    /// </summary>
    public class DiagnoseChunkGroupStructure
    {
        [Fact]
        public void DiagnoseCompleteChunkGroupStructure()
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

                // 跳过文件头（7 字节），从数据区域开始搜索 ChunkGroup marker (0x6C)
                Console.WriteLine("=== 搜索 ChunkGroup marker (0x6C) ===");

                fs.Position = 7; // 跳过 "TsFile" (6 bytes) + version (1 byte)
                long chunkGroupOffset = -1;

                while (fs.Position < fs.Length)
                {
                    var b = reader.ReadByte();
                    if (b == 0x6C)
                    {
                        chunkGroupOffset = fs.Position - 1;
                        Console.WriteLine($"找到 ChunkGroup marker 在 offset: {chunkGroupOffset}");
                        break;
                    }
                }

                if (chunkGroupOffset == -1)
                {
                    Console.WriteLine("未找到 ChunkGroup marker");
                    return;
                }

                Console.WriteLine();

                // 读取 ChunkGroup header
                fs.Position = chunkGroupOffset;
                Console.WriteLine($"=== 在 offset {chunkGroupOffset} 读取 ChunkGroup Header ===");

                var marker = reader.ReadByte();
                Console.WriteLine($"Marker: 0x{marker:X2} ({marker})");

                // 读取 DeviceID（VarInt count + VarIntString segments）
                var segmentCount = ReadUnsignedVarInt(reader);
                Console.WriteLine($"DeviceID segment count: {segmentCount}");

                for (int i = 0; i < segmentCount; i++)
                {
                    var segmentLength = ReadVarInt(reader, fs);
                    var segmentBytes = reader.ReadBytes(segmentLength);
                    var segment = System.Text.Encoding.UTF8.GetString(segmentBytes);
                    Console.WriteLine($"  Segment #{i + 1}: {segment}");
                }

                Console.WriteLine($"ChunkGroup header 结束位置: {fs.Position}");
                Console.WriteLine();

                // 读取 ChunkGroup 内的 chunks
                Console.WriteLine("=== 读取 ChunkGroup 内的 chunks ===");

                for (int chunkIndex = 0; chunkIndex < 5; chunkIndex++)
                {
                    var chunkStartPos = fs.Position;
                    Console.WriteLine($"Chunk #{chunkIndex + 1} 开始位置: {chunkStartPos}");

                    var chunkMarker = reader.ReadByte();
                    Console.WriteLine($"  Marker: 0x{chunkMarker:X2} ({chunkMarker})");

                    if (chunkMarker == 0x01 || chunkMarker == 0x05)
                    {
                        // Chunk header
                        var measurementLength = ReadVarInt(reader, fs);
                        var measurementBytes = reader.ReadBytes(measurementLength);
                        var measurementId = System.Text.Encoding.UTF8.GetString(measurementBytes);
                        Console.WriteLine($"  MeasurementId: {measurementId}");

                        var dataSize = ReadVarInt(reader, fs);
                        Console.WriteLine($"  Data size: {dataSize}");

                        var dataType = reader.ReadByte();
                        Console.WriteLine($"  Data type: {dataType}");

                        var compression = reader.ReadByte();
                        Console.WriteLine($"  Compression: {compression}");

                        var encoding = reader.ReadByte();
                        Console.WriteLine($"  Encoding: {encoding}");

                        Console.WriteLine($"  Chunk data 开始位置: {fs.Position}");
                        Console.WriteLine($"  Chunk data 结束位置: {fs.Position + dataSize}");

                        // 跳过 chunk data
                        fs.Position += dataSize;
                        Console.WriteLine($"  当前位置: {fs.Position}");
                    }
                    else
                    {
                        Console.WriteLine($"  不是 chunk header marker，停止读取");
                        break;
                    }

                    Console.WriteLine();
                }

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
            int value = ReadUnsignedVarInt(reader);
            int x = (int)((uint)value >> 1);
            if ((value & 1) != 0)
            {
                x = ~x;
            }
            return x;
        }
    }
}
