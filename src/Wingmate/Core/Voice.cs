using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;

namespace Wingmate.Core
{
    /// <summary>Plays pre-rendered Bella clips (16-bit mono WAV, embedded) joined into one sound.
    /// Use "|" in the key list for a short pause between alerts.</summary>
    public static class Voice
    {
        private static readonly Dictionary<string, byte[]?> Cache = new();
        private static int _rate = 24000;
        private static SoundPlayer? _player;
        private static MemoryStream? _stream;
        /// <summary>Silence rendered in front of every clip (tools/render_voice.py LEAD_IN). Kept for the first clip only.</summary>
        private const double LeadIn = 0.18;
        /// <summary>Extra silence before playback so the audio device is awake before the first word (avoids a clipped onset).</summary>
        private const double WakePad = 0.15;
        /// <summary>Playback volume 0..1 (SoundPlayer has none, so samples are scaled).</summary>
        public static double Volume { get; set; } = 1.0;

        /// <summary>Time until the speech in the joined clips ends (without playing them).</summary>
        public static TimeSpan Duration(IEnumerable<string> keys)
        {
            return TimeSpan.FromSeconds(SpeechEnd(Stitch(keys)) / 2.0 / _rate); // until the voice ends, not the silent tail
        }

        public static void Play(IEnumerable<string> keys)
        {
            try
            {
                var pcm = Stitch(keys);
                if (pcm.Length == 0) return;
                Stop();
                double v = Math.Clamp(Volume, 0, 1);
                if (v <= 0) return;
                if (v < 0.999)
                    for (int i = 0; i + 1 < pcm.Length; i += 2)
                    {
                        short smp = (short)Math.Round(BitConverter.ToInt16(pcm, i) * v);
                        pcm[i] = (byte)smp; pcm[i + 1] = (byte)(smp >> 8);
                    }
                _stream = new MemoryStream(Wav(pcm));
                _player = new SoundPlayer(_stream);
                _player.Play(); // async
            }
            catch (Exception ex) { Log.Write("voice: " + ex.Message); }
        }

        /// <summary>Joins clips. Each clip keeps its natural decay; between clips only ~120 ms of its silent tail is kept
        /// so phrases flow naturally, the last clip keeps its full tail, and 250 ms of extra silence lets the
        /// audio device drain completely before playback ends (so the last word's decay is never clipped).</summary>
        private static byte[] Stitch(IEnumerable<string> keys)
        {
            var list = keys.ToList();
            var pcm = new MemoryStream();
            pcm.Write(new byte[(int)(_rate * WakePad) * 2]);
            bool first = true;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == "|") { pcm.Write(new byte[(int)(_rate * 0.35) * 2]); continue; }
                var data = Load(list[i]) ?? Load("generic");
                if (data == null) continue;
                bool lastClip = !list.Skip(i + 1).Any(k => k != "|");
                int len = data.Length;
                if (!lastClip && list[i + 1] != "|") len = Math.Min(len, SpeechEnd(data) + (int)(_rate * 0.12) * 2);
                int skip = first ? 0 : Math.Min((int)(_rate * LeadIn) * 2, SpeechStart(data)); // mid-phrase: drop the lead-in, keep the pre-roll
                first = false;
                if (len > skip) pcm.Write(data, skip, len - skip);
            }
            if (first) return Array.Empty<byte>();
            pcm.Write(new byte[(int)(_rate * 0.25) * 2]);
            return pcm.ToArray();
        }

        /// <summary>Byte offset just past the last sample louder than about -54 dBFS (end of the speech decay).</summary>
        private static int SpeechEnd(byte[] data)
        {
            for (int i = data.Length - 2; i >= 0; i -= 2)
                if (Math.Abs((int)BitConverter.ToInt16(data, i)) > 66) return i + 2;
            return data.Length;
        }

        /// <summary>Byte offset of the first audible sample (never skip past it).</summary>
        private static int SpeechStart(byte[] data)
        {
            for (int i = 0; i + 1 < data.Length; i += 2)
                if (Math.Abs((int)BitConverter.ToInt16(data, i)) > 16) return i & ~1;
            return data.Length & ~1;
        }

        public static void Stop()
        {
            try { _player?.Stop(); _player?.Dispose(); } catch { }
            _player = null;
            _stream?.Dispose(); _stream = null;
        }

        private static byte[]? Load(string key)
        {
            if (Cache.TryGetValue(key, out var cached)) return cached;
            byte[]? result = null;
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("voice." + key + ".wav"))
            {
                if (s != null)
                {
                    using var br = new BinaryReader(s);
                    br.ReadBytes(12); // RIFF....WAVE
                    while (s.Position + 8 <= s.Length)
                    {
                        var id = new string(br.ReadChars(4));
                        int size = br.ReadInt32();
                        if (id == "fmt ") { var f = br.ReadBytes(size); _rate = BitConverter.ToInt32(f, 4); }
                        else if (id == "data") { result = br.ReadBytes(size); break; }
                        else br.ReadBytes(size);
                        if ((size & 1) == 1 && s.Position < s.Length) br.ReadByte();
                    }
                }
            }
            Cache[key] = result;
            return result;
        }

        private static byte[] Wav(byte[] pcm)
        {
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write("RIFF"u8.ToArray()); w.Write(36 + pcm.Length); w.Write("WAVE"u8.ToArray());
            w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(_rate); w.Write(_rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write("data"u8.ToArray()); w.Write(pcm.Length); w.Write(pcm);
            w.Flush();
            return ms.ToArray();
        }
    }
}
