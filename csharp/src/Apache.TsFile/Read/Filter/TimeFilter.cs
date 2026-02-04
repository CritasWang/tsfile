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
/// Time filter operators for filtering data based on timestamps.
/// </summary>
public abstract class TimeFilter : Filter
{
    /// <summary>
    /// Creates a filter for timestamps equal to the specified value.
    /// </summary>
    public static TimeFilter Eq(long value) => new TimeEqFilter(value);
    
    /// <summary>
    /// Creates a filter for timestamps not equal to the specified value.
    /// </summary>
    public static TimeFilter NotEq(long value) => new TimeNotEqFilter(value);
    
    /// <summary>
    /// Creates a filter for timestamps greater than the specified value.
    /// </summary>
    public static TimeFilter Gt(long value) => new TimeGtFilter(value);
    
    /// <summary>
    /// Creates a filter for timestamps greater than or equal to the specified value.
    /// </summary>
    public static TimeFilter GtEq(long value) => new TimeGtEqFilter(value);
    
    /// <summary>
    /// Creates a filter for timestamps less than the specified value.
    /// </summary>
    public static TimeFilter Lt(long value) => new TimeLtFilter(value);
    
    /// <summary>
    /// Creates a filter for timestamps less than or equal to the specified value.
    /// </summary>
    public static TimeFilter LtEq(long value) => new TimeLtEqFilter(value);
    
    /// <summary>
    /// Creates a filter for timestamps between the specified values (inclusive).
    /// </summary>
    public static TimeFilter Between(long minValue, long maxValue) => new TimeBetweenFilter(minValue, maxValue);
    
    /// <summary>
    /// Creates a filter for timestamps not between the specified values.
    /// </summary>
    public static TimeFilter NotBetween(long minValue, long maxValue) => new TimeNotBetweenFilter(minValue, maxValue);
    
    /// <inheritdoc />
    public override bool SatisfyBoolean(long time, bool value) => Satisfy(time, value);
    
    /// <inheritdoc />
    public override bool SatisfyInteger(long time, int value) => Satisfy(time, value);
    
    /// <inheritdoc />
    public override bool SatisfyLong(long time, long value) => Satisfy(time, value);
    
    /// <inheritdoc />
    public override bool SatisfyFloat(long time, float value) => Satisfy(time, value);
    
    /// <inheritdoc />
    public override bool SatisfyDouble(long time, double value) => Satisfy(time, value);
    
    /// <inheritdoc />
    public override bool SatisfyString(long time, string? value) => Satisfy(time, value);
    
    /// <inheritdoc />
    public override bool SatisfyBinary(long time, byte[]? value) => Satisfy(time, value);
}

/// <summary>
/// Time equals filter.
/// </summary>
public sealed class TimeEqFilter : TimeFilter
{
    private readonly long _value;
    
    public TimeEqFilter(long value) => _value = value;
    
    public override OperatorType OperatorType => OperatorType.TimeEq;
    
    public override bool Satisfy(long time, object? value) => time == _value;
    
    public override bool SatisfyStartEndTime(long startTime, long endTime) => 
        startTime <= _value && _value <= endTime;
    
    public override bool ContainStartEndTime(long startTime, long endTime) => 
        startTime == _value && endTime == _value;
    
    public override IReadOnlyList<TimeRange> GetTimeRanges() => 
        new[] { new TimeRange(_value, _value) };
    
    public override Filter Reverse() => new TimeNotEqFilter(_value);
}

/// <summary>
/// Time not equals filter.
/// </summary>
public sealed class TimeNotEqFilter : TimeFilter
{
    private readonly long _value;
    
    public TimeNotEqFilter(long value) => _value = value;
    
    public override OperatorType OperatorType => OperatorType.TimeNotEq;
    
    public override bool Satisfy(long time, object? value) => time != _value;
    
    public override bool SatisfyStartEndTime(long startTime, long endTime) => true;
    
    public override bool ContainStartEndTime(long startTime, long endTime) => 
        _value < startTime || _value > endTime;
    
    public override IReadOnlyList<TimeRange> GetTimeRanges() => 
        new[] { new TimeRange(long.MinValue, _value - 1), new TimeRange(_value + 1, long.MaxValue) };
    
    public override Filter Reverse() => new TimeEqFilter(_value);
}

/// <summary>
/// Time greater than filter.
/// </summary>
public sealed class TimeGtFilter : TimeFilter
{
    private readonly long _value;
    
    public TimeGtFilter(long value) => _value = value;
    
    public override OperatorType OperatorType => OperatorType.TimeGt;
    
    public override bool Satisfy(long time, object? value) => time > _value;
    
    public override bool SatisfyStartEndTime(long startTime, long endTime) => 
        endTime > _value;
    
    public override bool ContainStartEndTime(long startTime, long endTime) => 
        startTime > _value;
    
    public override IReadOnlyList<TimeRange> GetTimeRanges() => 
        new[] { new TimeRange(_value + 1, long.MaxValue) };
    
    public override Filter Reverse() => new TimeLtEqFilter(_value);
}

/// <summary>
/// Time greater than or equals filter.
/// </summary>
public sealed class TimeGtEqFilter : TimeFilter
{
    private readonly long _value;
    
    public TimeGtEqFilter(long value) => _value = value;
    
    public override OperatorType OperatorType => OperatorType.TimeGtEq;
    
    public override bool Satisfy(long time, object? value) => time >= _value;
    
    public override bool SatisfyStartEndTime(long startTime, long endTime) => 
        endTime >= _value;
    
    public override bool ContainStartEndTime(long startTime, long endTime) => 
        startTime >= _value;
    
    public override IReadOnlyList<TimeRange> GetTimeRanges() => 
        new[] { new TimeRange(_value, long.MaxValue) };
    
    public override Filter Reverse() => new TimeLtFilter(_value);
}

/// <summary>
/// Time less than filter.
/// </summary>
public sealed class TimeLtFilter : TimeFilter
{
    private readonly long _value;
    
    public TimeLtFilter(long value) => _value = value;
    
    public override OperatorType OperatorType => OperatorType.TimeLt;
    
    public override bool Satisfy(long time, object? value) => time < _value;
    
    public override bool SatisfyStartEndTime(long startTime, long endTime) => 
        startTime < _value;
    
    public override bool ContainStartEndTime(long startTime, long endTime) => 
        endTime < _value;
    
    public override IReadOnlyList<TimeRange> GetTimeRanges() => 
        new[] { new TimeRange(long.MinValue, _value - 1) };
    
    public override Filter Reverse() => new TimeGtEqFilter(_value);
}

/// <summary>
/// Time less than or equals filter.
/// </summary>
public sealed class TimeLtEqFilter : TimeFilter
{
    private readonly long _value;
    
    public TimeLtEqFilter(long value) => _value = value;
    
    public override OperatorType OperatorType => OperatorType.TimeLtEq;
    
    public override bool Satisfy(long time, object? value) => time <= _value;
    
    public override bool SatisfyStartEndTime(long startTime, long endTime) => 
        startTime <= _value;
    
    public override bool ContainStartEndTime(long startTime, long endTime) => 
        endTime <= _value;
    
    public override IReadOnlyList<TimeRange> GetTimeRanges() => 
        new[] { new TimeRange(long.MinValue, _value) };
    
    public override Filter Reverse() => new TimeGtFilter(_value);
}

/// <summary>
/// Time between filter (inclusive).
/// </summary>
public sealed class TimeBetweenFilter : TimeFilter
{
    private readonly long _minValue;
    private readonly long _maxValue;
    
    public TimeBetweenFilter(long minValue, long maxValue)
    {
        _minValue = minValue;
        _maxValue = maxValue;
    }
    
    public override OperatorType OperatorType => OperatorType.TimeBetweenAnd;
    
    public override bool Satisfy(long time, object? value) => 
        time >= _minValue && time <= _maxValue;
    
    public override bool SatisfyStartEndTime(long startTime, long endTime) => 
        startTime <= _maxValue && endTime >= _minValue;
    
    public override bool ContainStartEndTime(long startTime, long endTime) => 
        startTime >= _minValue && endTime <= _maxValue;
    
    public override IReadOnlyList<TimeRange> GetTimeRanges() => 
        new[] { new TimeRange(_minValue, _maxValue) };
    
    public override Filter Reverse() => new TimeNotBetweenFilter(_minValue, _maxValue);
}

/// <summary>
/// Time not between filter.
/// </summary>
public sealed class TimeNotBetweenFilter : TimeFilter
{
    private readonly long _minValue;
    private readonly long _maxValue;
    
    public TimeNotBetweenFilter(long minValue, long maxValue)
    {
        _minValue = minValue;
        _maxValue = maxValue;
    }
    
    public override OperatorType OperatorType => OperatorType.TimeNotBetweenAnd;
    
    public override bool Satisfy(long time, object? value) => 
        time < _minValue || time > _maxValue;
    
    public override bool SatisfyStartEndTime(long startTime, long endTime) => true;
    
    public override bool ContainStartEndTime(long startTime, long endTime) => 
        endTime < _minValue || startTime > _maxValue;
    
    public override IReadOnlyList<TimeRange> GetTimeRanges() => 
        new[] { new TimeRange(long.MinValue, _minValue - 1), new TimeRange(_maxValue + 1, long.MaxValue) };
    
    public override Filter Reverse() => new TimeBetweenFilter(_minValue, _maxValue);
}
