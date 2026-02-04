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

using Apache.TsFile.Encrypt;
using Apache.TsFile.Enums;
using Xunit;

namespace Apache.TsFile.Tests;

public class EncryptionTests
{
    [Fact]
    public void EncryptionType_Deserialize_ReturnsCorrectType()
    {
        Assert.Equal(EncryptionType.Unencrypted, EncryptionTypeExtensions.Deserialize(0));
        Assert.Equal(EncryptionType.SM4128, EncryptionTypeExtensions.Deserialize(1));
        Assert.Equal(EncryptionType.AES128, EncryptionTypeExtensions.Deserialize(2));
        Assert.Equal(EncryptionType.Custom, EncryptionTypeExtensions.Deserialize(3));
    }
    
    [Fact]
    public void EncryptionType_Serialize_ReturnsCorrectByte()
    {
        Assert.Equal(0, EncryptionType.Unencrypted.Serialize());
        Assert.Equal(1, EncryptionType.SM4128.Serialize());
        Assert.Equal(2, EncryptionType.AES128.Serialize());
        Assert.Equal(3, EncryptionType.Custom.Serialize());
    }
    
    [Fact]
    public void EncryptionType_GetExtension_ReturnsCorrectName()
    {
        Assert.Equal("UNENCRYPTED", EncryptionType.Unencrypted.GetExtension());
        Assert.Equal("SM4128", EncryptionType.SM4128.GetExtension());
        Assert.Equal("AES128", EncryptionType.AES128.GetExtension());
        Assert.Equal("CUSTOM", EncryptionType.Custom.GetExtension());
    }
    
    [Fact]
    public void EncryptParameter_Constructor_StoresValues()
    {
        var key = new byte[] { 1, 2, 3, 4 };
        var param = new EncryptParameter("AES128", key);
        
        Assert.Equal("AES128", param.Type);
        Assert.Equal(key, param.Key);
    }
    
    [Fact]
    public void EncryptParameter_Unencrypted_ReturnsNoEncryption()
    {
        var param = EncryptParameter.Unencrypted;
        
        Assert.Equal("UNENCRYPTED", param.Type);
        Assert.Empty(param.Key);
    }
    
    [Fact]
    public void NoEncryptor_Encrypt_ReturnsDataUnchanged()
    {
        var encryptor = new NoEncryptor();
        var data = new byte[] { 1, 2, 3, 4, 5 };
        
        var result = encryptor.Encrypt(data);
        
        Assert.Equal(data, result);
        Assert.Equal(EncryptionType.Unencrypted, encryptor.EncryptionType);
    }
    
    [Fact]
    public void NoEncryptor_EncryptWithOffset_ReturnsSlice()
    {
        var encryptor = new NoEncryptor();
        var data = new byte[] { 1, 2, 3, 4, 5 };
        
        var result = encryptor.Encrypt(data, 1, 3);
        
        Assert.Equal(new byte[] { 2, 3, 4 }, result);
    }
    
    [Fact]
    public void NoDecryptor_Decrypt_ReturnsDataUnchanged()
    {
        var decryptor = new NoDecryptor();
        var data = new byte[] { 1, 2, 3, 4, 5 };
        
        var result = decryptor.Decrypt(data);
        
        Assert.Equal(data, result);
        Assert.Equal(EncryptionType.Unencrypted, decryptor.EncryptionType);
    }
    
    [Fact]
    public void NoDecryptor_DecryptWithOffset_ReturnsSlice()
    {
        var decryptor = new NoDecryptor();
        var data = new byte[] { 1, 2, 3, 4, 5 };
        
        var result = decryptor.Decrypt(data, 1, 3);
        
        Assert.Equal(new byte[] { 2, 3, 4 }, result);
    }
    
    [Fact]
    public void EncryptorFactory_GetEncryptor_ReturnsNoEncryptor_ForUnencrypted()
    {
        var encryptor = EncryptorFactory.GetEncryptor("UNENCRYPTED", Array.Empty<byte>());
        
        Assert.IsType<NoEncryptor>(encryptor);
    }
    
    [Fact]
    public void EncryptorFactory_GetEncryptor_ReturnsNoEncryptor_ForEmptyType()
    {
        var encryptor = EncryptorFactory.GetEncryptor("", Array.Empty<byte>());
        
        Assert.IsType<NoEncryptor>(encryptor);
    }
    
    [Fact]
    public void EncryptorFactory_GetEncryptor_ThrowsNotImplemented_ForOtherTypes()
    {
        Assert.Throws<NotImplementedException>(() => 
            EncryptorFactory.GetEncryptor("AES128", new byte[16]));
    }
    
    [Fact]
    public void DecryptorFactory_GetDecryptor_ReturnsNoDecryptor_ForUnencrypted()
    {
        var decryptor = DecryptorFactory.GetDecryptor("UNENCRYPTED", Array.Empty<byte>());
        
        Assert.IsType<NoDecryptor>(decryptor);
    }
    
    [Fact]
    public void DecryptorFactory_GetDecryptor_ThrowsNotImplemented_ForOtherTypes()
    {
        Assert.Throws<NotImplementedException>(() => 
            DecryptorFactory.GetDecryptor("AES128", new byte[16]));
    }
}
