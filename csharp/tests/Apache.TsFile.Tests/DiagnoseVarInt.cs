using System;
using System.IO;
using Xunit;

namespace Apache.TsFile.Tests
{
    /// <summary>
    /// 诊断 VarInt 编码问题
    /// </summary>
    public class DiagnoseVarInt
    {
        [Fact]
        public void AnalyzeVarIntEncoding()
        {
            var testFile = "/tmp/interop-tests/java-table-model-v4/table_int32_uncompressed_alternating.tsfile";

            if (!File.Exists(testFile))
            {
                Console.WriteLine($"文件不存在: {testFile}");
                return;
            }

            using var fs = new FileStream(testFile, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fs);

            // 跳到元数据开始位置
            fs.Seek(-10, SeekOrigin.End);
            var metadataSize = ReadInt32BigEndian(reader);
            var metadataEndPos = fs.Length - 4 - 6;
            var metadataStartPos = metadataEndPos - metadataSize;

            Console.WriteLine($"元数据开始位置: {metadataStartPos}");
            Console.WriteLine($"元数据大小: {metadataSize}");
            Console.WriteLine();

            fs.Position = metadataStartPos;

            // 读取前 30 个字节
            var bytes = reader.ReadBytes(30);
            Console.WriteLine("前 30 字节:");
            for (int i = 0; i < bytes.Length; i++)
            {
                Console.WriteLine($"  [{i}] 0x{bytes[i]:X2} = {bytes[i]} (二进制: {Convert.ToString(bytes[i], 2).PadLeft(8, '0')})");
            }
            Console.WriteLine();

            // 手动解析 VarInt
            fs.Position = metadataStartPos;

            Console.WriteLine("=== 手动解析 VarInt ===");

            // 第一个字节
            var b1 = reader.ReadByte();
            Console.WriteLine($"字节 1: 0x{b1:X2} = {b1}");
            Console.WriteLine($"  最高位: {(b1 & 0x80) != 0}");
            Console.WriteLine($"  低 7 位: {b1 & 0x7F}");
            Console.WriteLine();

            // 第二个字节
            var b2 = reader.ReadByte();
            Console.WriteLine($"字节 2: 0x{b2:X2} = {b2}");
            Console.WriteLine($"  最高位: {(b2 & 0x80) != 0}");
            Console.WriteLine($"  低 7 位: {b2 & 0x7F}");
            Console.WriteLine();

            // 尝试不同的 VarInt 解码方式
            fs.Position = metadataStartPos;

            Console.WriteLine("=== 方式 1: 标准 VarInt（最高位继续标志）===");
            var value1 = ReadVarIntStandard(reader, fs);
            Console.WriteLine($"值: {value1}");
            Console.WriteLine($"当前位置: {fs.Position}");
            Console.WriteLine();

            fs.Position = metadataStartPos;

            Console.WriteLine("=== 方式 2: Unsigned VarInt（Protobuf 风格）===");
            var value2 = ReadVarIntProtobuf(reader, fs);
            Console.WriteLine($"值: {value2}");
            Console.WriteLine($"当前位置: {fs.Position}");
            Console.WriteLine();
        }

        private static int ReadInt32BigEndian(BinaryReader reader)
        {
            var bytes = reader.ReadBytes(4);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        private static int ReadVarIntStandard(BinaryReader reader, FileStream fs)
        {
            int value = 0;
            int shift = 0;
            byte b;

            do
            {
                b = reader.ReadByte();
                Console.WriteLine($"  读取字节: 0x{b:X2}, 最高位: {(b & 0x80) != 0}, 低 7 位: {b & 0x7F}");
                value |= (b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);

            return value;
        }

        private static int ReadVarIntProtobuf(BinaryReader reader, FileStream fs)
        {
            int value = 0;
            int shift = 0;
            byte b;

            do
            {
                b = reader.ReadByte();
                Console.WriteLine($"  读取字节: 0x{b:X2}, 最高位: {(b & 0x80) != 0}, 低 7 位: {b & 0x7F}");
                value |= (b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);

            return value;
        }
    }
}
