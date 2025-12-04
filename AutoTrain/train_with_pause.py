#!/usr/bin/env python3
"""
支持自动暂停的ML-Agents训练脚本
可以设置检查点自动暂停训练
"""

import os
import sys
import subprocess
import argparse
import time
import signal
from datetime import datetime


class PausableTrainer:
    def __init__(self):
        self.process = None
        self.is_paused = False
        self.pause_requested = False

    def signal_handler(self, sig, frame):
        """处理Ctrl+C信号"""
        print("\n\n⏸️  接收到暂停信号")
        self.pause_requested = True

    def run_training_with_pause(self, config_file, run_id, pause_steps=None, base_port=5005):
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

        print("=" * 70)
        print("⏯️  可暂停的ML-Agents训练")
        print("=" * 70)
        print(f"训练任务: {run_id}")
        print(f"暂停检查点: {pause_steps}")
        print("提示: 按 Ctrl+C 可以手动请求暂停")
        print("=" * 70)

        # 注册信号处理器
        signal.signal(signal.SIGINT, self.signal_handler)

        # 构建命令
        cmd = [
            "mlagents-learn",
            config_file,
            "--run-id", run_id,
            "--base-port", str(base_port),
            "--force"
        ]

        try:
            self.process = subprocess.Popen(
                cmd,
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                universal_newlines=True,
                bufsize=1
            )

            current_step = 0
            next_pause_index = 0
            is_training = True

            print("🚀 训练开始...")
            print("-" * 50)

            # 实时读取输出
            for line in iter(self.process.stdout.readline, ''):
                if not line and self.process.poll() is not None:
                    break

                line = line.strip()
                if line:
                    print(line)

                    # 解析当前训练步数
                    if "Step:" in line and "Time Elapsed:" in line:
                        # 从日志中提取步数
                        try:
                            step_part = line.split("Step:")[1].split(",")[0].strip()
                            current_step = int(step_part)
                            # print(f"当前步数: {current_step}")  # 调试用
                        except:
                            pass

                    # 检查是否到达自动暂停点
                    if (pause_steps and
                            next_pause_index < len(pause_steps) and
                            current_step >= pause_steps[next_pause_index]):

                        print(f"\n🎯 到达自动暂停点: {pause_steps[next_pause_index]} 步")
                        self._pause_training()
                        next_pause_index += 1

                        if next_pause_index < len(pause_steps):
                            next_step = pause_steps[next_pause_index]
                            print(f"下一个暂停点: {next_step} 步")
                        else:
                            print("这是最后一个暂停点")

                    # 检查手动暂停请求
                    if self.pause_requested:
                        print(f"\n⏸️  手动暂停请求，当前步数: {current_step}")
                        self._pause_training()
                        self.pause_requested = False

            # 训练结束
            return_code = self.process.wait()
            print(f"\n训练结束，退出代码: {return_code}")

        except Exception as e:
            print(f"❌ 训练错误: {e}")

    def _pause_training(self):
        """暂停训练并等待用户确认继续"""
        print("\n" + "=" * 50)
        print("⏸️  训练已暂停")
        print("=" * 50)
        print("选择操作:")
        print("1. 输入 'c' 或 'continue' 继续训练")
        print("2. 输入 's' 或 'stop' 停止训练")
        print("3. 输入 'r' 或 'restart' 重启训练")
        print("4. 输入 'i' 或 'info' 查看当前状态")

        while True:
            try:
                user_input = input("\n请输入选择: ").strip().lower()

                if user_input in ['c', 'continue']:
                    print("🔄 继续训练...")
                    print("-" * 50)
                    break

                elif user_input in ['s', 'stop']:
                    print("🛑 停止训练...")
                    if self.process:
                        self.process.terminate()
                    sys.exit(0)

                elif user_input in ['r', 'restart']:
                    print("🔄 重启训练...")
                    if self.process:
                        self.process.terminate()
                    return "restart"

                elif user_input in ['i', 'info']:
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
    parser.add_argument('--port', type=int, default=5005)

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

        print("\n" + "=" * 50)
        print("🔄 重新启动训练...")
        print("=" * 50)


if __name__ == "__main__":
    main()