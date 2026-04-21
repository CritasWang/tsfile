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

//! File I/O utilities

use crate::error::Result;
use std::fs::File;
use std::io::{BufReader, BufWriter, Read, Seek, SeekFrom, Write};
use std::path::Path;

/// Write file handle for TsFile
pub struct WriteFile {
    writer: BufWriter<File>,
    position: u64,
}

impl WriteFile {
    /// Create a new write file
    pub fn create<P: AsRef<Path>>(path: P) -> Result<Self> {
        let file = File::create(path)?;
        Ok(Self {
            writer: BufWriter::new(file),
            position: 0,
        })
    }

    /// Write data to the file
    pub fn write(&mut self, data: &[u8]) -> Result<()> {
        self.writer.write_all(data)?;
        self.position += data.len() as u64;
        Ok(())
    }

    /// Flush any buffered data
    pub fn flush(&mut self) -> Result<()> {
        self.writer.flush()?;
        Ok(())
    }

    /// Get the current write position
    pub fn position(&self) -> u64 {
        self.position
    }

    /// Close the file
    pub fn close(mut self) -> Result<()> {
        self.writer.flush()?;
        Ok(())
    }
}

/// Read file handle for TsFile
pub struct ReadFile {
    reader: BufReader<File>,
    size: u64,
}

impl ReadFile {
    /// Open a file for reading
    pub fn open<P: AsRef<Path>>(path: P) -> Result<Self> {
        let file = File::open(&path)?;
        let size = file.metadata()?.len();
        Ok(Self {
            reader: BufReader::new(file),
            size,
        })
    }

    /// Read exact number of bytes
    pub fn read_exact(&mut self, buf: &mut [u8]) -> Result<()> {
        self.reader.read_exact(buf)?;
        Ok(())
    }

    /// Read up to buf.len() bytes
    pub fn read(&mut self, buf: &mut [u8]) -> Result<usize> {
        Ok(self.reader.read(buf)?)
    }

    /// Seek to a position
    pub fn seek(&mut self, pos: SeekFrom) -> Result<u64> {
        Ok(self.reader.seek(pos)?)
    }

    /// Get the file size
    pub fn size(&self) -> u64 {
        self.size
    }

    /// Get the current read position
    pub fn position(&mut self) -> Result<u64> {
        Ok(self.reader.stream_position()?)
    }

    /// Close the file
    pub fn close(self) -> Result<()> {
        Ok(())
    }
}

// Implement Read trait to allow using ReadFile with varint functions
impl Read for ReadFile {
    fn read(&mut self, buf: &mut [u8]) -> std::io::Result<usize> {
        self.reader.read(buf)
    }
}

// Implement Seek trait for compatibility
impl Seek for ReadFile {
    fn seek(&mut self, pos: SeekFrom) -> std::io::Result<u64> {
        self.reader.seek(pos)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use tempfile::NamedTempFile;

    #[test]
    fn test_write_read_file() {
        let temp_file = NamedTempFile::new().unwrap();
        let path = temp_file.path();

        // Write data
        {
            let mut writer = WriteFile::create(path).unwrap();
            writer.write(b"Hello, TsFile!").unwrap();
            assert_eq!(writer.position(), 14);
            writer.flush().unwrap();
            writer.close().unwrap();
        }

        // Read data
        {
            let mut reader = ReadFile::open(path).unwrap();
            assert_eq!(reader.size(), 14);
            let mut buf = vec![0u8; 14];
            reader.read_exact(&mut buf).unwrap();
            assert_eq!(&buf, b"Hello, TsFile!");
            reader.close().unwrap();
        }
    }
}
