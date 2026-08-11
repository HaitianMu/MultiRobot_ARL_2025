# Final V11 Multi-Robot Evaluation Dataset

This directory is the curated data release for **Experiment II**, the zero-shot
multi-robot team-size evaluation. It contains the final V11 evaluation records,
the frozen policies and training configuration needed to identify the evaluated
system, and one separately collected scene-level trajectory example.

The single-agent RL/ARL comparison reported as Experiment I was not reproduced
in this evaluation cycle and is not included here. Historical diagnostics,
short smoke tests, superseded V12-V16 trials, Unity caches, logs, and builds were
deliberately excluded.

The data in this directory are released under the
[Creative Commons Attribution 4.0 International License](LICENSE). Project
source code outside this directory is licensed separately under the repository's
MIT License.

## Formal protocol

- Robot team sizes: 1, 2, 3, 4, and 5.
- Training team size: 3 robots.
- Frozen robot policy: `models/RobotBrain-59962.pt`.
- Frozen human policy: `models/HumanBehavior_1.29_1.onnx`.
- Independent base seeds: 204101, 204201, and 204301.
- Episodes: 100 per seed and team size (1,500 episodes in total).
- Humans: 50 per episode.
- Episode horizon: 150 s; simulation time scale: 20.
- Inference: deterministic Python ML-Agents inference.
- Layout: fixed test layout; robot count changes without retraining.
- Human policy: enabled and frozen during the robot-policy evaluation.
- `panic_state=-1`: the human policy selects its movement state; this is not a
  manually fixed panic category.
- Explicit fire objects: disabled (`use_fire=False`). CO- and temperature-related
  values remain in the archived human observation/environment path. The
  visibility interface was not active in this Experiment II control path.
- The evaluated robot system combines the frozen learned policy with the fixed
  search, coordinated-search, delivery, follower-retention, and recovery
  assistance settings recorded in every metadata file. Exit snapping is off.

The result should therefore be described as zero-shot team-size transfer of the
**frozen combined controller**, rather than as purely emergent communication-free
coordination.

## Main result

All `+/-` values below are sample standard deviations across the three seed-level
rates/scores, not episode-level standard errors.

| Robots | Escape rate (%) | Final-health score | Deaths/episode |
|---:|---:|---:|---:|
| 1 | 30.78 +/- 1.96 | 60.59 +/- 0.85 | 5.52 +/- 0.28 |
| 2 | 38.89 +/- 0.81 | 67.38 +/- 0.12 | 4.12 +/- 0.32 |
| 3 | 44.50 +/- 1.06 | 72.28 +/- 0.82 | 2.52 +/- 0.15 |
| 4 | 50.82 +/- 2.37 | 73.83 +/- 0.93 | 2.02 +/- 0.12 |
| 5 | 53.96 +/- 0.98 | 75.63 +/- 0.69 | 1.80 +/- 0.15 |

Definitions:

- `SuccessRate = 100 * total escaped / total initial humans`.
- `AvgEscapedHealthAll = escaped-health sum / total initial humans`. This is an
  initial-population-normalized score, not mean health conditional on escape.
- `AvgFinalHealthAll = (escaped + still-alive health sum) / total initial humans`.
- Aggregate error bars are seed-level sample SDs. The 95% confidence intervals
  use Student's t with `n=3`, `df=2`, and `t=4.302652729`.
- A `timeout` means the episode reached the configured 150-s horizon. It does not
  indicate a Unity or ML-Agents crash.

## Directory layout

```text
experiment_data/
  formal_evaluation/
    aggregate.csv                 # manuscript-level 3-seed statistics
    seed_results.csv              # one row per robot-count/seed batch
    episodes/                     # 15 raw files, 100 episode rows each
    summaries/                    # cumulative batch summaries; IsFinal=true is final
    metadata/                     # exact runtime flags for all 15 batches
    figures/                      # PDF plus PNG team-size result figure
  trajectory_example/
    episode.csv, summary.csv, metadata.txt
    scene_states.csv, human_events.csv
    robots/                       # three robot trajectory files
    snapshot_summary.csv
    figures/                      # PDF plus PNG scene-level timeline
  configs/                        # V11 POCA training configuration
  models/                         # exact frozen robot and human policies
  LICENSE                         # CC BY 4.0 data license
  DATA_DICTIONARY.md
  MANIFEST.csv
  verify_dataset.py
  checksums/SHA256SUMS.txt
```

The trajectory example uses three robots, seed 204195, and one episode. It was
collected with trace recording enabled and is **not** one of the formal 3 x 100
batches. It supports qualitative scene-level interpretation only.

## Software snapshot

- Unity: 2021.3.43f1c1.
- Unity ML-Agents package: 2.2.1-exp.1 (release 19 source package).
- Conda environment name: `EvacARL`.
- Python: 3.8.20.
- `mlagents` / `mlagents-envs`: 0.30.0.
- PyTorch: 1.8.2+cu111.
- NumPy: 1.21.2.

## Verification

The verifier uses only the Python standard library. From the repository root:

```powershell
conda run -n EvacARL python experiment_data/verify_dataset.py
```

It checks file counts, 1,500 raw episode rows, per-episode conservation fields,
the seed-level and aggregate calculations, formal metadata flags, the trajectory
bundle, and SHA-256 checksums.

The platform-specific Unity executable is intentionally not included in this
data-only release. Full rollout regeneration requires a compatible Unity build
of the repository plus the archived model/configuration. The published raw data
and verifier are sufficient to audit and recompute every Experiment II table and
error bar in this release.
