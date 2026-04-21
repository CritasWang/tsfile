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

//! TsFile format constants and specifications

/// Magic string at the start and end of TsFile
pub const MAGIC_STRING_TSFILE: &[u8] = b"TsFile";

/// TsFile version number
pub const VERSION_NUM_BYTE: u8 = 0x04;

/// Chunk group header marker
pub const CHUNK_GROUP_HEADER_MARKER: u8 = 0x00;

/// Chunk header marker (multiple pages)
pub const CHUNK_HEADER_MARKER: u8 = 0x01;

/// Chunk header marker (only one page)
pub const ONLY_ONE_PAGE_CHUNK_HEADER_MARKER: u8 = 0x05;

/// Separator marker (marks start of index section)
pub const SEPARATOR_MARKER: u8 = 0x02;

/// Operation index range marker
pub const OPERATION_INDEX_RANGE: u8 = 0x04;

/// Time column mask for aligned format
pub const TIME_COLUMN_MASK: u8 = 0x80;

/// Value column mask for aligned format
pub const VALUE_COLUMN_MASK: u8 = 0x40;

/// Size of the file footer (magic string + metadata size)
pub const FILE_FOOTER_SIZE: usize = 10; // 6 bytes magic + 4 bytes i32

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_constants() {
        assert_eq!(MAGIC_STRING_TSFILE, b"TsFile");
        assert_eq!(MAGIC_STRING_TSFILE.len(), 6);
        assert_eq!(VERSION_NUM_BYTE, 0x04);
        assert_eq!(FILE_FOOTER_SIZE, 10);
    }
}
