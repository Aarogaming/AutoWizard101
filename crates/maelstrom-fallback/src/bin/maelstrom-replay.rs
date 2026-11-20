//! Standalone offline replay verification CLI for the Maelstrom Autonomy Stack.
//! Replays recorded session traces through MaelstromAutonomyBridge without live client interaction.

use maelstrom_fallback::{AutonomyReplayHarness, AutonomyReplaySession, MaelstromAutonomyBridge};
use std::env;
use std::fs::File;
use std::io::Write;
use std::path::{Path, PathBuf};

fn print_help() {
    println!(
        r#"maelstrom-replay: Offline Autonomy Stack Session Trace Replayer

USAGE:
    maelstrom-replay <TRACE_JSON> [OPTIONS]
    maelstrom-replay --batch-dir <DIR> [OPTIONS]

ARGUMENTS:
    <TRACE_JSON>               Path to single serialized AutonomyReplaySession JSON file.

OPTIONS:
    --batch-dir <DIR>          Scan directory recursively for traces and run batch evaluation.
    --matrix-out <OUTPUT_MD>   Path to output Markdown benchmark matrix report (batch mode).
    --matrix-json <OUTPUT_JSON>Path to output raw batch metrics JSON artifact (batch mode).
    --markdown <OUTPUT_MD>     Path to output formatted Markdown statistical report (single trace).
    --json <OUTPUT_JSON>       Path to output raw metrics JSON artifact (single trace).
    --max-curvature <FLOAT>    Curvature threshold before warning (default: 5.0).
    --max-jerk <FLOAT>         Jerk threshold before warning (default: 10.0).
    --help                     Print this help message.

EXAMPLES:
    maelstrom-replay trace.json --markdown report.md --json report.json
    maelstrom-replay --batch-dir .local/quest-step-check --matrix-out matrix.md
"#
    );
}

fn main() {
    if let Err(err) = run() {
        eprintln!("Error: {}", err);
        std::process::exit(1);
    }
}

fn run() -> Result<(), Box<dyn std::error::Error>> {
    let args: Vec<String> = env::args().skip(1).collect();

    if args.is_empty() || args.iter().any(|a| a == "--help" || a == "-h") {
        print_help();
        return Ok(());
    }

    let mut batch_dir: Option<PathBuf> = None;
    let mut trace_path: Option<PathBuf> = None;
    let mut markdown_path: Option<PathBuf> = None;
    let mut json_path: Option<PathBuf> = None;
    let mut matrix_out_path: Option<PathBuf> = None;
    let mut matrix_json_path: Option<PathBuf> = None;
    let mut max_curvature = 5.0;
    let mut max_jerk = 10.0;

    let mut i = 0;
    while i < args.len() {
        match args[i].as_str() {
            "--batch-dir" => {
                i += 1;
                if i >= args.len() {
                    return Err("Missing argument for --batch-dir".into());
                }
                batch_dir = Some(PathBuf::from(&args[i]));
            }
            "--matrix-out" => {
                i += 1;
                if i >= args.len() {
                    return Err("Missing argument for --matrix-out".into());
                }
                matrix_out_path = Some(PathBuf::from(&args[i]));
            }
            "--matrix-json" => {
                i += 1;
                if i >= args.len() {
                    return Err("Missing argument for --matrix-json".into());
                }
                matrix_json_path = Some(PathBuf::from(&args[i]));
            }
            "--markdown" => {
                i += 1;
                if i >= args.len() {
                    return Err("Missing argument for --markdown".into());
                }
                markdown_path = Some(PathBuf::from(&args[i]));
            }
            "--json" => {
                i += 1;
                if i >= args.len() {
                    return Err("Missing argument for --json".into());
                }
                json_path = Some(PathBuf::from(&args[i]));
            }
            "--max-curvature" => {
                i += 1;
                if i >= args.len() {
                    return Err("Missing argument for --max-curvature".into());
                }
                max_curvature = args[i].parse::<f64>()?;
            }
            "--max-jerk" => {
                i += 1;
                if i >= args.len() {
                    return Err("Missing argument for --max-jerk".into());
                }
                max_jerk = args[i].parse::<f64>()?;
            }
            arg if !arg.starts_with("--") && trace_path.is_none() && batch_dir.is_none() => {
                trace_path = Some(PathBuf::from(arg));
            }
            unknown => {
                return Err(format!("Unknown option: {}", unknown).into());
            }
        }
        i += 1;
    }

    let harness = AutonomyReplayHarness {
        max_curvature_threshold: max_curvature,
        max_jerk_threshold: max_jerk,
    };

    if let Some(b_dir) = batch_dir {
        if !b_dir.exists() {
            return Err(format!("Batch directory not found: {}", b_dir.display()).into());
        }

        println!("Scanning for session traces in: {}", b_dir.display());
        let sessions = AutonomyReplaySession::discover_and_load_traces(&b_dir);
        if sessions.is_empty() {
            return Err(format!("No valid traces found in: {}", b_dir.display()).into());
        }
        println!(
            "Discovered {} trace sessions. Running batch evaluation...",
            sessions.len()
        );

        // Only the per-session subdirectories created under this base are removed; the base itself is left behind.
        let temp_dir = env::temp_dir().join(format!("maelstrom_batch_{}", uuid::Uuid::new_v4()));
        let batch_result = harness.run_batch(&sessions, &temp_dir);

        let md_matrix = batch_result.to_markdown_matrix();

        let out_md = matrix_out_path.or(markdown_path);
        if let Some(ref md_p) = out_md {
            write_file_safely(md_p, md_matrix.as_bytes())?;
            println!("Benchmark matrix saved to: {}", md_p.display());
        }

        let out_json = matrix_json_path.or(json_path);
        if let Some(ref json_p) = out_json {
            let json_val = batch_result.to_json_value();
            let json_str = serde_json::to_string_pretty(&json_val)?;
            write_file_safely(json_p, json_str.as_bytes())?;
            println!("Benchmark matrix JSON saved to: {}", json_p.display());
        }

        if out_md.is_none() && out_json.is_none() {
            println!("{}", md_matrix);
        }

        if batch_result.failed_sessions > 0 {
            eprintln!(
                "Batch Quality Gate FAILED: {}/{} sessions failed acceptance criteria",
                batch_result.failed_sessions, batch_result.total_sessions
            );
            std::process::exit(2);
        }

        return Ok(());
    }

    let trace_path = trace_path.ok_or("No trace file or --batch-dir specified")?;
    if !trace_path.exists() {
        return Err(format!("Trace file not found: {}", trace_path.display()).into());
    }

    // Load session trace
    let session = AutonomyReplaySession::from_file(&trace_path)?;

    // Construct pure offline bridge
    let temp_dir = env::temp_dir().join(format!("maelstrom_replay_{}", session.session_id));
    let mut bridge = MaelstromAutonomyBridge::new(&temp_dir)?;

    let (metrics, _results) = harness.replay_session(&session, &mut bridge);

    let duration_ms = session.duration_ms();
    let md_report = metrics.to_markdown(&session.session_id, duration_ms);

    // Write Markdown report if requested
    if let Some(ref md_p) = markdown_path {
        write_file_safely(md_p, md_report.as_bytes())?;
        println!("Markdown report saved to: {}", md_p.display());
    }

    // Write JSON artifact if requested
    if let Some(ref json_p) = json_path {
        let json_data = serde_json::to_string_pretty(&metrics)?;
        write_file_safely(json_p, json_data.as_bytes())?;
        println!("JSON metrics artifact saved to: {}", json_p.display());
    }

    // If no output file was specified, print Markdown report to stdout
    if markdown_path.is_none() && json_path.is_none() {
        println!("{}", md_report);
    }

    // Fail quality gate if accuracy is below 95.0%. This is the only gate: the report's own 99.5% badge, curvature,
    // jerk and warnings do not affect the exit code, and exit code 1 is reserved for input and I/O errors.
    if metrics.decision_accuracy_pct < 95.0 {
        eprintln!(
            "Quality Gate FAILED: Decision accuracy {:.2}% < 95.0%",
            metrics.decision_accuracy_pct
        );
        std::process::exit(2);
    }

    Ok(())
}

/// Despite the name, this truncates and overwrites an existing file without a message, creates parent directories,
/// and is not atomic (no temporary file and rename).
fn write_file_safely(path: &Path, content: &[u8]) -> std::io::Result<()> {
    if let Some(parent) = path.parent() {
        std::fs::create_dir_all(parent)?;
    }
    let mut file = File::create(path)?;
    file.write_all(content)?;
    Ok(())
}
