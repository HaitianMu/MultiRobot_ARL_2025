from __future__ import annotations

import csv
import hashlib
import math
import re
import statistics
from pathlib import Path


ROOT = Path(__file__).resolve().parent
FORMAL = ROOT / "formal_evaluation"
T_CRITICAL_DF2_95 = 4.302652729
TOLERANCE = 0.0011


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open(newline="", encoding="utf-8-sig") as stream:
        return list(csv.DictReader(stream))


def close(actual: float, expected: float, label: str) -> None:
    if not math.isclose(actual, expected, rel_tol=0.0, abs_tol=TOLERANCE):
        raise AssertionError(f"{label}: expected {expected}, got {actual}")


def sample_stats(values: list[float]) -> tuple[float, float, float, float]:
    mean = statistics.fmean(values)
    sd = statistics.stdev(values)
    margin = T_CRITICAL_DF2_95 * sd / math.sqrt(len(values))
    return mean, sd, mean - margin, mean + margin


def validate_episode_files() -> dict[tuple[int, int], dict[str, float]]:
    paths = sorted((FORMAL / "episodes").glob("*.csv"))
    if len(paths) != 15:
        raise AssertionError(f"Expected 15 formal episode files, found {len(paths)}")

    filename_re = re.compile(r"_r(\d+)_seed(\d+)\.csv$")
    computed: dict[tuple[int, int], dict[str, float]] = {}
    total_rows = 0

    for path in paths:
        match = filename_re.search(path.name)
        if match is None:
            raise AssertionError(f"Cannot parse condition from {path.name}")
        robot_count, base_seed = map(int, match.groups())
        rows = read_csv(path)
        if len(rows) != 100:
            raise AssertionError(f"{path.name}: expected 100 rows, found {len(rows)}")
        total_rows += len(rows)

        for index, row in enumerate(rows, start=1):
            initial = int(row["InitialHumans"])
            escaped = int(row["Escaped"])
            deaths = int(row["Deaths"])
            alive = int(row["AliveAtEnd"])
            if int(row["Episode"]) != index:
                raise AssertionError(f"{path.name}: unexpected episode index at row {index}")
            if int(row["RobotCount"]) != robot_count or initial != 50:
                raise AssertionError(f"{path.name}: inconsistent fixed condition at row {index}")
            if int(row["Seed"]) != base_seed + index - 1:
                raise AssertionError(f"{path.name}: unexpected episode seed at row {index}")
            if escaped + deaths + alive != initial:
                raise AssertionError(f"{path.name}: population accounting failed at row {index}")
            final_health = float(row["FinalSurvivorHealthSum"])
            close(
                final_health,
                float(row["EscapedHealthSum"]) + float(row["AliveHealthSum"]),
                f"{path.name} row {index} final health",
            )
            close(float(row["SuccessRate"]), 100.0 * escaped / initial, f"{path.name} row {index} success")
            close(
                float(row["AvgEscapedHealthAll"]),
                float(row["EscapedHealthSum"]) / initial,
                f"{path.name} row {index} escaped health",
            )
            close(
                float(row["AvgFinalHealthAll"]),
                final_health / initial,
                f"{path.name} row {index} final health score",
            )

        initial_total = sum(int(row["InitialHumans"]) for row in rows)
        escaped_total = sum(int(row["Escaped"]) for row in rows)
        deaths_total = sum(int(row["Deaths"]) for row in rows)
        computed[(robot_count, base_seed)] = {
            "Episodes": float(len(rows)),
            "InitialHumans": float(initial_total),
            "Escaped": float(escaped_total),
            "Deaths": float(deaths_total),
            "SuccessRate": 100.0 * escaped_total / initial_total,
            "AvgEpisodeTime": statistics.fmean(float(row["EpisodeTime"]) for row in rows),
            "AvgEscapedHealthAll": sum(float(row["EscapedHealthSum"]) for row in rows) / initial_total,
            "AvgFinalHealthAll": sum(float(row["FinalSurvivorHealthSum"]) for row in rows) / initial_total,
            "AvgDeathsPerEpisode": deaths_total / len(rows),
            "TimeoutRate": 100.0 * sum(row["Termination"].lower() == "timeout" for row in rows) / len(rows),
        }

    if total_rows != 1500:
        raise AssertionError(f"Expected 1,500 formal episode rows, found {total_rows}")
    return computed


def validate_seed_results(computed: dict[tuple[int, int], dict[str, float]]) -> dict[tuple[int, int], dict[str, float]]:
    rows = read_csv(FORMAL / "seed_results.csv")
    if len(rows) != 15:
        raise AssertionError(f"Expected 15 seed-level rows, found {len(rows)}")

    published: dict[tuple[int, int], dict[str, float]] = {}
    fields = [
        "Episodes",
        "InitialHumans",
        "Escaped",
        "Deaths",
        "SuccessRate",
        "AvgEpisodeTime",
        "AvgEscapedHealthAll",
        "AvgFinalHealthAll",
        "AvgDeathsPerEpisode",
        "TimeoutRate",
    ]
    for row in rows:
        key = (int(row["RobotCount"]), int(row["Seed"]))
        if key not in computed:
            raise AssertionError(f"Unexpected seed-level condition {key}")
        published[key] = {field: float(row[field]) for field in fields}
        for field in fields:
            close(published[key][field], computed[key][field], f"seed {key} {field}")
    return published


def validate_summaries(published: dict[tuple[int, int], dict[str, float]]) -> None:
    paths = sorted((FORMAL / "summaries").glob("*.csv"))
    if len(paths) != 15:
        raise AssertionError(f"Expected 15 summary files, found {len(paths)}")
    filename_re = re.compile(r"_r(\d+)_seed(\d+)\.csv$")
    for path in paths:
        match = filename_re.search(path.name)
        if match is None:
            raise AssertionError(f"Cannot parse condition from {path.name}")
        key = tuple(map(int, match.groups()))
        final_rows = [row for row in read_csv(path) if row["IsFinal"].lower() == "true"]
        if len(final_rows) != 1:
            raise AssertionError(f"{path.name}: expected one final row")
        row = final_rows[0]
        for field in (
            "TotalEpisodes",
            "InitialHumans",
            "Escaped",
            "Deaths",
            "SuccessRate",
            "AvgEpisodeTime",
            "AvgEscapedHealthAll",
            "AvgFinalHealthAll",
            "AvgDeathsPerEpisode",
        ):
            seed_field = "Episodes" if field == "TotalEpisodes" else field
            close(float(row[field]), published[key][seed_field], f"summary {key} {field}")


def validate_aggregate(published: dict[tuple[int, int], dict[str, float]]) -> None:
    aggregate_rows = read_csv(FORMAL / "aggregate.csv")
    if len(aggregate_rows) != 5:
        raise AssertionError(f"Expected 5 aggregate rows, found {len(aggregate_rows)}")

    metric_map = {
        "SuccessRate": "SuccessRate",
        "AvgHealth": "AvgEscapedHealthAll",
        "AvgFinalHealth": "AvgFinalHealthAll",
        "DeathsPerEpisode": "AvgDeathsPerEpisode",
        "TimeoutRate": "TimeoutRate",
        "AvgEpisodeTime": "AvgEpisodeTime",
    }
    previous_success: dict[int, float] | None = None
    for row in aggregate_rows:
        robot_count = int(row["RobotCount"])
        seed_rows = {seed: published[(robot_count, seed)] for seed in (204101, 204201, 204301)}
        if int(row["Seeds"]) != 3 or int(row["Episodes"]) != 300:
            raise AssertionError(f"Robot count {robot_count}: invalid aggregate sample size")
        for prefix, seed_field in metric_map.items():
            values = [seed_rows[seed][seed_field] for seed in sorted(seed_rows)]
            expected = sample_stats(values)
            for suffix, value in zip(("Mean", "SD", "CI95Low", "CI95High"), expected):
                close(float(row[prefix + suffix]), value, f"aggregate r{robot_count} {prefix}{suffix}")

        current_success = {seed: seed_rows[seed]["SuccessRate"] for seed in seed_rows}
        if previous_success is None:
            for field in ("MarginalGain", "MarginalGainSD", "MarginalGainCI95Low", "MarginalGainCI95High"):
                if row[field] != "":
                    raise AssertionError(f"Robot count 1: {field} should be empty")
        else:
            gains = [current_success[seed] - previous_success[seed] for seed in sorted(current_success)]
            expected = sample_stats(gains)
            for field, value in zip(
                ("MarginalGain", "MarginalGainSD", "MarginalGainCI95Low", "MarginalGainCI95High"),
                expected,
            ):
                close(float(row[field]), value, f"aggregate r{robot_count} {field}")
        previous_success = current_success


def parse_metadata(path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        if line.strip():
            key, value = line.split("=", 1)
            values[key] = value
    return values


def validate_metadata() -> None:
    paths = sorted((FORMAL / "metadata").glob("*.txt"))
    if len(paths) != 15:
        raise AssertionError(f"Expected 15 formal metadata files, found {len(paths)}")
    filename_re = re.compile(r"_r(\d+)_seed(\d+)\.txt$")
    expected = {
        "unity_version": "2021.3.43f1c1",
        "test_episode_count": "100",
        "deterministic_inference": "True",
        "use_fixed_test_layout": "True",
        "record_robot_trajectory": "False",
        "use_robot_brain": "True",
        "use_human_agent": "True",
        "use_fire": "False",
        "use_panic": "True",
        "is_multi_arl": "True",
        "panic_state": "-1",
        "max_episode_seconds": "150",
        "freeze_human_policy_during_robot_training": "True",
        "robot_search_assist_enabled": "True",
        "robot_coordinated_search_enabled": "True",
        "robot_delivery_assist_enabled": "True",
        "robot_exit_snap_assist_enabled": "False",
        "robot_recovery_assist_enabled": "True",
    }
    for path in paths:
        match = filename_re.search(path.name)
        if match is None:
            raise AssertionError(f"Cannot parse condition from {path.name}")
        robot_count, base_seed = match.groups()
        values = parse_metadata(path)
        for key, expected_value in expected.items():
            if values.get(key) != expected_value:
                raise AssertionError(f"{path.name}: {key} expected {expected_value}, got {values.get(key)}")
        if values.get("scaling_robot_count") != robot_count or values.get("base_seed") != base_seed:
            raise AssertionError(f"{path.name}: filename and metadata condition differ")


def validate_trajectory() -> None:
    trace = ROOT / "trajectory_example"
    episode_rows = read_csv(trace / "episode.csv")
    if len(episode_rows) != 1 or episode_rows[0]["Seed"] != "204195" or episode_rows[0]["RobotCount"] != "3":
        raise AssertionError("Unexpected qualitative trajectory episode")
    metadata = parse_metadata(trace / "metadata.txt")
    for key, value in {
        "base_seed": "204195",
        "test_episode_count": "1",
        "scaling_robot_count": "3",
        "record_robot_trajectory": "True",
        "record_scene_trajectory": "True",
    }.items():
        if metadata.get(key) != value:
            raise AssertionError(f"Trajectory metadata: {key} expected {value}")

    robot_paths = sorted((trace / "robots").glob("*.csv"))
    if len(robot_paths) != 3:
        raise AssertionError(f"Expected 3 robot trajectory files, found {len(robot_paths)}")
    scene_robot_ids = {
        row["AgentId"] for row in read_csv(trace / "scene_states.csv") if row["AgentType"] == "Robot"
    }
    trajectory_ids = set()
    for path in robot_paths:
        rows = read_csv(path)
        if not rows:
            raise AssertionError(f"Empty robot trajectory: {path.name}")
        trajectory_ids.update(row["RobotID"] for row in rows)
    if trajectory_ids != scene_robot_ids:
        raise AssertionError("Robot IDs differ between scene and robot trajectory files")
    if not read_csv(trace / "human_events.csv") or len(read_csv(trace / "snapshot_summary.csv")) != 4:
        raise AssertionError("Incomplete scene-level trajectory records")


def validate_checksums() -> None:
    path = ROOT / "checksums" / "SHA256SUMS.txt"
    if not path.exists():
        raise AssertionError("Missing SHA256SUMS.txt")
    for line in path.read_text(encoding="ascii").splitlines():
        if not line.strip():
            continue
        expected, relative = line.split("  ", 1)
        target = ROOT / relative
        if not target.is_file():
            raise AssertionError(f"Checksum target is missing: {relative}")
        digest = hashlib.sha256(target.read_bytes()).hexdigest()
        if digest.lower() != expected.lower():
            raise AssertionError(f"Checksum mismatch: {relative}")


def main() -> None:
    computed = validate_episode_files()
    published = validate_seed_results(computed)
    validate_summaries(published)
    validate_aggregate(published)
    validate_metadata()
    validate_trajectory()
    validate_checksums()
    print("Dataset verification passed.")
    print("Formal conditions: 5 robot counts x 3 seeds x 100 episodes = 1,500 episodes.")
    print("Qualitative trajectory: 3 robots, seed 204195, 1 episode (excluded from formal statistics).")


if __name__ == "__main__":
    main()
