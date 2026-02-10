using System;
using System.IO;
using System.Text;
using Xunit;

namespace Apache.TsFile.Tests
{
    /// <summary>
    /// 诊断工具：逐步解析 Java V4 文件的元数据结构
    /// </summary>
    public class DiagnoseV4Metadata
    {
        [Fact]
        public void DiagnoseTableModelV4File()
        {
            var testFile = "/tmp/interop-tests/java-table-model-v4/table_int32_uncompressed_alternating.tsfile";

            if (!File.Exists(testFile))
            {
                Console.WriteLine($"文件不存在: {testFile}");
                return;
            }

            Console.WriteLine($"分析文件: {testFile}");
            Console.WriteLine($"文件大小: {new FileInfo(testFile).Length} 字节");
            Console.WriteLine();

            try
            {
                using var fs = new FileStream(testFile, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(fs);

                // 1. 读取文件头
                Console.WriteLine("=== 文件头 ===");
                var magic = reader.ReadBytes(6);
                Console.WriteLine($"Magic: {System.Text.Encoding.ASCII.GetString(magic)}");

                var version = reader.ReadByte();
                Console.WriteLine($"Version: {version}");
                Console.WriteLine($"当前位置: {fs.Position}");
                Console.WriteLine();

                // 2. 跳到文件尾部读取元数据大小
                Console.WriteLine("=== 文件尾部 ===");
                fs.Seek(-10, SeekOrigin.End);
                Console.WriteLine($"尾部位置: {fs.Position}");

                var metadataSize = ReadInt32BigEndian(reader);
                Console.WriteLine($"元数据大小: {metadataSize} 字节");

                var footerMagic = reader.ReadBytes(6);
                Console.WriteLine($"Footer Magic: {System.Text.Encoding.ASCII.GetString(footerMagic)}");
                Console.WriteLine();

                // 3. 定位到元数据开始位置
                var metadataEndPos = fs.Length - 4 - 6;
                var metadataStartPos = metadataEndPos - metadataSize;

                Console.WriteLine($"元数据开始位置: {metadataStartPos}");
                Console.WriteLine($"元数据结束位置: {metadataEndPos}");
                Console.WriteLine();

                // 输出元数据开始位置的前 50 个字节
                fs.Position = metadataStartPos;
                var metadataBytes = reader.ReadBytes(Math.Min(50, (int)(metadataEndPos - metadataStartPos)));
                Console.WriteLine("元数据前 50 字节:");
                Console.WriteLine(BitConverter.ToString(metadataBytes).Replace("-", " "));
                Console.WriteLine();

                fs.Position = metadataStartPos;

                // 4. 读取元数据
                Console.WriteLine("=== 元数据解析 ===");

                // NOTE: SEPARATOR 只在 TimeseriesMetadata 之前，不在 TsFileMetadata 之前！
                // 第一个字节就是表数量

                // 4.1 读取表索引节点数量（使用 ReadUnsignedVarInt，无 ZigZag）
                var tableIndexNodeNum = ReadUnsignedVarInt(reader);
                Console.WriteLine($"表索引节点数量: {tableIndexNodeNum}");
                Console.WriteLine($"当前位置: {fs.Position}");
                Console.WriteLine();

                // 4.2 读取每个表的索引节点
                for (int i = 0; i < tableIndexNodeNum && i < 5; i++)
                {
                    Console.WriteLine($"--- 表索引节点 #{i + 1} ---");
                    var tableName = ReadVarIntString(reader, fs);
                    Console.WriteLine($"表名: {tableName}");
                    Console.WriteLine($"当前位置: {fs.Position}");

                    // 尝试读取 MetadataIndexNode
                    Console.WriteLine("尝试读取 MetadataIndexNode...");
                    var nodeType = reader.ReadByte();
                    Console.WriteLine($"节点类型: {nodeType}");
                    Console.WriteLine($"当前位置: {fs.Position}");

                    // 暂停，不继续读取
                    Console.WriteLine("(暂停，避免读取错误)");
                    Console.WriteLine();
                    break;
                }

                Console.WriteLine("诊断完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
                Console.WriteLine($"堆栈: {ex.StackTrace}");
            }
        }

        private static int ReadInt32BigEndian(BinaryReader reader)
        {
            var bytes = reader.ReadBytes(4);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return BitConverter.ToInt32(bytes, 0);
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

        private static string ReadVarIntString(BinaryReader reader, FileStream fs)
        {
            var length = ReadVarInt(reader, fs);
            var bytes = reader.ReadBytes(length);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
    }
}
