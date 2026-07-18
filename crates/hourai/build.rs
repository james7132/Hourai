#![allow(clippy::unwrap_used, clippy::expect_used)]

use std::{env, path::PathBuf};
use walkdir::WalkDir;

fn main() {
    let out_dir = env::var("OUT_DIR").expect("OUT_DIR must be specified");
    let proto_out_path = PathBuf::from(out_dir).join("proto");
    if !proto_out_path.exists() {
        std::fs::create_dir(&proto_out_path).unwrap();
    }

    let mut protos = Vec::new();
    for entry in WalkDir::new("../../proto")
        .into_iter()
        .filter_map(|e| e.ok())
    {
        let path = entry.path();
        if path.is_file() && path.extension().is_some_and(|ext| ext == "proto") {
            protos.push(path.to_str().unwrap().to_string());
        }
    }

    protobuf_codegen_pure::Codegen::new()
        .customize(protobuf_codegen_pure::Customize {
            serde_derive: Some(true),
            gen_mod_rs: Some(true),
            ..Default::default()
        })
        .out_dir(proto_out_path.to_str().unwrap())
        .inputs(&protos)
        .include("../../proto")
        .run()
        .expect("Failed to run Rust Protobuf codegen.");

    println!("cargo:rerun-if-changed=../../proto");
}
