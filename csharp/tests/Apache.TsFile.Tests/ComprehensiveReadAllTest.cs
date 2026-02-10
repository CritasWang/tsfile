using Apache.TsFile.IO;
using Xunit;
using Xunit.Abstractions;

namespace Apache.TsFile.Tests;

public class ComprehensiveReadAllTest
{
    private readonly ITestOutputHelper _output;
    public ComprehensiveReadAllTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public void ReadAll360JavaFiles()
    {
        var dir = Environment.GetEnvironmentVariable("JAVA_COMPREHENSIVE_DIR")
            ?? "/tmp/interop-tests/java-comprehensive";

        if (!Directory.Exists(dir))
        {
            _output.WriteLine("Skipping: directory not found");
            return;
        }

        var files = Directory.GetFiles(dir, "*.tsfile");
        if (files.Length == 0)
        {
            _output.WriteLine("Skipping: no .tsfile files found");
            return;
        }

        int success = 0, fail = 0;
        var errors = new List<string>();

        foreach (var file in files)
        {
            try
            {
                using var reader = new TsFileReader(file);
                Assert.Equal(4, reader.FileVersion);
                Assert.NotNull(reader.Schemas);

                // Query first device
                var deviceName = reader.Schemas.Keys.First();
                var result = reader.Query(deviceName);
                Assert.NotNull(result);
                Assert.True(result.Timestamps.Count > 0, $"No data in {Path.GetFileName(file)}");

                success++;
            }
            catch (Exception ex)
            {
                fail++;
                errors.Add($"{Path.GetFileName(file)}: {ex.GetType().Name}: {ex.Message} @ {ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}");
            }
        }

        _output.WriteLine($"Total: {files.Length}, Success: {success}, Fail: {fail}");

        // Group by full encoding name (handle multi-word like ts_2diff, gorilla_v1)
        var byFullEncoding = new Dictionary<string, int>();
        foreach (var e in errors)
        {
            var fname = Path.GetFileNameWithoutExtension(e.Split(':')[0]);
            // Parse: datatype_encoding_compression_pattern
            // Encodings: plain, rle, ts_2diff, gorilla, gorilla_v1, zigzag, dictionary
            string enc = "unknown";
            if (fname.Contains("_ts_2diff_")) enc = "ts_2diff";
            else if (fname.Contains("_gorilla_v1_")) enc = "gorilla_v1";
            else if (fname.Contains("_gorilla_")) enc = "gorilla";
            else if (fname.Contains("_plain_")) enc = "plain";
            else if (fname.Contains("_rle_")) enc = "rle";
            else if (fname.Contains("_zigzag_")) enc = "zigzag";
            else if (fname.Contains("_dictionary_")) enc = "dictionary";
            byFullEncoding[enc] = byFullEncoding.GetValueOrDefault(enc) + 1;
        }
        foreach (var kvp in byFullEncoding.OrderByDescending(k => k.Value))
            _output.WriteLine($"  Encoding '{kvp.Key}': {kvp.Value} failures");

        // Group by encoding+datatype
        var byEncType = new Dictionary<string, int>();
        foreach (var e in errors)
        {
            var fname = Path.GetFileNameWithoutExtension(e.Split(':')[0]);
            var dtype = fname.Split('_')[0]; // first part is datatype
            string enc = "unknown";
            if (fname.Contains("_ts_2diff_")) enc = "ts_2diff";
            else if (fname.Contains("_gorilla_v1_")) enc = "gorilla_v1";
            else if (fname.Contains("_gorilla_")) enc = "gorilla";
            else if (fname.Contains("_plain_")) enc = "plain";
            else if (fname.Contains("_rle_")) enc = "rle";
            else if (fname.Contains("_zigzag_")) enc = "zigzag";
            else if (fname.Contains("_dictionary_")) enc = "dictionary";
            var key = $"{enc}/{dtype}";
            byEncType[key] = byEncType.GetValueOrDefault(key) + 1;
        }
        _output.WriteLine("\nBy encoding/datatype:");
        foreach (var kvp in byEncType.OrderBy(k => k.Key))
            _output.WriteLine($"  {kvp.Key}: {kvp.Value} failures");

        Assert.True(fail == 0, $"{fail}/{files.Length} files failed.\nFirst errors:\n{string.Join("\n", errors.Take(10))}");
    }

    [Fact]
    public void DiagnoseSingleFile()
    {
        var dir = "/tmp/interop-tests/java-comprehensive";
        var file = Path.Combine(dir, "int32_plain_zstd_sequential.tsfile");
        if (!File.Exists(file))
        {
            _output.WriteLine("File not found, skipping");
            return;
        }

        // Read the raw file and find the value buffer for the non-aligned page
        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);
        
        // Skip header: magic(6) + version(1) = 7 bytes
        fs.Position = 7;
        
        // Read marker
        byte marker = br.ReadByte();
        _output.WriteLine($"First marker: 0x{marker:X2}");
        
        if (marker == 0x00)
        {
            // ChunkGroupHeader: skip device ID
            int segCount = ReadUVarInt(br);
            _output.WriteLine($"Device segments: {segCount}");
            for (int i = 0; i < segCount; i++)
            {
                int strLen = ReadZigZagVarInt(br);
                var strBytes = br.ReadBytes(strLen);
                _output.WriteLine($"  Segment[{i}]: {System.Text.Encoding.UTF8.GetString(strBytes)}");
            }
            marker = br.ReadByte();
            _output.WriteLine($"Chunk marker: 0x{marker:X2}");
        }
        
        // Dump raw bytes from chunk header position
        long chunkHeaderPos = fs.Position;
        var rawHeader = br.ReadBytes(30);
        _output.WriteLine($"Raw chunk header bytes (from pos {chunkHeaderPos}): {BitConverter.ToString(rawHeader, 0, Math.Min(30, rawHeader.Length))}");
        fs.Position = chunkHeaderPos;
        
        // Read chunk header: measurementID (VarIntString), dataSize (uVarInt), dataType, compression, encoding
        int measLen = ReadZigZagVarInt(br);
        var measName = System.Text.Encoding.UTF8.GetString(br.ReadBytes(measLen));
        int chunkSize = ReadUVarInt(br);
        byte dataType = br.ReadByte();
        byte compression = br.ReadByte();
        byte encoding = br.ReadByte();
        _output.WriteLine($"Measurement: {measName}, chunkDataSize: {chunkSize}, dataType: {dataType}, compression: {compression}, encoding: {encoding}");
        _output.WriteLine($"Position after chunk header: {fs.Position}");
        
        // Read page header
        int uncompressedSize = ReadUVarInt(br);
        int compressedSize = ReadUVarInt(br);
        _output.WriteLine($"Page: uncompressed={uncompressedSize}, compressed={compressedSize}");
        
        // Read page data (uncompressed since compression=0)
        var pageData = br.ReadBytes(compressedSize);
        _output.WriteLine($"Page data length: {pageData.Length}");
        
        // Parse non-aligned page: [timeBufferLength][timeBuffer][valueBuffer]
        int off = 0;
        int timeBufferLen = ReadUVarIntFromBytes(pageData, ref off);
        _output.WriteLine($"Time buffer length: {timeBufferLen}, offset after: {off}");
        
        var valueBuffer = new byte[pageData.Length - off - timeBufferLen];
        Array.Copy(pageData, off + timeBufferLen, valueBuffer, 0, valueBuffer.Length);
        _output.WriteLine($"Value buffer length: {valueBuffer.Length}");
        _output.WriteLine($"Value buffer first 20 bytes: {BitConverter.ToString(valueBuffer, 0, Math.Min(20, valueBuffer.Length))}");
        
        // Try to decode the value buffer as TS_2DIFF Int32
        int voff = 0;
        int packNum = ReadInt32BE(valueBuffer, ref voff);
        int packWidth = ReadInt32BE(valueBuffer, ref voff);
        int minDeltaBase = ReadInt32BE(valueBuffer, ref voff);
        int firstValue = ReadInt32BE(valueBuffer, ref voff);
        _output.WriteLine($"packNum={packNum}, packWidth={packWidth}, minDeltaBase={minDeltaBase}, firstValue=0x{firstValue:X8}");
        
        int encodingLength = (int)Math.Ceiling(packNum * packWidth / 8.0);
        _output.WriteLine($"encodingLength={encodingLength}, remaining bytes={valueBuffer.Length - voff}");
        
        Assert.True(true);
    }
    
    static int ReadUVarInt(BinaryReader br)
    {
        int v = 0, s = 0;
        byte b;
        do { b = br.ReadByte(); v |= (b & 0x7F) << s; s += 7; } while ((b & 0x80) != 0);
        return v;
    }
    static int ReadZigZagVarInt(BinaryReader br)
    {
        uint n = 0; int s = 0;
        byte b;
        do { b = br.ReadByte(); n |= (uint)(b & 0x7F) << s; s += 7; } while ((b & 0x80) != 0);
        return (int)(n >> 1) ^ -(int)(n & 1);
    }
    static int ReadUVarIntFromBytes(byte[] data, ref int offset)
    {
        int v = 0, s = 0;
        byte b;
        do { b = data[offset++]; v |= (b & 0x7F) << s; s += 7; } while ((b & 0x80) != 0);
        return v;
    }
    static int ReadInt32BE(byte[] data, ref int offset)
    {
        int v = (data[offset] << 24) | (data[offset+1] << 16) | (data[offset+2] << 8) | data[offset+3];
        offset += 4;
        return v;
    }
}
