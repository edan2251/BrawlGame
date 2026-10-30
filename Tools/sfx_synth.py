"""KNOCKBOX 효과음 합성기 (05-presentation.md 7절).

파이썬 표준 라이브러리만 쓴다. 효과음마다 고정 시드라 다시 실행해도 같은 파일이 나온다.

    python Tools/sfx_synth.py                  # Assets/_Project/Audio/Sfx 에 전부 생성
    python Tools/sfx_synth.py --out DIR jump   # DIR 에 jump.wav 만 생성

새 효과음은 SOUNDS 표에 함수를 더한다. 소리를 바꾸면 이 파일과 WAV를 함께 커밋한다.
"""
import argparse
import math
import os
import random
import struct
import wave

RATE = 44100
DEFAULT_OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "_Project", "Audio", "Sfx")


def envelope(t, attack, decay_time):
    """빠르게 올라가고 지수로 줄어드는 포락선."""
    if t < attack:
        return t / attack
    return math.exp(-(t - attack) / decay_time)


def lowpass(samples, amount):
    """한 극 저역 통과. amount 가 0에 가까울수록 더 먹먹하다."""
    out, y = [], 0.0
    for x in samples:
        y += amount * (x - y)
        out.append(y)
    return out


def sweep(duration, f0, f1, rng, noise=0.0, attack=0.004, decay=0.05, shape="sine"):
    """f0 → f1 로 미끄러지는 음 + 소음."""
    n = int(duration * RATE)
    phase, out = 0.0, []
    for i in range(n):
        t = i / RATE
        k = i / max(n - 1, 1)
        freq = f0 * (f1 / f0) ** k
        phase += 2.0 * math.pi * freq / RATE
        if shape == "square":
            tone = 1.0 if math.sin(phase) >= 0 else -1.0
            tone *= 0.5
        else:
            tone = math.sin(phase)
        value = tone * (1.0 - noise) + (rng.random() * 2.0 - 1.0) * noise
        out.append(value * envelope(t, attack, decay))
    return out


def jump(rng):
    body = sweep(0.14, 330.0, 760.0, rng, noise=0.08, attack=0.003, decay=0.045)
    return [s * 0.55 for s in body]


def land(rng):
    thump = sweep(0.16, 140.0, 55.0, rng, noise=0.0, attack=0.002, decay=0.05)
    n = len(thump)
    dust = lowpass([(rng.random() * 2.0 - 1.0) * envelope(i / RATE, 0.001, 0.03) for i in range(n)], 0.18)
    return [0.75 * a + 0.5 * b for a, b in zip(thump, dust)]


def dash(rng):
    n = int(0.24 * RATE)
    raw = [(rng.random() * 2.0 - 1.0) for _ in range(n)]
    # 바람 소리: 소음을 점점 밝게(저역 통과 계수를 키우며) 걸러 '휙' 하고 지나가게 한다.
    out, y = [], 0.0
    for i, x in enumerate(raw):
        k = i / (n - 1)
        amount = 0.04 + 0.35 * math.sin(math.pi * k)
        y += amount * (x - y)
        out.append(y * math.sin(math.pi * k) ** 0.7)
    return [s * 1.4 for s in out]


SOUNDS = {
    "jump": (jump, 101),
    "land": (land, 102),
    "dash": (dash, 103),
}


def normalize(samples, peak=0.85):
    top = max(abs(s) for s in samples) or 1.0
    return [s * peak / top for s in samples]


def write_wav(path, samples):
    frames = b"".join(struct.pack("<h", int(round(max(-1.0, min(1.0, s)) * 32767))) for s in samples)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(frames)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", default=DEFAULT_OUT)
    parser.add_argument("names", nargs="*", help="만들 효과음 이름(없으면 전부)")
    args = parser.parse_args()

    names = args.names or sorted(SOUNDS)
    os.makedirs(args.out, exist_ok=True)
    for name in names:
        if name not in SOUNDS:
            raise SystemExit(f"알 수 없는 효과음: {name} (가능: {', '.join(sorted(SOUNDS))})")
        func, seed = SOUNDS[name]
        samples = normalize(func(random.Random(seed)))
        path = os.path.join(args.out, name + ".wav")
        write_wav(path, samples)
        print(f"{path} ({len(samples) / RATE:.3f}s)")


if __name__ == "__main__":
    main()
