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

namespace Apache.TsFile.Enums;

/// <summary>
/// Enumeration of supported encryption types in TSFile format.
/// </summary>
public enum EncryptionType : byte
{
    /// <summary>No encryption.</summary>
    Unencrypted = 0,
    
    /// <summary>SM4 128-bit encryption.</summary>
    SM4128 = 1,
    
    /// <summary>AES 128-bit encryption.</summary>
    AES128 = 2,
    
    /// <summary>Custom encryption type for extensibility.</summary>
    Custom = 3
}

/// <summary>
/// Extension methods for EncryptionType enum.
/// </summary>
public static class EncryptionTypeExtensions
{
    /// <summary>
    /// Gets the extension name of the encryption type.
    /// </summary>
    public static string GetExtension(this EncryptionType encryptionType)
    {
        return encryptionType switch
        {
            EncryptionType.Unencrypted => "UNENCRYPTED",
            EncryptionType.SM4128 => "SM4128",
            EncryptionType.AES128 => "AES128",
            EncryptionType.Custom => "CUSTOM",
            _ => throw new ArgumentException($"Unknown encryption type: {encryptionType}")
        };
    }
    
    /// <summary>
    /// Gets the serialized size of encryption type.
    /// </summary>
    public static int GetSerializedSize()
    {
        return sizeof(byte);
    }
    
    /// <summary>
    /// Serializes the encryption type to a byte.
    /// </summary>
    public static byte Serialize(this EncryptionType encryptionType)
    {
        return (byte)encryptionType;
    }
    
    /// <summary>
    /// Deserializes an EncryptionType from a byte value.
    /// </summary>
    public static EncryptionType Deserialize(byte value)
    {
        return value switch
        {
            0 => EncryptionType.Unencrypted,
            1 => EncryptionType.SM4128,
            2 => EncryptionType.AES128,
            _ => EncryptionType.Custom
        };
    }
}
