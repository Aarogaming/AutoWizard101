//! Stub PlayerController – in the original C# this drives mouse clicks and key presses.
//! Here we merely log actions; a real implementation could use the `windows` crate to call WinAPI.

use super::combat_geometry::Point;

#[derive(Debug, Default)]
pub struct PlayerController;

impl PlayerController {
    pub fn new() -> Self {
        Self {}
    }

    pub fn click(&self, point: Point) {
        println!("[PlayerController] Click at ({}, {})", point.x, point.y);
    }

    pub fn press_number9(&self) {
        println!("[PlayerController] Press number 9 key");
    }
}
