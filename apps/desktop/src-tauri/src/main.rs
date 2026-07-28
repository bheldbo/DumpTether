#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use std::{
    net::TcpListener,
    sync::Mutex,
};
use tauri::{
    Manager, RunEvent, WebviewUrl, WebviewWindowBuilder,
};
use tauri_plugin_shell::{
    process::{CommandChild, CommandEvent},
    ShellExt,
};

struct SidecarProcess(Mutex<Option<CommandChild>>);

struct DesktopRuntime {
    api_base_url: String,
    bootstrap_token: String,
    cors_origin: &'static str,
}

fn main() {
    let app = tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .setup(|app| {
            let runtime = create_desktop_runtime()?;
            let resource_dir = app.path().resource_dir()?;
            let sidecar_args = build_sidecar_args(&runtime);
            let sidecar_command = app
                .shell()
                .sidecar("dumptether-api")?
                .args(sidecar_args)
                .current_dir(resource_dir);
            let (mut receiver, child) = sidecar_command.spawn()?;
            app.manage(SidecarProcess(Mutex::new(Some(child))));

            let initialization_script = format!(
                "Object.defineProperty(window, '__DUMPTETHER_DESKTOP_RUNTIME__', \
                 {{ value: Object.freeze({{ apiBaseUrl: '{}', bootstrapToken: '{}' }}), \
                 writable: false, configurable: false }});",
                runtime.api_base_url,
                runtime.bootstrap_token,
            );

            WebviewWindowBuilder::new(
                app,
                "main",
                WebviewUrl::App("index.html".into()),
            )
            .title("DumpTether")
            .inner_size(1440.0, 900.0)
            .min_inner_size(380.0, 640.0)
            .resizable(true)
            .initialization_script(initialization_script)
            .build()?;

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
            });

            Ok(())
        })
        .build(tauri::generate_context!())
        .expect("error while building DumpTether desktop");

    app.run(|app_handle, event| {
        if let RunEvent::Exit = event {
            let state = app_handle.state::<SidecarProcess>();
            if let Ok(mut child) = state.0.lock() {
                if let Some(child) = child.take() {
                    let _ = child.kill();
                }
            };
        }
    });
}

fn create_desktop_runtime() -> Result<DesktopRuntime, Box<dyn std::error::Error>> {
    let listener = TcpListener::bind("127.0.0.1:0")?;
    let port = listener.local_addr()?.port();
    drop(listener);

    let mut token_bytes = [0_u8; 32];
    getrandom::fill(&mut token_bytes)
        .map_err(|error| std::io::Error::other(error.to_string()))?;
    let bootstrap_token = token_bytes
        .iter()
        .map(|byte| format!("{byte:02x}"))
        .collect::<String>();

    Ok(DesktopRuntime {
        api_base_url: format!("http://127.0.0.1:{port}"),
        bootstrap_token,
        cors_origin: desktop_web_origin(),
    })
}

fn build_sidecar_args(runtime: &DesktopRuntime) -> Vec<String> {
    [
        "--environment",
        "Desktop",
        "--urls",
        runtime.api_base_url.as_str(),
        "--Desktop:BootstrapToken",
        runtime.bootstrap_token.as_str(),
        "--Database:Provider",
        "Sqlite",
        "--Database:ApplyMigrationsOnStartup",
        "true",
        "--Auth:RequireAuthentication",
        "true",
        "--Auth:AllowGuestSessions",
        "false",
        "--Auth:SignupMode",
        "Closed",
        "--Auth:EnableDevelopmentLogin",
        "false",
        "--Auth:EnableLocalDesktopLogin",
        "true",
        "--EmailConfirmation:Enabled",
        "false",
        "--Email:Provider",
        "None",
        "--Mfa:Email:Enabled",
        "false",
        "--OAuth:Microsoft:Enabled",
        "false",
        "--Cors:AllowedOrigins:0",
        runtime.cors_origin,
    ]
    .into_iter()
    .map(String::from)
    .collect()
}

#[cfg(debug_assertions)]
fn desktop_web_origin() -> &'static str {
    "http://localhost:5173"
}

#[cfg(not(debug_assertions))]
fn desktop_web_origin() -> &'static str {
    "http://tauri.localhost"
}
