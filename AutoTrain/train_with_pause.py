#!/usr/bin/env python3
"""
支持自动暂停的ML-Agents训练脚本
可以设置检查点自动暂停训练
"""

import argparse
import signal
import subprocess
import sys
from datetime import datetime


DEFAULT_BASE_PORT = 5005
HEADER_WIDTH = 70
SECTION_WIDTH = 50

CONTINUE_COMMANDS = {"c", "continue"}
STOP_COMMANDS = {"s", "stop"}
RESTART_COMMANDS = {"r", "restart"}
INFO_COMMANDS = {"i", "info"}


class PausableTrainer:
    def __init__(self):
        self.process = None
        self.is_paused = False
        self.pause_requested = False

    def signal_handler(self, sig, frame):
        """处理Ctrl+C信号"""
        print("\n\n⏸️  接收到暂停信号")
        self.pause_requested = True

    def run_training_with_pause(self, config_file, run_id, pause_steps=None, base_port=DEFAULT_BASE_PORT):
        """
        运行可暂停的训练

        参数:
            config_file: 配置文件路径
            run_id: 训练ID
            pause_steps: 在哪些步数暂停 [5000, 10000, 20000]
            base_port: 通信端口
        """

        if pause_steps is None:
            pause_steps = []

        self._print_training_header(run_id, pause_steps)

        # 注册信号处理器
        signal.signal(signal.SIGINT, self.signal_handler)
        cmd = self._build_training_command(config_file, run_id, base_port)

        try:
            self.process = self._start_training_process(cmd)

            current_step = 0
            next_pause_index = 0

            print("🚀 训练开始...")
            print("-" * SECTION_WIDTH)

            # 实时读取输出
            for line in self._iter_training_output():
                print(line)
                current_step = self._parse_current_step(line, current_step)
                next_pause_index = self._handle_auto_pause(
                    pause_steps=pause_steps,
                    next_pause_index=next_pause_index,
                    current_step=current_step,
                )
                self._handle_manual_pause(current_step)

            # 训练结束
            return_code = self.process.wait()
            print(f"\n训练结束，退出代码: {return_code}")

        except Exception as e:
            print(f"❌ 训练错误: {e}")

    def _print_training_header(self, run_id, pause_steps):
        print("=" * HEADER_WIDTH)
        print("⏯️  可暂停的ML-Agents训练")
        print("=" * HEADER_WIDTH)
        print(f"训练任务: {run_id}")
        print(f"暂停检查点: {pause_steps}")
        print("提示: 按 Ctrl+C 可以手动请求暂停")
        print("=" * HEADER_WIDTH)

    def _build_training_command(self, config_file, run_id, base_port):
        return [
            "mlagents-learn",
            config_file,
            "--run-id", run_id,
            "--base-port", str(base_port),
            "--force",
        ]

    def _start_training_process(self, cmd):
        return subprocess.Popen(
            cmd,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            universal_newlines=True,
            bufsize=1,
        )

    def _iter_training_output(self):
        for line in iter(self.process.stdout.readline, ''):
            if not line and self.process.poll() is not None:
                break

            line = line.strip()
            if line:
                yield line

    def _parse_current_step(self, line, current_step):
        if "Step:" not in line or "Time Elapsed:" not in line:
            return current_step

        try:
            step_part = line.split("Step:")[1].split(",")[0].strip()
            return int(step_part)
        except (IndexError, ValueError):
            return current_step

    def _handle_auto_pause(self, pause_steps, next_pause_index, current_step):
        if not self._should_auto_pause(pause_steps, next_pause_index, current_step):
            return next_pause_index

        pause_step = pause_steps[next_pause_index]
        print(f"\n🎯 到达自动暂停点: {pause_step} 步")
        self._pause_training()

        next_pause_index += 1
        if next_pause_index < len(pause_steps):
            next_step = pause_steps[next_pause_index]
            print(f"下一个暂停点: {next_step} 步")
        else:
            print("这是最后一个暂停点")

        return next_pause_index

    def _should_auto_pause(self, pause_steps, next_pause_index, current_step):
        return (
            pause_steps
            and next_pause_index < len(pause_steps)
            and current_step >= pause_steps[next_pause_index]
        )

    def _handle_manual_pause(self, current_step):
        if not self.pause_requested:
            return

        print(f"\n⏸️  手动暂停请求，当前步数: {current_step}")
        self._pause_training()
        self.pause_requested = False

    def _pause_training(self):
        """暂停训练并等待用户确认继续"""
        print("\n" + "=" * SECTION_WIDTH)
        print("⏸️  训练已暂停")
        print("=" * SECTION_WIDTH)
        print("选择操作:")
        print("1. 输入 'c' 或 'continue' 继续训练")
        print("2. 输入 's' 或 'stop' 停止训练")
        print("3. 输入 'r' 或 'restart' 重启训练")
        print("4. 输入 'i' 或 'info' 查看当前状态")

        while True:
            try:
                user_input = input("\n请输入选择: ").strip().lower()

                if user_input in CONTINUE_COMMANDS:
                    print("🔄 继续训练...")
                    print("-" * SECTION_WIDTH)
                    break

                elif user_input in STOP_COMMANDS:
                    print("🛑 停止训练...")
                    if self.process:
                        self.process.terminate()
                    sys.exit(0)

                elif user_input in RESTART_COMMANDS:
                    print("🔄 重启训练...")
                    if self.process:
                        self.process.terminate()
                    return "restart"

                elif user_input in INFO_COMMANDS:
                    print("📊 当前状态:")
                    print("  训练暂停中...")

                else:
                    print("❓ 未知命令，请重新输入")

            except KeyboardInterrupt:
                print("\n🛑 强制停止训练")
                if self.process:
                    self.process.terminate()
                sys.exit(0)

        return "continue"


def main():
    parser = argparse.ArgumentParser(description='可暂停的ML-Agents训练')
    parser.add_argument('--config', required=True, help='AutoTrain/ARL_config.yaml')
    parser.add_argument('--run-id', default=f"pausable_{datetime.now().strftime('%H%M%S')}")
    parser.add_argument('--pause-steps', type=int, nargs='+',
                        help='在指定步数自动暂停，例如: --pause-steps 100000 150000 200000')
    parser.add_argument('--port', type=int, default=DEFAULT_BASE_PORT)

    args = parser.parse_args()

    trainer = PausableTrainer()

    while True:
        result = trainer.run_training_with_pause(
            config_file=args.config,
            run_id=args.run_id,
            pause_steps=args.pause_steps,
            base_port=args.port
        )

        if result != "restart":
            break

        print("\n" + "=" * SECTION_WIDTH)
        print("🔄 重新启动训练...")
        print("=" * SECTION_WIDTH)


if __name__ == "__main__":
    main()
