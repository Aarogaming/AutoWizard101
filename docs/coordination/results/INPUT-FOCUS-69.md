# Input accuracy across focus boundaries

Status: offline source repair; not installed.

A synthetic recorded W press before focus loss incorrectly matched a prediction after focus regain when both were within timing tolerance. Reproduced as an expected-zero/actual-one match failure. Comparisons now group presses by control and focused interval. Injected-input exclusion also stays within its interval, preventing old automation events from masking later human predictions.

Three comparison tests pass, including mismatch counts across focus loss and injected exclusion isolation. Black check (one worker) and compileall passed. Restricted-environment Black stalled; identified task-owned formatter processes were stopped and checks completed in the normal user environment. No game, capture, model, desktop or installation activity.

This is offline scoring correctness, not evidence that prediction accuracy improved. Next: audit input log timestamp ordering/range validation and add bounded-log validation before processing long recordings.
