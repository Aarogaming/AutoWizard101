//! Read-only diagnostic client. Never starts a host or connects it to a game.
use maelstrom_fallback::maelstrom_bridge::{
    call_stream_bounded, compute_pipe_name, DEFAULT_TIMEOUT,
};
use std::fs::OpenOptions;
use std::io::Write;
use std::sync::{atomic::AtomicBool, Arc};

fn main() {
    if let Err(error) = run() {
        eprintln!("Bridge check failed: {error}");
        std::process::exit(1);
    }
}

fn run() -> Result<(), Box<dyn std::error::Error>> {
    let args: Vec<String> = std::env::args().skip(1).collect();
    if args == ["--help"] {
        println!("maelstrom-bridge <data-root> <capabilities|catalog|state|snapshot> <new-report.json>\nRead-only; no host startup or game interaction.");
        return Ok(());
    }
    validate_args(&args)?;
    let pipe = compute_pipe_name(&args[0])?;
    let mut report = OpenOptions::new()
        .create_new(true)
        .write(true)
        .open(&args[2])?;
    #[cfg(windows)]
    {
        let runtime = tokio::runtime::Builder::new_current_thread()
            .enable_all()
            .build()?;
        let result = runtime.block_on(async {
            let mut stream = maelstrom_fallback::maelstrom_bridge::connect_pipe(
                &pipe,
                Arc::new(AtomicBool::new(false)),
            )
            .await?;
            call_stream_bounded(
                &mut stream,
                &args[1],
                DEFAULT_TIMEOUT,
                Arc::new(AtomicBool::new(false)),
            )
            .await
        });
        match result {
            Ok(reply) => {
                let mut envelope = serde_json::to_value(&reply)?;
                if args[1] == "snapshot" {
                    let state =
                        maelstrom_fallback::maelstrom_bridge::NativeHostState::from_snapshot_json(
                            reply.result.as_ref().unwrap_or(&serde_json::Value::Null),
                        );
                    let mut projection = serde_json::to_value(state)?;
                    if let Some(object) = projection.as_object_mut() {
                        object.remove("raw_snapshot");
                    }
                    envelope["projection"] = projection;
                }
                serde_json::to_writer_pretty(&mut report, &envelope)?;
                report.write_all(b"\n")?;
                println!("Read-only {} check passed; report saved.", args[1]);
                Ok(())
            }
            Err(error) => {
                serde_json::to_writer_pretty(
                    &mut report,
                    &serde_json::json!({"ok":false,"error":error.to_string()}),
                )?;
                Err(error.into())
            }
        }
    }
    #[cfg(not(windows))]
    {
        let _ = (pipe, report);
        Err("Named pipes require Windows".into())
    }
}

fn validate_args(args: &[String]) -> Result<(), &'static str> {
    if args.len() != 3
        || !matches!(
            args[1].as_str(),
            "capabilities" | "catalog" | "state" | "snapshot"
        )
    {
        return Err("Use --help for the read-only command syntax");
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::validate_args;
    #[test]
    fn cli_rejects_mutating_commands_and_missing_arguments() {
        assert!(validate_args(&[]).is_err());
        for command in ["start", "connect", "observe", "step", "deal", "stop"] {
            assert!(validate_args(&["root".into(), command.into(), "report".into()]).is_err());
        }
        assert!(validate_args(&["root".into(), "snapshot".into(), "report".into()]).is_ok());
    }
}
