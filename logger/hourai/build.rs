use walkdir::WalkDir;
use std::env::VarError;
use std::ffi::OsStr;
use std::path::{Path, PathBuf};

extern crate protobuf_codegen_pure;

fn find_proto_files(root: impl AsRef<Path>) -> impl Iterator<Item=Box<Path>> {
    WalkDir::new(root.as_ref())
        .into_iter()
        .filter_map(Result::ok)
        .filter(|e| !e.file_type().is_dir() &&
            e.path().extension() == Some(OsStr::new("proto")))
        .map(|e| Box::from(e.path()))
}

fn protobuf_codegen(src_dir: &str, out_dir: &str) {
    let proto_out_path = PathBuf::from(format!("{}/proto", out_dir));
    if !proto_out_path.exists() {
        std::fs::create_dir(&proto_out_path).unwrap();
    }
    let paths = find_proto_files(src_dir).collect::<Vec<Box<Path>>>();
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
        .include(src_dir)
        .run()
        .expect("Failed to run Rust Protobuf codegen.");
    println!("Protobuf codegen complete.");
}

fn main() {
    let source_dir = match std::env::var("DOCKER_BUILD") {
        Err(VarError::NotPresent) => "../../proto/",
        _ => "../proto/",
    };
    let out_dir = std::env::var("OUT_DIR").expect("OUT_DIR must be specified");
    protobuf_codegen(source_dir, out_dir.as_str());
}
