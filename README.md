# FSRS: Fire Scenario Rescue System

<p align="center">

**Adversarial Reinforcement Learning for Multi-Robot Emergency Evacuation under Dynamic Fire Hazards and Boundedly Rational Human Behavior**

</p>

---

## Overview

EvacARL is a multi-robot evacuation guidance framework designed for emergency fire scenarios. The project integrates:

* Adversarial Reinforcement Learning (ARL)
* Multi-Agent Posthumous Credit Assignment (MA-POCA)
* Attention-based Multi-Agent Coordination
* Fire Dynamics Simulation (PyroSim/FDS)
* Unity ML-Agents

to enable autonomous robots to safely guide panic-prone civilians through complex and dynamically evolving fire environments.

Unlike conventional evacuation systems that assume fully rational human behavior, EvacARL explicitly models:

* Panic
* Herding behavior
* Resistance to guidance
* Spatial disorientation
* Health degradation caused by smoke exposure

Through adversarial co-training, robots learn robust evacuation strategies capable of handling highly uncertain human responses and environmental hazards.

---

## Key Features

### Co-evolutionary Adversarial Training

Robots and human agents are trained simultaneously in an adversarial game.

* Robots learn evacuation guidance strategies.
* Human agents learn panic-driven behaviors.

This co-evolution process enables robots to develop sophisticated behaviors such as:

* Strategic blocking
* Dynamic regrouping
* Hazard avoidance
* Adaptive task allocation

resulting in significantly improved robustness against unpredictable crowd behaviors.

---

### Scalable Attention-Based Multi-Agent Architecture

The framework adopts an entity-centric observation model based on MA-POCA.

A permutation-invariant attention mechanism allows policies to process:

* Variable numbers of robots
* Variable numbers of civilians
* Dynamic environmental entities

without requiring a fixed observation size.

As a result, policies trained with three robots can be directly deployed to teams containing one to five robots without retraining.

---

### Hybrid Collaborative Reward Design

A hybrid reward function combines:

#### Global Objectives

* Successful evacuation
* Casualty reduction
* Rescue efficiency

#### Local Objectives

* Guidance quality
* Human following behavior
* Hazard avoidance

Additionally, a cohesion constraint prevents robots from abandoning slower followers, improving group consistency and rescue reliability.

---

### High-Fidelity Hazard Simulation

The simulation environment tightly integrates:

#### Unity Physics Engine

* Multi-floor buildings
* Navigation constraints
* Dynamic obstacles
* Collision handling

#### PyroSim/FDS Fire Dynamics

* CO concentration
* Temperature fields
* Visibility degradation
* Smoke propagation

This hazard-in-the-loop design allows realistic evaluation under both environmental and physiological constraints.

---

## Experimental Results

### Micro-Level Human–Robot Interaction

Compared with standard reinforcement learning approaches:

| Metric                  | Improvement |
| ----------------------- | ----------- |
| Evacuation Success Rate | +4.54%      |
| Evacuation Efficiency   | +18.1%      |

Adversarially trained robots learn to effectively handle irrational resistance and panic-induced behaviors.

---

### Large-Scale Building Evacuation

Experiments were conducted in a complex building environment containing:

* 26 rooms
* Multiple floors
* Dynamic fire hazards
* Smoke propagation
* Panic-driven civilians

Results demonstrate:

| Metric                  | Improvement |
| ----------------------- | ----------- |
| Average Survival Health | +48%        |
| Team Scalability        | 1–5 Robots  |
| Transfer Method         | Zero-Shot   |

Multi-robot collaboration substantially improves evacuation quality and rescue effectiveness.

---

## System Architecture

```text
          PyroSim / FDS
                 │
                 ▼
      Fire & Smoke Dynamics
                 │
                 ▼
          Unity Environment
                 │
                 ▼
      Human Agents (Panic Model)
                 ▲
                 │
       Adversarial Training
                 │
                 ▼
          Robot Agents
                 │
                 ▼
      Attention-Based MA-POCA
                 │
                 ▼
       Evacuation Actions
```

---

## Project Structure

```text
Assets
├── ML-Agents
├── Onnx
│   └── Trained Models
├── Prefabs
├── Resources
├── Scenes
├── Scripts
│
├── Env
├── Fire
├── FireAgent
├── FloorAgent
├── Human
├── Robot
└── StairsEntrance
```

### Core Components

| Module         | Description                  |
| -------------- | ---------------------------- |
| Env            | Global environment manager   |
| Fire           | Fire and smoke simulation    |
| FireAgent      | Fire source generation agent |
| FloorAgent     | Evacuation robot controller  |
| Human          | Civilian behavior model      |
| Robot          | Robot embodiment             |
| StairsEntrance | Stairwell navigation module  |

---

## Installation

### Requirements

```text
Python 3.8
PyTorch 1.8.2
Unity 2021 LTS
ML-Agents Release 19
```

### Python Dependencies

```bash
conda install pytorch torchvision torchaudio cudatoolkit=11.1 -c pytorch-lts
pip install mlagents
```

### Unity Packages

```text
com.unity.ml-agents
com.unity.ml-agents.extensions
```

---

## Training

Enable training mode in the `Env` object:

```text
isTraining    = true
useFireAgent  = true
useFloorAgent = true
useRobot      = true
```

Launch training:

```bash
mlagents-learn config-7.yaml \
--run-id=0 \
--env=Evac.exe \
--num-envs=16 \
--no-graphic \
--force
```

Training results will be saved to:

```text
results/
├── Evac/
├── SetFire/
├── run_logs/
├── configuration.yaml
├── Evac.onnx
└── SetFire.onnx
```

---

## Using Trained Models

Place exported ONNX models into:

```text
Assets/Onnx/
```

Then assign the model to the corresponding agent through the Unity Inspector.

For deployment, disable training mode:

```text
isTraining = false
```

---

## Future Work

* Sim-to-Real Transfer
* Real Robot Deployment
* Human Behavior Calibration using Real Evacuation Data
* Large-Scale Urban Evacuation Scenarios
* Multi-Hazard Emergency Response

---



---

## Acknowledgements

This project was built using:

* Unity ML-Agents
* PyTorch
* MA-POCA
* PyroSim
* FDS
* Unity Physics Engine

for research on intelligent robot-assisted emergency evacuation under dynamic fire hazards.
