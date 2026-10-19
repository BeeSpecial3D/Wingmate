"""Render Wingmate's Bella voice clips with Kokoro (af_bella). Run on the build box only.
Usage: python tools/render_voice.py <models_dir> <out_dir>"""
import sys, os, numpy as np, soundfile as sf
LEAD_IN = 0.18  # seconds of silence before each clip (Voice.LeadIn in the app must match)
from kokoro_onnx import Kokoro

CLIPS = {
    # event names (keys match "voice" in schedule.json)
    "siege": "Artifact Siege", "siege_bosses": "Siege bosses", "nahma": "Guardian Lord Nahma",
    "watcher_kaira": "Watcher Kaira", "rift": "Rift portals", "arena": "Arena of Tactics",
    "weekly_reset": "Weekly reset", "shugo": "Shugo Festival", "invasion": "Dimensional Invasion",
    "generic": "Abyss event",
    # phrases
    "in_1": "starts in one minute.", "in_2": "starts in two minutes.", "in_3": "starts in three minutes.",
    "in_5": "starts in five minutes.", "in_10": "starts in ten minutes.", "in_15": "starts in fifteen minutes.",
    "in_30": "starts in thirty minutes.", "in_60": "starts in one hour.",
    "now": "is starting now!", "test": "This is a test alert.",
}
# "starts in N minutes." for every warning time 1-60 (60 = "one hour"), so any lead time can be announced
ONES = "zero one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen seventeen eighteen nineteen".split()
TENS = {20: "twenty", 30: "thirty", 40: "forty", 50: "fifty"}
def words(n): return ONES[n] if n < 20 else TENS[n - n % 10] + ("" if n % 10 == 0 else "-" + ONES[n % 10])
for n in range(1, 61):
    CLIPS[f"in_{n}"] = "starts in one hour." if n == 60 else f"starts in {words(n)} minute{'' if n == 1 else 's'}."
# under 10 minutes: urgent wording, slightly faster (Bella has no emotion control, so urgency comes from text + speed)
SPEED = {}  # key -> Kokoro speed (default 1.0)
for n in range(1, 10):
    CLIPS[f"in_{n}"] = f"starts in {words(n)} minute{'' if n == 1 else 's'}! Get ready!"
    SPEED[f"in_{n}"] = 1.1
# checklist celebration lines (keys cel_<pool>_<n>, same order as CelebrationLines.Pools in Core/Checklist.cs)
CEL = {
    "daily": ["Nice work, dailies done!", "That's your dailies, all wrapped up.", "Dailies complete. Go enjoy the rest of your day.", "All done for today. Great job!"],
    "weekly": ["Weeklies complete. That's a solid week!", "Every weekly checked off. Nicely done.", "That's the whole week handled. Well played!"],
    "custom": ["Checklist complete!", "All done. Nice work!", "List complete. Time to make a new one!", "All checked off. Time for a new list!"],
    "long": ["That was a long one. Seriously impressive!", "Every last one done. That's real dedication.", "Wow, you powered through that whole list!", "Big list, fully cleared. You earned a break!"],
}
for pool, lines in CEL.items():
    for i, t in enumerate(lines): CLIPS[f"cel_{pool}_{i+1}"] = t
# stitched clips: key -> [(text, speed, pause after in seconds)], each part rendered and trimmed on its own, then
# joined (lead-in dropped, cut 120 ms after the speech decay) with the pauses, for comedic timing
STITCHED = {
    # all of a character's lists complete (Core/Checklist.cs CelebrationLines.AllDoneKey)
    "cel_all_1": [("Amazing.", 0.9, 0.30), ("Really... amazing.", 0.9, 0.55),
                  ("But I really do think you should go drink some water, and take a break.", 1.0, 0.65),
                  ("But hey, I'm just a program.", 1.0, 0.25), ("So what do I know?", 1.05, 0.0)],
}
only = set(sys.argv[3:])  # optional: render only these keys
models, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
k = Kokoro(os.path.join(models, "kokoro-v1.0.int8.onnx"), os.path.join(models, "voices-v1.0.bin"))
def render(text, speed):
    s, sr = k.create(text, voice="af_bella", speed=speed, lang="en-us")
    s = np.asarray(s, dtype=np.float32)
    # Keep the natural onset and decay: trim only true silence (below 0.0005) with generous margins
    # (100 ms before the first sound so soft onsets and plosive bursts like the T in "This" survive,
    # 150 ms after the decay), then add 180 ms of silence in front (LEAD_IN, skipped by the app when a
    # clip follows another mid-phrase) and 300 ms behind. Short fades touch only the silent margins.
    idx = np.where(np.abs(s) > 0.0005)[0]
    if len(idx):
        start = max(0, idx[0] - int(0.10*sr))
        end = idx[-1] + int(0.15*sr)
        if end > len(s): s = np.concatenate([s, np.zeros(end - len(s), np.float32)])
        s = s[start:end].copy()
        f = int(0.01*sr); s[:f] *= np.linspace(0, 1, f, dtype=np.float32)  # only inside the 100 ms pre-roll
        f = int(0.03*sr); s[-f:] *= np.linspace(1, 0, f, dtype=np.float32)
    return np.concatenate([np.zeros(int(LEAD_IN*sr), np.float32), s, np.zeros(int(0.30*sr), np.float32)]), sr

def stitched(parts):
    body = []
    for text, speed, pause in parts:
        s, sr = render(text, speed)
        loud = np.where(np.abs(s) > 66/32768)[0]  # same speech-end threshold as the app (about -54 dBFS)
        end = (loud[-1] + 1 if len(loud) else len(s)) + int(0.12*sr)
        body += [s[int(LEAD_IN*sr):end], np.zeros(int(pause*sr), np.float32)]
    return np.concatenate([np.zeros(int(LEAD_IN*sr), np.float32), *body, np.zeros(int(0.30*sr), np.float32)]), sr

for key, text in list(CLIPS.items()) + [(k2, None) for k2 in STITCHED]:
    if only and key not in only: continue
    s, sr = stitched(STITCHED[key]) if text is None else render(text, SPEED.get(key, 1.0))
    sf.write(os.path.join(out, key + ".wav"), s, sr, subtype="PCM_16")
    print(key, sr, round(len(s)/sr, 2))
