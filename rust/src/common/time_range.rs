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

//! Time range utilities

use super::Timestamp;

/// Represents a time range for queries
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct TimeRange {
    /// Start time (inclusive)
    pub start_time: Timestamp,
    /// End time (inclusive)
    pub end_time: Timestamp,
}

impl TimeRange {
    /// Create a new time range
    pub fn new(start_time: Timestamp, end_time: Timestamp) -> Self {
        Self {
            start_time,
            end_time,
        }
    }

    /// Create an unbounded time range (all time)
    pub fn all() -> Self {
        Self {
            start_time: Timestamp::MIN,
            end_time: Timestamp::MAX,
        }
    }

    /// Check if a timestamp is within this range
    pub fn contains(&self, timestamp: Timestamp) -> bool {
        timestamp >= self.start_time && timestamp <= self.end_time
    }

    /// Check if this range overlaps with another range
    pub fn overlaps(&self, other: &TimeRange) -> bool {
        self.start_time <= other.end_time && self.end_time >= other.start_time
    }

    /// Get the intersection of two time ranges
    pub fn intersection(&self, other: &TimeRange) -> Option<TimeRange> {
        if !self.overlaps(other) {
            return None;
        }
        Some(TimeRange::new(
            self.start_time.max(other.start_time),
            self.end_time.min(other.end_time),
        ))
    }

    /// Get the duration of this time range
    pub fn duration(&self) -> i64 {
        self.end_time.saturating_sub(self.start_time)
    }
}

impl Default for TimeRange {
    fn default() -> Self {
        Self::all()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_time_range_creation() {
        let range = TimeRange::new(100, 200);
        assert_eq!(range.start_time, 100);
        assert_eq!(range.end_time, 200);

        let all = TimeRange::all();
        assert_eq!(all.start_time, Timestamp::MIN);
        assert_eq!(all.end_time, Timestamp::MAX);
    }

    #[test]
    fn test_contains() {
        let range = TimeRange::new(100, 200);
        assert!(range.contains(100));
        assert!(range.contains(150));
        assert!(range.contains(200));
        assert!(!range.contains(99));
        assert!(!range.contains(201));
    }

    #[test]
    fn test_overlaps() {
        let range1 = TimeRange::new(100, 200);
        let range2 = TimeRange::new(150, 250);
        let range3 = TimeRange::new(250, 300);

        assert!(range1.overlaps(&range2));
        assert!(range2.overlaps(&range1));
        assert!(!range1.overlaps(&range3));
        assert!(!range3.overlaps(&range1));
    }

    #[test]
    fn test_intersection() {
        let range1 = TimeRange::new(100, 200);
        let range2 = TimeRange::new(150, 250);
        let range3 = TimeRange::new(300, 400);

        let intersection = range1.intersection(&range2);
        assert!(intersection.is_some());
        let intersection = intersection.unwrap();
        assert_eq!(intersection.start_time, 150);
        assert_eq!(intersection.end_time, 200);

        assert!(range1.intersection(&range3).is_none());
    }

    #[test]
    fn test_duration() {
        let range = TimeRange::new(100, 200);
        assert_eq!(range.duration(), 100);

        let single = TimeRange::new(100, 100);
        assert_eq!(single.duration(), 0);
    }
}
