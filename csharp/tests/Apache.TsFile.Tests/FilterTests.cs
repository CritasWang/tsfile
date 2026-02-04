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

using Apache.TsFile.Read.Filter;
using Xunit;

namespace Apache.TsFile.Tests;

public class FilterTests
{
    #region TimeFilter Tests
    
    [Fact]
    public void TimeFilter_Eq_ShouldMatchExactTimestamp()
    {
        var filter = TimeFilter.Eq(100);
        
        Assert.True(filter.Satisfy(100, null));
        Assert.False(filter.Satisfy(99, null));
        Assert.False(filter.Satisfy(101, null));
    }
    
    [Fact]
    public void TimeFilter_NotEq_ShouldNotMatchExactTimestamp()
    {
        var filter = TimeFilter.NotEq(100);
        
        Assert.False(filter.Satisfy(100, null));
        Assert.True(filter.Satisfy(99, null));
        Assert.True(filter.Satisfy(101, null));
    }
    
    [Fact]
    public void TimeFilter_Gt_ShouldMatchGreaterTimestamps()
    {
        var filter = TimeFilter.Gt(100);
        
        Assert.False(filter.Satisfy(100, null));
        Assert.False(filter.Satisfy(99, null));
        Assert.True(filter.Satisfy(101, null));
    }
    
    [Fact]
    public void TimeFilter_GtEq_ShouldMatchGreaterOrEqualTimestamps()
    {
        var filter = TimeFilter.GtEq(100);
        
        Assert.True(filter.Satisfy(100, null));
        Assert.False(filter.Satisfy(99, null));
        Assert.True(filter.Satisfy(101, null));
    }
    
    [Fact]
    public void TimeFilter_Lt_ShouldMatchLesserTimestamps()
    {
        var filter = TimeFilter.Lt(100);
        
        Assert.False(filter.Satisfy(100, null));
        Assert.True(filter.Satisfy(99, null));
        Assert.False(filter.Satisfy(101, null));
    }
    
    [Fact]
    public void TimeFilter_LtEq_ShouldMatchLesserOrEqualTimestamps()
    {
        var filter = TimeFilter.LtEq(100);
        
        Assert.True(filter.Satisfy(100, null));
        Assert.True(filter.Satisfy(99, null));
        Assert.False(filter.Satisfy(101, null));
    }
    
    [Fact]
    public void TimeFilter_Between_ShouldMatchTimestampsInRange()
    {
        var filter = TimeFilter.Between(50, 100);
        
        Assert.True(filter.Satisfy(50, null));
        Assert.True(filter.Satisfy(75, null));
        Assert.True(filter.Satisfy(100, null));
        Assert.False(filter.Satisfy(49, null));
        Assert.False(filter.Satisfy(101, null));
    }
    
    [Fact]
    public void TimeFilter_SatisfyStartEndTime_ShouldCheckTimeRange()
    {
        var filter = TimeFilter.GtEq(50);
        
        Assert.True(filter.SatisfyStartEndTime(100, 200));
        Assert.True(filter.SatisfyStartEndTime(50, 100));
        Assert.False(filter.SatisfyStartEndTime(10, 49));
    }
    
    [Fact]
    public void TimeFilter_GetTimeRanges_ShouldReturnCorrectRanges()
    {
        var filter = TimeFilter.Between(50, 100);
        var ranges = filter.GetTimeRanges();
        
        Assert.Single(ranges);
        Assert.Equal(50, ranges[0].Min);
        Assert.Equal(100, ranges[0].Max);
    }
    
    #endregion
    
    #region ValueFilter Tests
    
    [Fact]
    public void ValueFilter_Eq_ShouldMatchExactValue()
    {
        var filter = ValueFilter.Eq(42);
        
        Assert.True(filter.SatisfyInteger(100, 42));
        Assert.False(filter.SatisfyInteger(100, 41));
        Assert.False(filter.SatisfyInteger(100, 43));
    }
    
    [Fact]
    public void ValueFilter_Gt_ShouldMatchGreaterValues()
    {
        var filter = ValueFilter.Gt(42);
        
        Assert.False(filter.SatisfyInteger(100, 42));
        Assert.False(filter.SatisfyInteger(100, 41));
        Assert.True(filter.SatisfyInteger(100, 43));
    }
    
    [Fact]
    public void ValueFilter_Lt_ShouldMatchLesserValues()
    {
        var filter = ValueFilter.Lt(42);
        
        Assert.False(filter.SatisfyInteger(100, 42));
        Assert.True(filter.SatisfyInteger(100, 41));
        Assert.False(filter.SatisfyInteger(100, 43));
    }
    
    [Fact]
    public void ValueFilter_Between_ShouldMatchValuesInRange()
    {
        var filter = ValueFilter.Between(10, 20);
        
        Assert.True(filter.SatisfyInteger(100, 10));
        Assert.True(filter.SatisfyInteger(100, 15));
        Assert.True(filter.SatisfyInteger(100, 20));
        Assert.False(filter.SatisfyInteger(100, 9));
        Assert.False(filter.SatisfyInteger(100, 21));
    }
    
    [Fact]
    public void ValueFilter_IsNull_ShouldMatchNullValues()
    {
        var filter = ValueFilter.IsNull();
        
        Assert.True(filter.Satisfy(100, null));
        Assert.False(filter.SatisfyInteger(100, 42));
        Assert.True(filter.SatisfyString(100, null));
        Assert.False(filter.SatisfyString(100, "test"));
    }
    
    [Fact]
    public void ValueFilter_IsNotNull_ShouldMatchNonNullValues()
    {
        var filter = ValueFilter.IsNotNull();
        
        Assert.False(filter.Satisfy(100, null));
        Assert.True(filter.SatisfyInteger(100, 42));
        Assert.False(filter.SatisfyString(100, null));
        Assert.True(filter.SatisfyString(100, "test"));
    }
    
    #endregion
    
    #region Logical Filter Tests
    
    [Fact]
    public void AndFilter_ShouldRequireBothConditions()
    {
        var filter1 = TimeFilter.GtEq(50);
        var filter2 = TimeFilter.LtEq(100);
        var andFilter = new AndFilter(filter1, filter2);
        
        Assert.True(andFilter.Satisfy(50, null));
        Assert.True(andFilter.Satisfy(75, null));
        Assert.True(andFilter.Satisfy(100, null));
        Assert.False(andFilter.Satisfy(49, null));
        Assert.False(andFilter.Satisfy(101, null));
    }
    
    [Fact]
    public void OrFilter_ShouldRequireAnyCondition()
    {
        var filter1 = TimeFilter.Lt(50);
        var filter2 = TimeFilter.Gt(100);
        var orFilter = new OrFilter(filter1, filter2);
        
        Assert.True(orFilter.Satisfy(49, null));
        Assert.True(orFilter.Satisfy(101, null));
        Assert.False(orFilter.Satisfy(50, null));
        Assert.False(orFilter.Satisfy(75, null));
        Assert.False(orFilter.Satisfy(100, null));
    }
    
    [Fact]
    public void NotFilter_ShouldNegateCondition()
    {
        var filter = TimeFilter.Eq(100);
        var notFilter = new NotFilter(filter);
        
        Assert.False(notFilter.Satisfy(100, null));
        Assert.True(notFilter.Satisfy(99, null));
        Assert.True(notFilter.Satisfy(101, null));
    }
    
    #endregion
    
    #region FilterFactory Tests
    
    [Fact]
    public void FilterFactory_And_ShouldHandleNullFilters()
    {
        var filter = TimeFilter.Eq(100);
        
        Assert.Same(filter, FilterFactory.And(filter, null));
        Assert.Same(filter, FilterFactory.And(null, filter));
        Assert.Null(FilterFactory.And(null, null));
    }
    
    [Fact]
    public void FilterFactory_Or_ShouldHandleNullFilters()
    {
        var filter = TimeFilter.Eq(100);
        
        Assert.Same(filter, FilterFactory.Or(filter, null));
        Assert.Same(filter, FilterFactory.Or(null, filter));
        Assert.Null(FilterFactory.Or(null, null));
    }
    
    #endregion
    
    #region TimeRange Tests
    
    [Fact]
    public void TimeRange_Contains_ShouldCheckTimestampInRange()
    {
        var range = new TimeRange(50, 100);
        
        Assert.True(range.Contains(50));
        Assert.True(range.Contains(75));
        Assert.True(range.Contains(100));
        Assert.False(range.Contains(49));
        Assert.False(range.Contains(101));
    }
    
    [Fact]
    public void TimeRange_Overlaps_ShouldCheckRangeOverlap()
    {
        var range1 = new TimeRange(50, 100);
        var range2 = new TimeRange(75, 125);
        var range3 = new TimeRange(101, 150);
        
        Assert.True(range1.Overlaps(range2));
        Assert.True(range2.Overlaps(range1));
        Assert.False(range1.Overlaps(range3));
    }
    
    #endregion
}
