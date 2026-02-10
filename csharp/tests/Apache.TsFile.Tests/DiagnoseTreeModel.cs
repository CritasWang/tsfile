using System;
using System.IO;
using Xunit;

namespace Apache.TsFile.Tests
{
    /// <summary>
    /// 诊断 Tree Model V4 文件的元数据结构
    /// </summary>
    public class DiagnoseTreeModel
    {
        [Fact]
        public void DiagnoseTreeModelV4File()
        {
            var testFile = "/tmp/interop-tests/java-comprehensive/boolean_plain_gzip_alternating.tsfile";

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

                // 2. 读取文件头后的数据
                Console.WriteLine("=== 文件头后数据 ===");
                var headerBytes = reader.ReadBytes(20);
                Console.WriteLine("前 20 字节:");
                Console.WriteLine(BitConverter.ToString(headerBytes).Replace("-", " "));
                Console.WriteLine();

                // 3. 跳到文件尾部读取元数据大小
                Console.WriteLine("=== 文件尾部 ===");
                fs.Seek(-10, SeekOrigin.End);
                Console.WriteLine($"尾部位置: {fs.Position}");

                var metadataSize = ReadInt32BigEndian(reader);
                Console.WriteLine($"元数据大小: {metadataSize} 字节");

                var footerMagic = reader.ReadBytes(6);
                Console.WriteLine($"Footer Magic: {System.Text.Encoding.ASCII.GetString(footerMagic)}");
                Console.WriteLine();

                // 4. 定位到元数据开始位置
                var metadataEndPos = fs.Length - 4 - 6;
                var metadataStartPos = metadataEndPos - metadataSize;

                Console.WriteLine($"元数据开始位置: {metadataStartPos}");
                Console.WriteLine($"元数据结束位置: {metadataEndPos}");
                Console.WriteLine();

                // 输出元数据开始位置的前 80 字节
                fs.Position = metadataStartPos;
                var metadataBytes = reader.ReadBytes(Math.Min(80, (int)(metadataEndPos - metadataStartPos)));
                Console.WriteLine("元数据前 80 字节:");
                Console.WriteLine(BitConverter.ToString(metadataBytes).Replace("-", " "));
                Console.WriteLine();

                fs.Position = metadataStartPos;

                // 5. 读取元数据
                Console.WriteLine("=== 元数据解析 ===");

                // 5.1 读取表索引节点数量
                var tableIndexNodeNum = ReadUnsignedVarInt(reader);
                Console.WriteLine($"表索引节点数量: {tableIndexNodeNum}");
                Console.WriteLine($"当前位置: {fs.Position}");
                Console.WriteLine();

                // 5.2 读取每个表的索引节点
                for (int i = 0; i < tableIndexNodeNum && i < 3; i++)
                {
                    Console.WriteLine($"--- 表索引节点 #{i + 1} ---");
                    var tableName = ReadVarIntString(reader, fs);
                    Console.WriteLine($"表名/设备路径: {tableName}");
                    Console.WriteLine($"当前位置: {fs.Position}");

                    // 读取 MetadataIndexNode
                    var entryCount = ReadUnsignedVarInt(reader);
                    Console.WriteLine($"Entry 数量: {entryCount}");
                    Console.WriteLine($"当前位置: {fs.Position}");

                    // 跳过 entries（暂不解析）
                    for (int j = 0; j < entryCount; j++)
                    {
                        // 跳过 DeviceMetadataIndexEntry
                        // - segment count (VarInt)
                        // - segments (VarIntString[])
                        // - offset (Int64)
                        var segmentCount = ReadUnsignedVarInt(reader);
                        Console.WriteLine($"  Entry #{j + 1}: segment count = {segmentCount}");
                        for (int k = 0; k < segmentCount; k++)
                        {
                            var segment = ReadVarIntString(reader, fs);
                            Console.WriteLine($"    Segment #{k + 1}: {segment}");
                        }
                        var offset = ReadInt64BigEndian(reader);
                        Console.WriteLine($"    Offset: {offset}");
                    }

                    // 读取 endOffset 和 nodeType
                    var endOffset = ReadInt64BigEndian(reader);
                    var nodeType = reader.ReadByte();
                    Console.WriteLine($"End Offset: {endOffset}");
                    Console.WriteLine($"Node Type: {nodeType}");
                    Console.WriteLine($"当前位置: {fs.Position}");
                    Console.WriteLine();
                }

                // 5.3 读取表 schema 数量
                Console.WriteLine("=== 表 Schema 解析 ===");
                var tableSchemaNum = ReadUnsignedVarInt(reader);
                Console.WriteLine($"表 Schema 数量: {tableSchemaNum}");
                Console.WriteLine($"当前位置: {fs.Position}");
                Console.WriteLine();

                if (tableSchemaNum > 0)
                {
                    Console.WriteLine("Tree Model 文件有 table schemas");
                }
                else
                {
                    Console.WriteLine("Tree Model 文件没有 table schemas（这是预期的）");
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

        private static long ReadInt64BigEndian(BinaryReader reader)
        {
            var bytes = reader.ReadBytes(8);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return BitConverter.ToInt64(bytes, 0);
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
