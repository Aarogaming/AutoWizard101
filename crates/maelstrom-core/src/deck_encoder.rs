//! Deck Encoder / Decoder ported from Deimos-Wizard101 and ProjectMaelstrom's DeckEncoder.cs.
//! Encodes and decodes deck strings using delimited syntax compressed with zlib and encoded in Base64.

use std::collections::HashMap;
use std::io::{Read, Write};
use base64::engine::general_purpose::STANDARD as BASE64;
use base64::Engine;
use flate2::read::ZlibDecoder;
use flate2::write::ZlibEncoder;
use flate2::Compression;

const DELIMITER1: &str = "+";
const DELIMITER2: &str = ",";
const DELIMITER3: &str = ";";

#[derive(Debug, Clone, PartialEq, Eq, Default)]
pub struct DeckDefinition {
    pub normal: HashMap<String, u32>,
    pub tc: HashMap<String, u32>,
    pub item: HashMap<String, u32>,
}

#[derive(Debug, thiserror::Error)]
pub enum DeckError {
    #[error("Base64 decode error: {0}")]
    Base64(#[from] base64::DecodeError),
    #[error("IO/Decompression error: {0}")]
    Io(#[from] std::io::Error),
    #[error("Invalid deck format: {0}")]
    Format(String),
}

pub struct DeckEncoder;

impl DeckEncoder {
    fn serialize_section(section: &HashMap<String, u32>) -> String {
        if section.is_empty() {
            return "N".to_string();
        }

        let mut parts = Vec::new();
        // Sort keys for deterministic output
        let mut keys: Vec<_> = section.keys().collect();
        keys.sort();

        for k in keys {
            let v = section[k];
            parts.push(format!("{}{}{}", k, DELIMITER1, v));
        }

        parts.join(DELIMITER2)
    }

    fn deserialize_section(section: &str) -> HashMap<String, u32> {
        let mut dict = HashMap::new();
        let trimmed = section.trim();
        if trimmed.is_empty() || trimmed == "N" {
            return dict;
        }

        for item in trimmed.split(DELIMITER2) {
            let parts: Vec<&str> = item.split(DELIMITER1).collect();
            if parts.len() == 2 {
                if let Ok(count) = parts[1].parse::<u32>() {
                    dict.insert(parts[0].to_string(), count);
                }
            }
        }

        dict
    }

    /// Encodes normal, tc, and item deck dictionaries into a base64 zlib string.
    pub fn encode(deck: &DeckDefinition) -> Result<String, DeckError> {
        let sec_normal = Self::serialize_section(&deck.normal);
        let sec_tc = Self::serialize_section(&deck.tc);
        let sec_item = Self::serialize_section(&deck.item);

        let combined = format!("{}{}{}{}{}", sec_normal, DELIMITER3, sec_tc, DELIMITER3, sec_item);

        let mut encoder = ZlibEncoder::new(Vec::new(), Compression::default());
        encoder.write_all(combined.as_bytes())?;
        let compressed = encoder.finish()?;

        Ok(BASE64.encode(compressed))
    }

    /// Decodes a base64 zlib string into normal, tc, and item deck maps.
    pub fn decode(token: &str) -> Result<DeckDefinition, DeckError> {
        let token = token.trim();
        if token.is_empty() {
            return Err(DeckError::Format("Deck token cannot be empty".to_string()));
        }

        let compressed = BASE64.decode(token)?;
        let mut decoder = ZlibDecoder::new(&compressed[..]);
        let mut text = String::new();
        decoder.read_to_string(&mut text)?;

        let sections: Vec<&str> = text.split(DELIMITER3).collect();
        if sections.len() < 3 {
            return Err(DeckError::Format(format!(
                "Invalid deck token format; expected at least 3 sections, got {}",
                sections.len()
            )));
        }

        Ok(DeckDefinition {
            normal: Self::deserialize_section(sections[0]),
            tc: Self::deserialize_section(sections[1]),
            item: Self::deserialize_section(sections[2]),
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_deck_encode_decode_roundtrip() {
        let mut normal = HashMap::new();
        normal.insert("Tempest".to_string(), 4);
        normal.insert("Stormblade".to_string(), 2);

        let mut tc = HashMap::new();
        tc.insert("Cleanse Charm".to_string(), 1);

        let item = HashMap::new();

        let original = DeckDefinition { normal, tc, item };
        let token = DeckEncoder::encode(&original).expect("Encoding failed");
        let decoded = DeckEncoder::decode(&token).expect("Decoding failed");

        assert_eq!(original, decoded);
    }

    #[test]
    fn test_empty_deck_roundtrip() {
        let original = DeckDefinition::default();
        let token = DeckEncoder::encode(&original).expect("Encoding failed");
        let decoded = DeckEncoder::decode(&token).expect("Decoding failed");

        assert_eq!(original, decoded);
    }
}
