use std::fs;
use std::io::Write;
use std::path::Path;

fn main() {
    println!("cargo:rerun-if-changed=build.rs");

    let out_dir = std::env::var("OUT_DIR").unwrap();
    let icon_path = Path::new(&out_dir).join("app.ico");
    let rc_path = Path::new(&out_dir).join("app.rc");

    // Generate a simple 32x32 blue speaker icon
    let icon_data = generate_icon(32);
    fs::write(&icon_path, icon_data).unwrap();

    // Create .rc file - use forward slashes for rc.exe compatibility
    let icon_str = icon_path.to_string_lossy().replace('\\', "/");
    let rc_content = format!(r#"MAINICON ICON "{}""#, icon_str);
    fs::write(&rc_path, rc_content).unwrap();

    // Compile the resource file
    embed_resource::compile(&rc_path, embed_resource::NONE);
}

fn append_bytes(data: &mut Vec<u8>, bytes: &[u8]) {
    data.extend_from_slice(bytes);
}

fn generate_icon(size: u8) -> Vec<u8> {
    let width = size;
    let height = size;
    let pixel_count = width as usize * height as usize;

    // Generate 32-bit BGRA pixel data (ICO format uses BGRA)
    let mut bgra = Vec::with_capacity(pixel_count * 4);
    let center = width as f32 / 2.0;

    for y in 0..height {
        for x in 0..width {
            let dx = x as f32 - center + 0.5;
            let dy = y as f32 - center + 0.5;
            let dist = (dx * dx + dy * dy).sqrt();

            if dist < 12.0 {
                // Blue circle
                bgra.push(230); // B
                bgra.push(130); // G
                bgra.push(70);  // R
                bgra.push(255); // A
            } else if dist < 14.0 {
                // Light blue border
                bgra.push(255);
                bgra.push(160);
                bgra.push(100);
                bgra.push(255);
            } else {
                // Transparent
                bgra.push(0);
                bgra.push(0);
                bgra.push(0);
                bgra.push(0);
            }
        }
    }

    // AND mask (1 bit per pixel, row padded to 32-bit boundary)
    let row_size = (width as usize + 31) / 32 * 4;
    let and_mask_size = row_size * height as usize;
    let and_mask = vec![0u8; and_mask_size];

    // Build ICO file
    let mut data = Vec::new();

    // ICONDIR (6 bytes)
    append_bytes(&mut data, &[0u8, 0]); // Reserved
    append_bytes(&mut data, &[1u8, 0]); // Type (1 = icon)
    append_bytes(&mut data, &[1u8, 0]); // Count (1 image)

    // ICONDIRENTRY (16 bytes)
    append_bytes(&mut data, &[width]);
    append_bytes(&mut data, &[height]);
    append_bytes(&mut data, &[0]); // Color palette (0 = no palette)
    append_bytes(&mut data, &[0]); // Reserved
    append_bytes(&mut data, &[1u8, 0]); // Color planes
    append_bytes(&mut data, &[32u8, 0]); // Bits per pixel
    let image_size = (pixel_count * 4 + and_mask_size) as u32;
    append_bytes(&mut data, &image_size.to_le_bytes());
    let image_offset = 6 + 16; // ICONDIR + ICONDIRENTRY
    append_bytes(&mut data, &(image_offset as u32).to_le_bytes());

    // BITMAPINFOHEADER (40 bytes)
    append_bytes(&mut data, &40u32.to_le_bytes()); // Header size
    append_bytes(&mut data, &((width as i32) * 2).to_le_bytes()); // Width (doubled for XOR+AND)
    append_bytes(&mut data, &(height as i32).to_le_bytes()); // Height
    append_bytes(&mut data, &1u16.to_le_bytes()); // Planes
    append_bytes(&mut data, &32u16.to_le_bytes()); // Bit count
    append_bytes(&mut data, &0u32.to_le_bytes()); // Compression
    append_bytes(&mut data, &image_size.to_le_bytes()); // Image size
    append_bytes(&mut data, &0i32.to_le_bytes()); // X pixels per meter
    append_bytes(&mut data, &0i32.to_le_bytes()); // Y pixels per meter
    append_bytes(&mut data, &0u32.to_le_bytes()); // Colors used
    append_bytes(&mut data, &0u32.to_le_bytes()); // Important colors

    // XOR mask (BGRA)
    append_bytes(&mut data, &bgra);

    // AND mask
    append_bytes(&mut data, &and_mask);

    data
}
