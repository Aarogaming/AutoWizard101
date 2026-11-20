//! Deck Encoder / Decoder ported from Deimos-Wizard101 and ProjectMaelstrom's DeckEncoder.cs.
//! Encodes and decodes deck strings using delimited syntax compressed with zlib and encoded in Base64.

use base64::engine::general_purpose::STANDARD as BASE64;
use base64::Engine;
use flate2::read::ZlibDecoder;
use flate2::write::ZlibEncoder;
use flate2::Compression;
use std::collections::HashMap;
use std::io::{Read, Write};

// Literal backslashes are part of the native/Deimos wire format, not regex escapes.
const DELIMITER1: &str = r"\+";
const DELIMITER2: &str = r"\,";
const DELIMITER3: &str = r"\;";

#[derive(Debug, Clone, PartialEq, Eq, Default)]
pub struct DeckDefinition {
    pub normal: HashMap<String, i32>,
    pub tc: HashMap<String, i32>,
    pub item: HashMap<String, i32>,
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
    fn serialize_section(section: &HashMap<String, i32>) -> String {
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

    fn deserialize_section(section: &str) -> HashMap<String, i32> {
        let mut dict: HashMap<String, i32> = HashMap::new();
        if section.trim().is_empty() || section == "N" {
            return dict;
        }

        for item in section.split(DELIMITER2).filter(|item| !item.is_empty()) {
            let parts: Vec<&str> = item.split(DELIMITER1).collect();
            if parts.len() == 2 {
                if let Ok(count) = parts[1].trim().parse::<i32>() {
                    // Native decoding uses OrdinalIgnoreCase and retains the first spelling.
                    // ASCII names match that contract; non-ASCII case folding remains unqualified.
                    let key = dict
                        .keys()
                        .find(|key| key.eq_ignore_ascii_case(parts[0]))
                        .cloned()
                        .unwrap_or_else(|| parts[0].to_string());
                    dict.insert(key, count);
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

        let combined = format!(
            "{}{}{}{}{}",
            sec_normal, DELIMITER3, sec_tc, DELIMITER3, sec_item
        );

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

        // Convert.FromBase64String accepts embedded ASCII whitespace.
        let compact: String = token
            .chars()
            .filter(|c| !matches!(c, ' ' | '\t' | '\r' | '\n'))
            .collect();
        let compressed = BASE64.decode(compact)?;
        let mut decoder = ZlibDecoder::new(&compressed[..]);
        let mut bytes = Vec::new();
        decoder.read_to_end(&mut bytes)?;
        let text = String::from_utf8_lossy(&bytes);

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
    fn decodes_native_dotnet_zlib_fixture() {
        // Generated independently with .NET ZLibStream/Convert.ToBase64String from
        // the literal native wire text, not with this encoder.
        let token = "eJwLSc0tSC0uidE2idEJLskvyk3KSUxJjdE2irF2zklNzCtOVXDOSCzKjdE2jLH2AwCCWQ/m";
        let deck = DeckEncoder::decode(token).unwrap();
        assert_eq!(
            deck.normal,
            HashMap::from([("Tempest".into(), 4), ("Stormblade".into(), 2)])
        );
        assert_eq!(deck.tc, HashMap::from([("Cleanse Charm".into(), 1)]));
        assert!(deck.item.is_empty());
        assert_eq!(
            DeckEncoder::decode(&format!(" \r\n{}\t{} ", &token[..20], &token[20..])).unwrap(),
            deck
        );
    }

    #[test]
    fn encoder_emits_native_literal_delimiters_and_signed_counts() {
        let deck = DeckDefinition {
            normal: HashMap::from([("Tempest".into(), -1), ("Stormblade".into(), 2)]),
            tc: HashMap::new(),
            item: HashMap::new(),
        };
        let bytes = BASE64.decode(DeckEncoder::encode(&deck).unwrap()).unwrap();
        let mut text = String::new();
        ZlibDecoder::new(bytes.as_slice())
            .read_to_string(&mut text)
            .unwrap();
        assert_eq!(text, r"Stormblade\+2\,Tempest\+-1\;N\;N");
    }

    #[test]
    fn native_section_semantics_preserve_names_and_last_case_insensitive_value() {
        let values = DeckEncoder::deserialize_section(
            r"Tempest\+4\,TEMPEST\+ +2 \,,literal+name;\+-1\,bad\,overflow\+2147483648\, spaced \+0",
        );
        assert_eq!(
            values,
            HashMap::from([
                ("Tempest".into(), 2),
                (",literal+name;".into(), -1),
                (" spaced ".into(), 0)
            ])
        );
        assert!(DeckEncoder::deserialize_section("N").is_empty());
        assert!(DeckEncoder::decode(" ").is_err());
        assert!(DeckEncoder::decode("not base64!").is_err());
    }

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
