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
/// Represents a time range with minimum and maximum timestamps.
/// </summary>
public readonly struct TimeRange
{
    /// <summary>
    /// Gets the minimum timestamp (inclusive).
    /// </summary>
    public long Min { get; }
    
    /// <summary>
    /// Gets the maximum timestamp (inclusive).
    /// </summary>
    public long Max { get; }
    
    /// <summary>
    /// Creates a new time range with the specified bounds.
    /// </summary>
    /// <param name="min">The minimum timestamp (inclusive).</param>
    /// <param name="max">The maximum timestamp (inclusive).</param>
    public TimeRange(long min, long max)
    {
        Min = min;
        Max = max;
    }
    
    /// <summary>
    /// Checks if the specified timestamp falls within this time range.
    /// </summary>
    /// <param name="timestamp">The timestamp to check.</param>
    /// <returns>True if the timestamp is within the range; otherwise, false.</returns>
    public bool Contains(long timestamp)
    {
        return timestamp >= Min && timestamp <= Max;
    }
    
    /// <summary>
    /// Checks if this time range fully contains another time range.
    /// </summary>
    /// <param name="startTime">The start time of the other range.</param>
    /// <param name="endTime">The end time of the other range.</param>
    /// <returns>True if this range fully contains the other range; otherwise, false.</returns>
    public bool Contains(long startTime, long endTime)
    {
        return Min <= startTime && Max >= endTime;
    }
    
    /// <summary>
    /// Checks if this time range overlaps with another time range.
    /// </summary>
    /// <param name="other">The other time range.</param>
    /// <returns>True if the ranges overlap; otherwise, false.</returns>
    public bool Overlaps(TimeRange other)
    {
        return Min <= other.Max && Max >= other.Min;
    }
    
    /// <inheritdoc />
    public override string ToString()
    {
        return $"[{Min}, {Max}]";
    }
    
    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is TimeRange other && Min == other.Min && Max == other.Max;
    }
    
    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(Min, Max);
    }
    
    /// <summary>
    /// Checks if two time ranges are equal.
    /// </summary>
    public static bool operator ==(TimeRange left, TimeRange right)
    {
        return left.Equals(right);
    }
    
    /// <summary>
    /// Checks if two time ranges are not equal.
    /// </summary>
    public static bool operator !=(TimeRange left, TimeRange right)
    {
        return !left.Equals(right);
    }
}
