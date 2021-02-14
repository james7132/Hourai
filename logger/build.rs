use walkdir::WalkDir;
use std::ffi::OsStr;
use std::path::{Path, PathBuf};

extern crate protobuf_codegen_pure;

fn find_proto_files(root: impl AsRef<Path>) -> impl Iterator<Item=Box<Path>> {
    return WalkDir::new(root.as_ref())
            .into_iter()
            .filter_map(Result::ok)
            .filter(|e| !e.file_type().is_dir() &&
                         e.path().extension() == Some(OsStr::new("proto")))
            .map(|e| Box::from(e.path()));
}

fn protobuf_codegen(out_dir: &String) {
    let proto_out_path = PathBuf::from(out_dir.clone() + "/proto");
    if !proto_out_path.exists() {
        std::fs::create_dir(&proto_out_path).unwrap();
    }
    let paths = find_proto_files("src/").collect::<Vec<Box<Path>>>();
    println!("Running protobuf codegen...");
    for path in &paths {
        println!("Input Proto File: {:?}", path);
    }
    protobuf_codegen_pure::Codegen::new()
        .customize(protobuf_codegen_pure::Customize {
            gen_mod_rs: Some(true),
            ..Default::default()
        })
        .out_dir(&proto_out_path)
        .inputs(&paths[..])
        .include("src/")
        .run()
        .expect("Failed to run Rust Protobuf codegen.");
    println!("Protobuf codegen complete.");
}

fn main() {
    let out_dir = std::env::var("OUT_DIR").expect("OUT_DIR must be specified");
    protobuf_codegen(&out_dir);
}
