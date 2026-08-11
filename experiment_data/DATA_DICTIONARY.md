# Data Dictionary

## `formal_evaluation/episodes/*.csv`

| Column | Meaning |
|---|---|
| `RunId` | Unique evaluation-run identifier, including checkpoint, robot count, base seed, and tag. |
| `Episode` | One-based episode index inside the 100-episode batch. |
| `Mode` | Unity experiment mode. |
| `RobotCount` | Number of robots used in the episode. |
| `PanicState` | `-1` lets the frozen human policy select its state. |
| `Seed` | Episode seed. It starts at the batch base seed and increments by episode. |
| `Termination` | Termination reason; `timeout` means the 150-s horizon was reached. |
| `InitialHumans` | Initial human count (50 in every formal episode). |
| `Escaped` | Humans that reached an exit during the episode. |
| `Deaths` | Humans recorded dead during the episode. |
| `AliveAtEnd` | Humans alive but not escaped at termination. |
| `EpisodeTime` | Simulated episode duration in seconds. |
| `EscapedHealthSum` | Sum of final health for escaped humans. |
| `AliveHealthSum` | Sum of final health for humans alive but not escaped at termination. |
| `FinalSurvivorHealthSum` | `EscapedHealthSum + AliveHealthSum`. |
| `SuccessRate` | `100 * Escaped / InitialHumans` for this episode. |
| `AvgEscapedHealthAll` | `EscapedHealthSum / InitialHumans`. |
| `AvgFinalHealthAll` | `FinalSurvivorHealthSum / InitialHumans`. |

The population accounting identity is
`InitialHumans = Escaped + Deaths + AliveAtEnd`.

## `formal_evaluation/summaries/*.csv`

These files contain cumulative snapshots, normally every 10 episodes. Use only
the row with `IsFinal=true` for the completed batch.

| Column | Meaning |
|---|---|
| `RunId`, `Mode`, `RobotCount`, `PanicState`, `Seed` | Batch identity and fixed condition. `Seed` is the base seed. |
| `TotalEpisodes` | Number of episodes accumulated in this row. |
| `InitialHumans`, `Escaped`, `Deaths` | Cumulative population counts. |
| `SuccessRate` | `100 * Escaped / InitialHumans`. |
| `AvgEpisodeTime` | Mean simulated duration over accumulated episodes. |
| `AvgEscapedHealthAll` | Cumulative escaped-health sum divided by cumulative initial humans. |
| `AvgFinalHealthAll` | Cumulative final-survivor-health sum divided by cumulative initial humans. |
| `AvgEscapedPerEpisode` | `Escaped / TotalEpisodes`. |
| `AvgDeathsPerEpisode` | `Deaths / TotalEpisodes`. |
| `IsFinal` | `true` only for the completed formal batch row. |

## `formal_evaluation/seed_results.csv`

One row per `(RobotCount, base Seed)` formal batch. `TimeoutRate` is the percentage
of its 100 raw episodes whose `Termination` is `timeout`. All other metrics are
the final cumulative values defined above.

## `formal_evaluation/aggregate.csv`

One row per robot count. `Seeds=3` and `Episodes=300` in every row. Suffixes mean:

- `Mean`: arithmetic mean over the three seed-level values.
- `SD`: sample standard deviation over the three seed-level values.
- `CI95Low`, `CI95High`: two-sided Student-t interval with `df=2`.
- `MarginalGain`: paired, seed-matched escape-rate increase from `N-1` to `N`
  robots. It is empty for one robot.
- `AvgHealth*`: aggregate form of `AvgEscapedHealthAll`.
- `AvgFinalHealth*`: aggregate form of `AvgFinalHealthAll`.
- `DeathsPerEpisode*`, `TimeoutRate*`, `AvgEpisodeTime*`: corresponding
  seed-level metrics and their uncertainty statistics.

## `trajectory_example/scene_states.csv`

| Column | Meaning |
|---|---|
| `Episode`, `Time` | Trace episode and simulated time. The recorder uses zero-based episode numbering. |
| `AgentType` | `Robot` or `Human`. |
| `AgentId` | Unity runtime instance identifier. |
| `X`, `Z` | Ground-plane position in Unity world coordinates. |
| `Health` | Human health; empty for robots. |
| `HumanState` | Human movement/behavior state; empty for robots. |
| `DirectLeaderId` | Immediate leader in the recorded guidance chain. |
| `TopLevelLeaderId` | Root leader reached by following the leader chain. |
| `DirectFollowerCount` | Number of direct followers recorded at the sample time. |

## `trajectory_example/human_events.csv`

Records each human `escape` or `death` event with time, position, health, and the
direct/top-level leader identifiers at that event.

## `trajectory_example/robots/*.csv`

| Column | Meaning |
|---|---|
| `Episode` | Zero-based trace episode index. |
| `RobotID` | Robot Unity runtime identifier; it matches `scene_states.csv`. |
| `Time` | Simulated time in seconds. |
| `X`, `Z` | Robot ground-plane position. |
| `FollowerCount` | Direct follower count recorded by the robot trajectory logger. |

## `trajectory_example/snapshot_summary.csv`

Contains the requested figure time, nearest sampled time, active-human count,
robot-connected-human count, cumulative escapes, and cumulative deaths used for
the scene-level timeline panels.
