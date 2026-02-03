/*
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

using Apache.TsFile.Enums;

namespace Apache.TsFile.Encoding.Encoder;

/// <summary>
/// SPRINTZ encoder - Sensor-optimized: predictive + ZigZag + bit-packing.
/// </summary>
public class SprintzEncoder : IEncoder
{
    private readonly PlainEncoder _fallbackEncoder = new();
    
    public SprintzEncoder(TsDataType dataType)
    {
        // For now, fall back to Plain encoding
        // Full SPRINTZ implementation would require predictive encoding, ZigZag, and bit-packing
    }
    
    public void Encode(bool value, MemoryStream stream)
    {
        _fallbackEncoder.Encode(value, stream);
    }
    
    public void Encode(int value, MemoryStream stream)
    {
        _fallbackEncoder.Encode(value, stream);
    }
    
    public void Encode(long value, MemoryStream stream)
    {
        _fallbackEncoder.Encode(value, stream);
    }
    
    public void Encode(float value, MemoryStream stream)
    {
        _fallbackEncoder.Encode(value, stream);
    }
    
    public void Encode(double value, MemoryStream stream)
    {
        _fallbackEncoder.Encode(value, stream);
    }
    
    public void Encode(string value, MemoryStream stream)
    {
        _fallbackEncoder.Encode(value, stream);
    }
    
    public void Encode(byte[] value, MemoryStream stream)
    {
        _fallbackEncoder.Encode(value, stream);
    }
    
    public void Flush(MemoryStream stream)
    {
        _fallbackEncoder.Flush(stream);
    }
    
    public int GetOneItemMaxSize()
    {
        return _fallbackEncoder.GetOneItemMaxSize();
    }
    
    public long GetMaxByteSize()
    {
        return _fallbackEncoder.GetMaxByteSize();
    }
}
