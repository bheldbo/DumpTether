#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use tauri_plugin_shell::{process::CommandEvent, ShellExt};

fn main() {
    tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .setup(|app| {
            let sidecar_command = app
                .shell()
                .sidecar("dumptether-api")?
                .args(["--environment=Desktop"]);
            let (mut receiver, child) = sidecar_command.spawn()?;

            tauri::async_runtime::spawn(async move {
                while let Some(event) = receiver.recv().await {
                    match event {
                        CommandEvent::Stdout(line) => {
                            let line = String::from_utf8_lossy(&line);
                            println!("[DumpTether.Api] {line}");
                        }
                        CommandEvent::Stderr(line) => {
                            let line = String::from_utf8_lossy(&line);
                            eprintln!("[DumpTether.Api] {line}");
                        }
                        _ => {}
                    }
                }

                let _ = child.kill();
            });

            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("error while running DumpTether desktop");
}
