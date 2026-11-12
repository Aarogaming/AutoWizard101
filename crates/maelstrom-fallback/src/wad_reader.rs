//! KingsIsle WAD (.wad) archive reader ported from libkiwad, wizwad, and ProjectMaelstrom's WadArchiveReader.cs.

use std::collections::HashMap;
use std::fs::File;
use std::io::{Cursor, Read, Seek, SeekFrom};
use std::path::Path;
use flate2::read::ZlibDecoder;

const KIWAD_MAGIC: &[u8; 5] = b"KIWAD";

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct WadEntryInfo {
    pub path: String,
    pub offset: u32,
    pub size: u32,
    pub compressed_size: u32,
    pub is_compressed: bool,
    pub checksum: u32,
}

#[derive(Debug, thiserror::Error)]
pub enum WadError {
    #[error("IO error: {0}")]
    Io(#[from] std::io::Error),
    #[error("Invalid magic: expected 'KIWAD', found {0:?}")]
    InvalidMagic(Vec<u8>),
    #[error("Entry not found: {0}")]
    EntryNotFound(String),
}

pub struct WadArchiveReader {
    file: File,
    pub version: u32,
    pub entry_count: u32,
    pub entries: HashMap<String, WadEntryInfo>,
}

impl WadArchiveReader {
    pub fn open<P: AsRef<Path>>(path: P) -> Result<Self, WadError> {
        let mut file = File::open(path)?;
        let mut magic = [0u8; 5];
        file.read_exact(&mut magic)?;

        if &magic != KIWAD_MAGIC {
            return Err(WadError::InvalidMagic(magic.to_vec()));
        }

        let mut buf4 = [0u8; 4];
        file.read_exact(&mut buf4)?;
        let version = u32::from_le_bytes(buf4);

        file.read_exact(&mut buf4)?;
        let entry_count = u32::from_le_bytes(buf4);

        if version >= 2 {
            let mut buf1 = [0u8; 1];
            file.read_exact(&mut buf1)?;
        }

        let mut entries = HashMap::with_capacity(entry_count as usize);

        for _ in 0..entry_count {
            file.read_exact(&mut buf4)?;
            let offset = u32::from_le_bytes(buf4);

            file.read_exact(&mut buf4)?;
            let size = u32::from_le_bytes(buf4);

            file.read_exact(&mut buf4)?;
            let compressed_size = u32::from_le_bytes(buf4);

            let mut bool_buf = [0u8; 1];
            file.read_exact(&mut bool_buf)?;
            let is_compressed = bool_buf[0] != 0;

            file.read_exact(&mut buf4)?;
            let checksum = u32::from_le_bytes(buf4);

            file.read_exact(&mut buf4)?;
            let path_len = u32::from_le_bytes(buf4) as usize;

            let mut path_bytes = vec![0u8; path_len];
            file.read_exact(&mut path_bytes)?;

            let entry_path = String::from_utf8_lossy(&path_bytes)
                .trim_end_matches('\0')
                .to_string();

            let entry = WadEntryInfo {
                path: entry_path.clone(),
                offset,
                size,
                compressed_size,
                is_compressed,
                checksum,
            };

            entries.insert(entry_path.to_ascii_lowercase(), entry);
        }

        Ok(Self {
            file,
            version,
            entry_count,
            entries,
        })
    }

    /// Extract entry data by name (case-insensitive)
    pub fn extract(&mut self, entry_name: &str) -> Result<Vec<u8>, WadError> {
        let entry = self
            .entries
            .get(&entry_name.to_ascii_lowercase())
            .cloned()
            .ok_or_else(|| WadError::EntryNotFound(entry_name.to_string()))?;

        self.extract_entry(&entry)
    }

    /// Reads and decompresses (if required) an entry's raw data bytes.
    pub fn extract_entry(&mut self, entry: &WadEntryInfo) -> Result<Vec<u8>, WadError> {
        self.file.seek(SeekFrom::Start(entry.offset as u64))?;

        let read_size = if entry.is_compressed {
            entry.compressed_size as usize
        } else {
            entry.size as usize
        };

        let mut data = vec![0u8; read_size];
        self.file.read_exact(&mut data)?;

        if !entry.is_compressed {
            return Ok(data);
        }

        let mut decoder = ZlibDecoder::new(Cursor::new(data));
        let mut decompressed = Vec::with_capacity(entry.size as usize);
        decoder.read_to_end(&mut decompressed)?;

        Ok(decompressed)
    }
}
