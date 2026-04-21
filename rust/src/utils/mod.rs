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

//! Utility functions

pub mod varint;

pub use varint::{
    read_var_int, read_var_string, read_var_uint, write_var_int, write_var_string, write_var_uint,
};

/// Convert boolean to string
pub fn bool_to_string(value: bool) -> String {
    value.to_string()
}

/// Convert option to string
pub fn option_to_string<T: std::fmt::Display>(value: &Option<T>) -> String {
    match value {
        Some(v) => v.to_string(),
        None => "None".to_string(),
    }
}
