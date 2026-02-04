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
using Apache.TsFile.Read;
using Apache.TsFile.Write;
using Xunit;

namespace Apache.TsFile.Tests;

public class BuilderTests
{
    [Fact]
    public void TsFileWriterBuilder_Build_ThrowsOnMissingFilePath()
    {
        var builder = new TsFileWriterBuilder();
        
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }
    
    [Fact]
    public void TsFileWriterBuilder_Build_CreatesWriter()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var builder = new TsFileWriterBuilder()
                .FilePath(tempFile);
            
            using var writer = builder.Build();
            
            Assert.NotNull(writer);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
    
    [Fact]
    public void TsFileReaderBuilder_Build_ThrowsOnMissingFilePath()
    {
        var builder = new TsFileReaderBuilder();
        
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }
    
    [Fact]
    public void TsFileWriterV4Builder_Build_ThrowsOnMissingSchema()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var builder = new TsFileWriterV4Builder()
                .FilePath(tempFile);
            
            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
    
    [Fact]
    public void TsBlock_Creation_Works()
    {
        var timeColumn = new TimeColumn(new long[] { 1000, 2000, 3000 }, 3);
        var intColumn = new IntColumn(new int[] { 100, 200, 300 }, null, 3);
        
        var tsBlock = new TsBlock(timeColumn, intColumn);
        
        Assert.Equal(3, tsBlock.PositionCount);
        Assert.Equal(1, tsBlock.ValueColumnCount);
        Assert.Equal(1000, tsBlock.StartTime);
        Assert.Equal(3000, tsBlock.EndTime);
        Assert.False(tsBlock.IsEmpty);
    }
    
    [Fact]
    public void TsBlock_GetTime_ReturnsCorrectValue()
    {
        var timeColumn = new TimeColumn(new long[] { 1000, 2000, 3000 }, 3);
        var tsBlock = new TsBlock(timeColumn);
        
        Assert.Equal(1000, tsBlock.GetTime(0));
        Assert.Equal(2000, tsBlock.GetTime(1));
        Assert.Equal(3000, tsBlock.GetTime(2));
    }
    
    [Fact]
    public void TsBlock_GetColumn_ReturnsCorrectColumn()
    {
        var timeColumn = new TimeColumn(new long[] { 1000, 2000 }, 2);
        var intColumn = new IntColumn(new int[] { 100, 200 }, null, 2);
        var doubleColumn = new DoubleColumn(new double[] { 1.5, 2.5 }, null, 2);
        
        var tsBlock = new TsBlock(timeColumn, intColumn, doubleColumn);
        
        var col0 = tsBlock.GetColumn(0);
        var col1 = tsBlock.GetColumn(1);
        
        Assert.Equal(TsDataType.Int32, col0.DataType);
        Assert.Equal(TsDataType.Double, col1.DataType);
    }
    
    [Fact]
    public void TsBlockBuilder_Build_CreatesTsBlock()
    {
        var builder = new TsBlockBuilder(new[] { TsDataType.Int32 });
        var timeBuilder = builder.GetTimeColumnBuilder();
        var valueBuilder = builder.GetColumnBuilder(0);
        
        timeBuilder.WriteLong(1000);
        valueBuilder.WriteInt(100);
        builder.DeclarePosition();
        
        timeBuilder.WriteLong(2000);
        valueBuilder.WriteInt(200);
        builder.DeclarePosition();
        
        var tsBlock = builder.Build();
        
        Assert.Equal(2, tsBlock.PositionCount);
        Assert.Equal(1000, tsBlock.GetTime(0));
        Assert.Equal(2000, tsBlock.GetTime(1));
    }
    
    [Fact]
    public void IntColumn_GetObject_ReturnsCorrectValue()
    {
        var column = new IntColumn(new int[] { 100, 200, 300 }, null, 3);
        
        Assert.Equal(100, column.GetObject(0));
        Assert.Equal(200, column.GetObject(1));
        Assert.Equal(300, column.GetObject(2));
        Assert.False(column.IsNull(0));
    }
    
    [Fact]
    public void IntColumn_WithNulls_HandlesNullValues()
    {
        var column = new IntColumn(
            new int[] { 100, 0, 300 }, 
            new bool[] { false, true, false }, 
            3);
        
        Assert.Equal(100, column.GetObject(0));
        Assert.Null(column.GetObject(1));
        Assert.Equal(300, column.GetObject(2));
        
        Assert.False(column.IsNull(0));
        Assert.True(column.IsNull(1));
        Assert.False(column.IsNull(2));
    }
}
