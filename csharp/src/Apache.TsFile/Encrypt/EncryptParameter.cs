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

namespace Apache.TsFile.Encrypt;

/// <summary>
/// Parameters for encryption configuration.
/// </summary>
public class EncryptParameter
{
    /// <summary>
    /// Gets the encryption type name.
    /// </summary>
    public string Type { get; }
    
    /// <summary>
    /// Gets the encryption key.
    /// </summary>
    public byte[] Key { get; }
    
    /// <summary>
    /// Creates a new encrypt parameter with the specified type and key.
    /// </summary>
    /// <param name="type">The encryption type name.</param>
    /// <param name="key">The encryption key bytes.</param>
    public EncryptParameter(string type, byte[] key)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Key = key ?? throw new ArgumentNullException(nameof(key));
    }
    
    /// <summary>
    /// Creates a new encrypt parameter with no encryption.
    /// </summary>
    public static EncryptParameter Unencrypted => new(EncryptionType.Unencrypted.GetExtension(), Array.Empty<byte>());
}
