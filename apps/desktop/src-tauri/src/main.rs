#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use tauri_plugin_shell::{process::CommandEvent, ShellExt};

fn main() {
    tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .setup(|app| {
            let sidecar_command = app.shell().sidecar("dumptether-api")?.args([
                "--urls=http://127.0.0.1:55868",
                "--Database:Provider=Sqlite",
                "--Database:ApplyMigrationsOnStartup=true",
                "--Auth:RequireAuthentication=true",
                "--Auth:AllowGuestSessions=true",
                "--Auth:EnableDevelopmentLogin=false",
                "--EmailConfirmation:Enabled=false",
                "--Email:Smtp:Enabled=false",
                "--Email:BrevoApi:Enabled=false",
                "--Mfa:Email:Enabled=false",
                "--OAuth:Google:Enabled=false",
                "--OAuth:Microsoft:Enabled=false",
                "--OAuth:Facebook:Enabled=false",
                "--Cors:AllowedOrigins:0=http://tauri.localhost",
                "--Cors:AllowedOrigins:1=http://localhost:5173",
                "--Cors:AllowedOrigins:2=http://127.0.0.1:5173",
            ]);
            let (mut receiver, mut child) = sidecar_command.spawn()?;

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
