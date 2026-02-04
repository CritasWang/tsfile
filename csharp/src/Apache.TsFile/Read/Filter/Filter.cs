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
/// Base class for all filters used in TsFile queries.
/// A filter is an executable expression tree describing criteria for which records to keep
/// when loading data from a TsFile.
/// </summary>
public abstract class Filter
{
    /// <summary>
    /// Gets the operator type of this filter.
    /// </summary>
    public abstract OperatorType OperatorType { get; }
    
    /// <summary>
    /// Examines whether a single point (with time and value) satisfies the filter.
    /// </summary>
    /// <param name="time">The timestamp of the point.</param>
    /// <param name="value">The value of the point.</param>
    /// <returns>True if the point satisfies the filter; otherwise, false.</returns>
    public abstract bool Satisfy(long time, object? value);
    
    /// <summary>
    /// Examines whether a single point with boolean value satisfies the filter.
    /// </summary>
    public abstract bool SatisfyBoolean(long time, bool value);
    
    /// <summary>
    /// Examines whether a single point with integer value satisfies the filter.
    /// </summary>
    public abstract bool SatisfyInteger(long time, int value);
    
    /// <summary>
    /// Examines whether a single point with long value satisfies the filter.
    /// </summary>
    public abstract bool SatisfyLong(long time, long value);
    
    /// <summary>
    /// Examines whether a single point with float value satisfies the filter.
    /// </summary>
    public abstract bool SatisfyFloat(long time, float value);
    
    /// <summary>
    /// Examines whether a single point with double value satisfies the filter.
    /// </summary>
    public abstract bool SatisfyDouble(long time, double value);
    
    /// <summary>
    /// Examines whether a single point with string value satisfies the filter.
    /// </summary>
    public abstract bool SatisfyString(long time, string? value);
    
    /// <summary>
    /// Examines whether a single point with binary value satisfies the filter.
    /// </summary>
    public abstract bool SatisfyBinary(long time, byte[]? value);
    
    /// <summary>
    /// Examines whether the specified time range satisfies the filter.
    /// This is used to determine if a chunk or page can be skipped entirely.
    /// </summary>
    /// <param name="startTime">The start time of the range.</param>
    /// <param name="endTime">The end time of the range.</param>
    /// <returns>True if the time range satisfies the filter; otherwise, false.</returns>
    public abstract bool SatisfyStartEndTime(long startTime, long endTime);
    
    /// <summary>
    /// Examines whether the time range [startTime, endTime] is fully contained by this filter.
    /// </summary>
    /// <param name="startTime">The start time of the range.</param>
    /// <param name="endTime">The end time of the range.</param>
    /// <returns>True if all points in the time range satisfy the filter; otherwise, false.</returns>
    public abstract bool ContainStartEndTime(long startTime, long endTime);
    
    /// <summary>
    /// Returns the time ranges which satisfy the filter.
    /// </summary>
    /// <returns>A list of time ranges that satisfy the filter.</returns>
    public abstract IReadOnlyList<TimeRange> GetTimeRanges();
    
    /// <summary>
    /// Returns the logical inverse of this filter.
    /// </summary>
    /// <returns>A filter that is the logical negation of this filter.</returns>
    public abstract Filter Reverse();
    
    /// <summary>
    /// Creates a copy of this filter.
    /// When the filter is stateless, this may return the same instance.
    /// </summary>
    /// <returns>A copy of this filter.</returns>
    public virtual Filter Copy()
    {
        return this;
    }
    
    /// <summary>
    /// Examines whether all values in a chunk or page satisfy the filter
    /// based on its metadata/statistics.
    /// </summary>
    /// <param name="statistics">The statistics metadata.</param>
    /// <returns>True if all values satisfy the filter; otherwise, false.</returns>
    public virtual bool AllSatisfy(IStatistics? statistics)
    {
        if (statistics == null)
            return false;
        return ContainStartEndTime(statistics.StartTime, statistics.EndTime);
    }
    
    /// <summary>
    /// Examines whether a chunk or page can be skipped entirely based on its metadata/statistics.
    /// </summary>
    /// <param name="statistics">The statistics metadata.</param>
    /// <returns>True if the chunk/page can be skipped; otherwise, false.</returns>
    public virtual bool CanSkip(IStatistics? statistics)
    {
        if (statistics == null)
            return false;
        return !SatisfyStartEndTime(statistics.StartTime, statistics.EndTime);
    }
}

/// <summary>
/// Interface for metadata statistics used in filter optimization.
/// </summary>
public interface IStatistics
{
    /// <summary>
    /// Gets the start time (minimum timestamp).
    /// </summary>
    long StartTime { get; }
    
    /// <summary>
    /// Gets the end time (maximum timestamp).
    /// </summary>
    long EndTime { get; }
    
    /// <summary>
    /// Gets the count of values.
    /// </summary>
    long Count { get; }
}
