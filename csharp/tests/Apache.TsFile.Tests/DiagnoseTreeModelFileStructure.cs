using System;
using System.IO;
using System.Text;
using Xunit;

namespace Apache.TsFile.Tests
{
    /// <summary>
    /// 诊断 Tree Model V4 文件的完整结构
    /// </summary>
    public class DiagnoseTreeModelFileStructure
    {
        [Fact]
        public void DiagnoseCompleteFileStructure()
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
                Console.WriteLine("=== 文件头 (offset 0-6) ===");
                var magic = reader.ReadBytes(6);
                Console.WriteLine($"Magic: {System.Text.Encoding.ASCII.GetString(magic)}");
                var version = reader.ReadByte();
                Console.WriteLine($"Version: {version}");
                Console.WriteLine($"当前位置: {fs.Position}");
                Console.WriteLine();

                // 2. 读取文件头后的数据（offset 7 开始）
                Console.WriteLine("=== 数据区域 (offset 7-150) ===");
                fs.Position = 7;
                var dataBytes = reader.ReadBytes(Math.Min(150, (int)(fs.Length - 7)));

                Console.WriteLine("十六进制视图:");
                for (int i = 0; i < dataBytes.Length; i += 16)
                {
                    var offset = 7 + i;
                    var hex = BitConverter.ToString(dataBytes, i, Math.Min(16, dataBytes.Length - i)).Replace("-", " ");

                    // 尝试解析为 ASCII
                    var ascii = new StringBuilder();
                    for (int j = i; j < Math.Min(i + 16, dataBytes.Length); j++)
                    {
                        var b = dataBytes[j];
                        ascii.Append(b >= 32 && b < 127 ? (char)b : '.');
                    }

                    Console.WriteLine($"{offset,4:D4}: {hex,-47} | {ascii}");
                }
                Console.WriteLine();

                // 3. 特别关注 offset 4 的位置
                Console.WriteLine("=== Offset 4 分析 ===");
                fs.Position = 4;
                Console.WriteLine($"Offset 4 的前 20 字节:");
                var chunk4 = reader.ReadBytes(20);
                Console.WriteLine(BitConverter.ToString(chunk4).Replace("-", " "));
                Console.WriteLine();

                // 尝试解析
                fs.Position = 4;
                var byte0 = reader.ReadByte();
                Console.WriteLine($"Byte 0: 0x{byte0:X2} ({byte0})");

                if (byte0 == 0x6C) // 108
                {
                    Console.WriteLine("这可能是 ChunkGroup marker (0x6C)");
                    Console.WriteLine("Tree Model V4 可能使用 ChunkGroup 结构");
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
    }
}
