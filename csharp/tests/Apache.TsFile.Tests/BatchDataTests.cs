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
using Apache.TsFile.Read.Common;
using Xunit;

namespace Apache.TsFile.Tests;

public class BatchDataTests
{
    [Fact]
    public void BatchData_PutAndGet_Boolean()
    {
        var batchData = new BatchData(TsDataType.Boolean);
        
        batchData.PutBoolean(1000, true);
        batchData.PutBoolean(1001, false);
        batchData.PutBoolean(1002, true);
        
        Assert.Equal(3, batchData.Count);
        Assert.True(batchData.HasCurrent);
        
        Assert.Equal(1000, batchData.CurrentTime);
        Assert.True(batchData.GetBoolean());
        batchData.Next();
        
        Assert.Equal(1001, batchData.CurrentTime);
        Assert.False(batchData.GetBoolean());
        batchData.Next();
        
        Assert.Equal(1002, batchData.CurrentTime);
        Assert.True(batchData.GetBoolean());
        batchData.Next();
        
        Assert.False(batchData.HasCurrent);
    }
    
    [Fact]
    public void BatchData_PutAndGet_Int32()
    {
        var batchData = new BatchData(TsDataType.Int32);
        
        batchData.PutInt(1000, 100);
        batchData.PutInt(1001, 200);
        batchData.PutInt(1002, 300);
        
        Assert.Equal(3, batchData.Count);
        
        Assert.Equal(100, batchData.GetIntByIndex(0));
        Assert.Equal(200, batchData.GetIntByIndex(1));
        Assert.Equal(300, batchData.GetIntByIndex(2));
    }
    
    [Fact]
    public void BatchData_PutAndGet_Int64()
    {
        var batchData = new BatchData(TsDataType.Int64);
        
        batchData.PutLong(1000, 100L);
        batchData.PutLong(1001, 200L);
        
        Assert.Equal(2, batchData.Count);
        Assert.Equal(100L, batchData.GetLongByIndex(0));
        Assert.Equal(200L, batchData.GetLongByIndex(1));
    }
    
    [Fact]
    public void BatchData_PutAndGet_Float()
    {
        var batchData = new BatchData(TsDataType.Float);
        
        batchData.PutFloat(1000, 1.5f);
        batchData.PutFloat(1001, 2.5f);
        
        Assert.Equal(2, batchData.Count);
        Assert.Equal(1.5f, batchData.GetFloatByIndex(0));
        Assert.Equal(2.5f, batchData.GetFloatByIndex(1));
    }
    
    [Fact]
    public void BatchData_PutAndGet_Double()
    {
        var batchData = new BatchData(TsDataType.Double);
        
        batchData.PutDouble(1000, 1.5);
        batchData.PutDouble(1001, 2.5);
        
        Assert.Equal(2, batchData.Count);
        Assert.Equal(1.5, batchData.GetDoubleByIndex(0));
        Assert.Equal(2.5, batchData.GetDoubleByIndex(1));
    }
    
    [Fact]
    public void BatchData_PutAndGet_String()
    {
        var batchData = new BatchData(TsDataType.String);
        
        batchData.PutString(1000, "hello");
        batchData.PutString(1001, "world");
        
        Assert.Equal(2, batchData.Count);
        Assert.Equal("hello", batchData.GetStringByIndex(0));
        Assert.Equal("world", batchData.GetStringByIndex(1));
    }
    
    [Fact]
    public void BatchData_GetTimeByIndex_ReturnsCorrectTimestamp()
    {
        var batchData = new BatchData(TsDataType.Int32);
        
        batchData.PutInt(1000, 100);
        batchData.PutInt(2000, 200);
        batchData.PutInt(3000, 300);
        
        Assert.Equal(1000, batchData.GetTimeByIndex(0));
        Assert.Equal(2000, batchData.GetTimeByIndex(1));
        Assert.Equal(3000, batchData.GetTimeByIndex(2));
    }
    
    [Fact]
    public void BatchData_MinMaxTimestamp_ReturnsCorrectValues()
    {
        var batchData = new BatchData(TsDataType.Int32);
        
        batchData.PutInt(1000, 100);
        batchData.PutInt(2000, 200);
        batchData.PutInt(3000, 300);
        
        Assert.Equal(1000, batchData.GetMinTimestamp());
        Assert.Equal(3000, batchData.GetMaxTimestamp());
    }
    
    [Fact]
    public void BatchData_Reset_ReturnsToBeginning()
    {
        var batchData = new BatchData(TsDataType.Int32);
        
        batchData.PutInt(1000, 100);
        batchData.PutInt(2000, 200);
        
        batchData.Next();
        Assert.Equal(2000, batchData.CurrentTime);
        
        batchData.ResetBatchData();
        Assert.Equal(1000, batchData.CurrentTime);
    }
    
    [Fact]
    public void BatchData_CurrentValue_ReturnsCorrectValue()
    {
        var batchData = new BatchData(TsDataType.Int32);
        
        batchData.PutInt(1000, 42);
        
        Assert.Equal(42, batchData.CurrentValue);
    }
    
    [Fact]
    public void BatchData_Empty_Properties()
    {
        var batchData = new BatchData(TsDataType.Int32);
        
        Assert.Equal(0, batchData.Count);
        Assert.True(batchData.IsEmpty);
        Assert.False(batchData.HasCurrent);
    }
}
