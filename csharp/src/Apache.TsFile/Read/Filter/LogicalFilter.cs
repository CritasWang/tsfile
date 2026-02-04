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

namespace Apache.TsFile.Read.Filter;

/// <summary>
/// Logical AND filter - combines two filters with AND logic.
/// </summary>
public sealed class AndFilter : Filter
{
    private readonly Filter _left;
    private readonly Filter _right;
    
    public AndFilter(Filter left, Filter right)
    {
        _left = left ?? throw new ArgumentNullException(nameof(left));
        _right = right ?? throw new ArgumentNullException(nameof(right));
    }
    
    /// <summary>
    /// Gets the left operand filter.
    /// </summary>
    public Filter Left => _left;
    
    /// <summary>
    /// Gets the right operand filter.
    /// </summary>
    public Filter Right => _right;
    
    public override OperatorType OperatorType => OperatorType.And;
    
    public override bool Satisfy(long time, object? value) => 
        _left.Satisfy(time, value) && _right.Satisfy(time, value);
    
    public override bool SatisfyBoolean(long time, bool value) => 
        _left.SatisfyBoolean(time, value) && _right.SatisfyBoolean(time, value);
    
    public override bool SatisfyInteger(long time, int value) => 
        _left.SatisfyInteger(time, value) && _right.SatisfyInteger(time, value);
    
    public override bool SatisfyLong(long time, long value) => 
        _left.SatisfyLong(time, value) && _right.SatisfyLong(time, value);
    
    public override bool SatisfyFloat(long time, float value) => 
        _left.SatisfyFloat(time, value) && _right.SatisfyFloat(time, value);
    
    public override bool SatisfyDouble(long time, double value) => 
        _left.SatisfyDouble(time, value) && _right.SatisfyDouble(time, value);
    
    public override bool SatisfyString(long time, string? value) => 
        _left.SatisfyString(time, value) && _right.SatisfyString(time, value);
    
    public override bool SatisfyBinary(long time, byte[]? value) => 
        _left.SatisfyBinary(time, value) && _right.SatisfyBinary(time, value);
    
    public override bool SatisfyStartEndTime(long startTime, long endTime) => 
        _left.SatisfyStartEndTime(startTime, endTime) && _right.SatisfyStartEndTime(startTime, endTime);
    
    public override bool ContainStartEndTime(long startTime, long endTime) => 
        _left.ContainStartEndTime(startTime, endTime) && _right.ContainStartEndTime(startTime, endTime);
    
    public override IReadOnlyList<TimeRange> GetTimeRanges()
    {
        var leftRanges = _left.GetTimeRanges();
        var rightRanges = _right.GetTimeRanges();
        
        var result = new List<TimeRange>();
        foreach (var leftRange in leftRanges)
        {
            foreach (var rightRange in rightRanges)
            {
                var start = Math.Max(leftRange.Min, rightRange.Min);
                var end = Math.Min(leftRange.Max, rightRange.Max);
                if (start <= end)
                {
                    result.Add(new TimeRange(start, end));
                }
            }
        }
        return result;
    }
    
    public override Filter Reverse() => new OrFilter(_left.Reverse(), _right.Reverse());
    
    public override Filter Copy() => new AndFilter(_left.Copy(), _right.Copy());
}

/// <summary>
/// Logical OR filter - combines two filters with OR logic.
/// </summary>
public sealed class OrFilter : Filter
{
    private readonly Filter _left;
    private readonly Filter _right;
    
    public OrFilter(Filter left, Filter right)
    {
        _left = left ?? throw new ArgumentNullException(nameof(left));
        _right = right ?? throw new ArgumentNullException(nameof(right));
    }
    
    /// <summary>
    /// Gets the left operand filter.
    /// </summary>
    public Filter Left => _left;
    
    /// <summary>
    /// Gets the right operand filter.
    /// </summary>
    public Filter Right => _right;
    
    public override OperatorType OperatorType => OperatorType.Or;
    
    public override bool Satisfy(long time, object? value) => 
        _left.Satisfy(time, value) || _right.Satisfy(time, value);
    
    public override bool SatisfyBoolean(long time, bool value) => 
        _left.SatisfyBoolean(time, value) || _right.SatisfyBoolean(time, value);
    
    public override bool SatisfyInteger(long time, int value) => 
        _left.SatisfyInteger(time, value) || _right.SatisfyInteger(time, value);
    
    public override bool SatisfyLong(long time, long value) => 
        _left.SatisfyLong(time, value) || _right.SatisfyLong(time, value);
    
    public override bool SatisfyFloat(long time, float value) => 
        _left.SatisfyFloat(time, value) || _right.SatisfyFloat(time, value);
    
    public override bool SatisfyDouble(long time, double value) => 
        _left.SatisfyDouble(time, value) || _right.SatisfyDouble(time, value);
    
    public override bool SatisfyString(long time, string? value) => 
        _left.SatisfyString(time, value) || _right.SatisfyString(time, value);
    
    public override bool SatisfyBinary(long time, byte[]? value) => 
        _left.SatisfyBinary(time, value) || _right.SatisfyBinary(time, value);
    
    public override bool SatisfyStartEndTime(long startTime, long endTime) => 
        _left.SatisfyStartEndTime(startTime, endTime) || _right.SatisfyStartEndTime(startTime, endTime);
    
    public override bool ContainStartEndTime(long startTime, long endTime) => 
        _left.ContainStartEndTime(startTime, endTime) || _right.ContainStartEndTime(startTime, endTime);
    
    public override IReadOnlyList<TimeRange> GetTimeRanges()
    {
        var leftRanges = _left.GetTimeRanges();
        var rightRanges = _right.GetTimeRanges();
        
        var result = new List<TimeRange>(leftRanges.Count + rightRanges.Count);
        result.AddRange(leftRanges);
        result.AddRange(rightRanges);
        
        // Merge overlapping ranges
        result.Sort((a, b) => a.Min.CompareTo(b.Min));
        var merged = new List<TimeRange>();
        foreach (var range in result)
        {
            if (merged.Count == 0 || merged[^1].Max < range.Min - 1)
            {
                merged.Add(range);
            }
            else
            {
                var last = merged[^1];
                merged[^1] = new TimeRange(last.Min, Math.Max(last.Max, range.Max));
            }
        }
        return merged;
    }
    
    public override Filter Reverse() => new AndFilter(_left.Reverse(), _right.Reverse());
    
    public override Filter Copy() => new OrFilter(_left.Copy(), _right.Copy());
}

/// <summary>
/// Logical NOT filter - negates a filter.
/// </summary>
public sealed class NotFilter : Filter
{
    private readonly Filter _filter;
    
    public NotFilter(Filter filter)
    {
        _filter = filter ?? throw new ArgumentNullException(nameof(filter));
    }
    
    /// <summary>
    /// Gets the negated filter.
    /// </summary>
    public Filter InnerFilter => _filter;
    
    public override OperatorType OperatorType => OperatorType.Not;
    
    public override bool Satisfy(long time, object? value) => !_filter.Satisfy(time, value);
    
    public override bool SatisfyBoolean(long time, bool value) => !_filter.SatisfyBoolean(time, value);
    
    public override bool SatisfyInteger(long time, int value) => !_filter.SatisfyInteger(time, value);
    
    public override bool SatisfyLong(long time, long value) => !_filter.SatisfyLong(time, value);
    
    public override bool SatisfyFloat(long time, float value) => !_filter.SatisfyFloat(time, value);
    
    public override bool SatisfyDouble(long time, double value) => !_filter.SatisfyDouble(time, value);
    
    public override bool SatisfyString(long time, string? value) => !_filter.SatisfyString(time, value);
    
    public override bool SatisfyBinary(long time, byte[]? value) => !_filter.SatisfyBinary(time, value);
    
    public override bool SatisfyStartEndTime(long startTime, long endTime) => true;
    
    public override bool ContainStartEndTime(long startTime, long endTime) => false;
    
    public override IReadOnlyList<TimeRange> GetTimeRanges() => 
        new[] { new TimeRange(long.MinValue, long.MaxValue) };
    
    public override Filter Reverse() => _filter.Copy();
    
    public override Filter Copy() => new NotFilter(_filter.Copy());
}
