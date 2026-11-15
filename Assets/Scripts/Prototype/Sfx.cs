using System.Collections.Generic;
using UnityEngine;

namespace SteamGame.Prototype
{
    public enum Sound
    {
        Slash,
        Hit,
        Pincer,
        Block,
        Hurt,
        Jump,
        Dash,
        Swap,
        Anchor,
        Warn,
        Shoot,
        EnemyDie,
        Explosion,
        Buy,
        Deny,
        DoorOpen,
        Solve,
        BossIntro,
        BossRage,
        BossSlam,
        BossDie,
    }

    /// <summary>
    /// 효과음과 보스전 음악. 소리 파일 없이, 사인파와 사각파와 잡음을 섞어 Play 때 만들어 낸다.
    /// 소리마다 높낮이가 변하는 음 몇 개와 잡음 하나로 이루어진다. 수치는 Recipe에서 바꾼다.
    /// </summary>
    public static class Sfx
    {
        /// <summary>전체 음량. 0에서 1 사이.</summary>
        public const float MasterVolume = 0.7f;

        private const int SampleRate = 44100;
        /// <summary>같은 소리가 한꺼번에 여러 번 겹쳐 커지지 않게 하는 최소 간격(초).</summary>
        private const float MinRepeatGap = 0.04f;
        private const float MusicVolume = 0.5f;
        private const float MusicLength = 2.4f;
        private const float MusicBeat = 0.6f;

        /// <summary>음 하나. From에서 To로 높낮이가 변하고, Decay가 클수록 빨리 사라진다.</summary>
        private struct Tone
        {
            public float From;
            public float To;
            public float Volume;
            public float Decay;
            public float Delay;
            public bool Square;

            public Tone(float from, float to, float volume, float decay, float delay = 0f, bool square = false)
            {
                From = from;
                To = to;
                Volume = volume;
                Decay = decay;
                Delay = delay;
                Square = square;
            }
        }

        private static readonly Dictionary<Sound, AudioClip> Clips = new Dictionary<Sound, AudioClip>();
        private static readonly Dictionary<Sound, float> LastPlayed = new Dictionary<Sound, float>();
        private static AudioSource _source;
        private static AudioSource _music;
        private static AudioClip _musicClip;
        private static uint _noiseState = 12345u;

        /// <summary>Play를 시작할 때 한 번 부른다. host에 소리를 내는 부품을 붙인다.</summary>
        public static void Attach(GameObject host)
        {
            _source = host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _music = host.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            LastPlayed.Clear();
            AudioListener.volume = 1f;

            // 처음 날 때 만들면 그 순간 화면이 멈칫하므로, 시작할 때 모두 만들어 둔다.
            foreach (Sound sound in System.Enum.GetValues(typeof(Sound)))
            {
                Get(sound);
            }
            if (_musicClip == null) _musicClip = BuildMusic();
        }

        public static bool Muted
        {
            get { return AudioListener.volume <= 0f; }
            set { AudioListener.volume = value ? 0f : 1f; }
        }

        public static void Play(Sound sound, float volume = 1f)
        {
            if (_source == null) return;

            float now = Time.unscaledTime;
            float last;
            if (LastPlayed.TryGetValue(sound, out last) && now - last < MinRepeatGap) return;
            LastPlayed[sound] = now;

            AudioClip clip = Get(sound);
            if (clip != null) _source.PlayOneShot(clip, Mathf.Clamp01(volume) * MasterVolume);
        }

        /// <summary>보스전 음악: 낮게 깔리는 음과 심장 박동. 보스가 분노하면 빨라진다.</summary>
        public static void StartBossMusic()
        {
            if (_music == null) return;

            if (_musicClip == null) _musicClip = BuildMusic();
            _music.clip = _musicClip;
            _music.volume = MusicVolume * MasterVolume;
            _music.pitch = 1f;
            _music.Play();
        }

        public static void SetBossMusicEnraged(bool enraged)
        {
            if (_music != null) _music.pitch = enraged ? 1.2f : 1f;
        }

        public static void StopBossMusic()
        {
            if (_music != null) _music.Stop();
        }

        private static AudioClip Get(Sound sound)
        {
            AudioClip clip;
            // 플레이를 멈추면 만든 소리가 사라지므로, 사라진 것은 다시 만든다.
            if (Clips.TryGetValue(sound, out clip) && clip != null) return clip;

            clip = Recipe(sound);
            Clips[sound] = clip;
            return clip;
        }

        // Build(이름, 길이, 음들, 잡음 음량, 잡음 감쇠, 잡음 밝기(1이면 날카롭고 작을수록 둔탁함), 잡음이 부풀었다 꺼지는지)
        private static AudioClip Recipe(Sound sound)
        {
            string name = sound.ToString();
            switch (sound)
            {
                case Sound.Slash:
                    return Build(name, 0.13f, new[] { new Tone(900f, 300f, 0.08f, 12f) }, 0.5f, 0f, 0.9f, true);
                case Sound.Hit:
                    return Build(name, 0.14f, new[] { new Tone(190f, 70f, 0.7f, 22f) }, 0.45f, 45f, 0.6f, false);
                case Sound.Pincer:
                    return Build(name, 0.38f, new[] { new Tone(190f, 70f, 0.7f, 22f), new Tone(988f, 988f, 0.28f, 9f), new Tone(1480f, 1480f, 0.22f, 9f, 0.05f) }, 0.45f, 45f, 0.6f, false);
                case Sound.Block:
                    return Build(name, 0.14f, new[] { new Tone(1250f, 1250f, 0.3f, 30f, 0f, true), new Tone(1870f, 1870f, 0.2f, 35f, 0f, true) }, 0.2f, 60f, 1f, false);
                case Sound.Hurt:
                    return Build(name, 0.3f, new[] { new Tone(420f, 140f, 0.45f, 7f, 0f, true) }, 0.3f, 25f, 0.4f, false);
                case Sound.Jump:
                    return Build(name, 0.1f, new[] { new Tone(330f, 620f, 0.28f, 12f) }, 0f, 0f, 1f, false);
                case Sound.Dash:
                    return Build(name, 0.2f, new[] { new Tone(500f, 200f, 0.1f, 10f) }, 0.5f, 0f, 0.25f, true);
                case Sound.Swap:
                    return Build(name, 0.22f, new[] { new Tone(520f, 520f, 0.3f, 18f), new Tone(880f, 880f, 0.3f, 14f, 0.08f) }, 0f, 0f, 1f, false);
                case Sound.Anchor:
                    return Build(name, 0.3f, new[] { new Tone(196f, 196f, 0.35f, 8f), new Tone(392f, 392f, 0.2f, 10f), new Tone(98f, 98f, 0.1f, 10f, 0f, true) }, 0f, 0f, 1f, false);
                case Sound.Warn:
                    return Build(name, 0.07f, new[] { new Tone(1400f, 1400f, 0.16f, 25f, 0f, true) }, 0f, 0f, 1f, false);
                case Sound.Shoot:
                    return Build(name, 0.14f, new[] { new Tone(880f, 330f, 0.22f, 14f, 0f, true) }, 0f, 0f, 1f, false);
                case Sound.EnemyDie:
                    return Build(name, 0.32f, new[] { new Tone(320f, 70f, 0.5f, 9f) }, 0.4f, 14f, 0.5f, false);
                case Sound.Explosion:
                    return Build(name, 0.6f, new[] { new Tone(110f, 35f, 0.8f, 6f) }, 0.7f, 7f, 0.15f, false);
                case Sound.Buy:
                    return Build(name, 0.3f, new[] { new Tone(988f, 988f, 0.3f, 14f), new Tone(1319f, 1319f, 0.3f, 10f, 0.09f) }, 0f, 0f, 1f, false);
                case Sound.Deny:
                    return Build(name, 0.2f, new[] { new Tone(160f, 160f, 0.3f, 30f, 0f, true), new Tone(150f, 150f, 0.3f, 30f, 0.1f, true) }, 0f, 0f, 1f, false);
                case Sound.DoorOpen:
                    return Build(name, 0.5f, new[] { new Tone(523f, 523f, 0.25f, 7f), new Tone(659f, 659f, 0.25f, 7f, 0.08f), new Tone(784f, 784f, 0.25f, 6f, 0.16f) }, 0f, 0f, 1f, false);
                case Sound.Solve:
                    return Build(name, 0.7f, new[] { new Tone(523f, 523f, 0.24f, 5f), new Tone(659f, 659f, 0.24f, 5f, 0.09f), new Tone(784f, 784f, 0.24f, 5f, 0.18f), new Tone(1047f, 1047f, 0.24f, 5f, 0.27f) }, 0f, 0f, 1f, false);
                case Sound.BossIntro:
                    return Build(name, 1.3f, new[] { new Tone(55f, 45f, 0.6f, 1.2f), new Tone(82f, 60f, 0.2f, 1.5f, 0f, true) }, 0.5f, 0f, 0.06f, true);
                case Sound.BossRage:
                    return Build(name, 0.8f, new[] { new Tone(70f, 130f, 0.35f, 2f, 0f, true), new Tone(60f, 110f, 0.5f, 2f) }, 0.45f, 0f, 0.2f, true);
                case Sound.BossSlam:
                    return Build(name, 0.5f, new[] { new Tone(90f, 30f, 0.9f, 7f) }, 0.6f, 9f, 0.12f, false);
                case Sound.BossDie:
                    return Build(name, 1.5f, new[] { new Tone(220f, 28f, 0.7f, 2f), new Tone(110f, 30f, 0.2f, 2.5f, 0f, true) }, 0.6f, 3f, 0.1f, false);
                default:
                    return null;
            }
        }

        private static AudioClip Build(string name, float seconds, Tone[] tones, float noiseVolume, float noiseDecay, float noiseColor, bool noiseSwell)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
            var data = new float[count];
            var phases = new float[tones.Length];
            // 잡음을 둔탁하게 걸러도 크기가 줄지 않게 맞춘다.
            float noiseGain = Mathf.Sqrt((2f - noiseColor) / Mathf.Max(0.001f, noiseColor));
            float low = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float sample = 0f;

                for (int k = 0; k < tones.Length; k++)
                {
                    Tone tone = tones[k];
                    float local = t - tone.Delay;
                    if (local < 0f) continue;

                    float span = Mathf.Max(0.0001f, seconds - tone.Delay);
                    float hz = tone.From * Mathf.Pow(tone.To / tone.From, Mathf.Clamp01(local / span));
                    phases[k] += hz / SampleRate;
                    if (phases[k] >= 1f) phases[k] -= 1f;

                    float wave = tone.Square ? (phases[k] < 0.5f ? 0.6f : -0.6f) : Mathf.Sin(phases[k] * 2f * Mathf.PI);
                    float attack = Mathf.Clamp01(local / 0.004f);
                    sample += wave * tone.Volume * attack * Mathf.Exp(-tone.Decay * local);
                }

                if (noiseVolume > 0f)
                {
                    low += (NextNoise() - low) * noiseColor;
                    float envelope = noiseSwell ? Mathf.Sin(i / (float)count * Mathf.PI) : Mathf.Exp(-noiseDecay * t);
                    sample += low * noiseGain * noiseVolume * envelope;
                }

                // 시작과 끝을 부드럽게 해서 딱딱 끊기는 소리를 막는다.
                float edge = Mathf.Clamp01(i / 96f) * Mathf.Clamp01((count - 1 - i) / 256f);
                data[i] = Mathf.Clamp(sample * edge, -1f, 1f);
            }

            return ToClip(name, data);
        }

        /// <summary>
        /// 보스전 음악 한 마디(2.4초, 네 박자). 이음매 없이 반복되도록 모든 음이 마디 길이에 딱 맞게 돈다.
        /// </summary>
        private static AudioClip BuildMusic()
        {
            int count = Mathf.RoundToInt(MusicLength * SampleRate);
            var data = new float[count];
            float[] bassNotes = { 110f, 110f, 130.81f, 103.83f };

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                int beat = Mathf.Min(bassNotes.Length - 1, Mathf.FloorToInt(t / MusicBeat));
                float inBeat = t - beat * MusicBeat;

                // 심장 박동: 박자마다 큰 "쿵", 조금 뒤에 작은 "쿵".
                float sample = Kick(inBeat) * 0.8f + Kick(inBeat - 0.22f) * 0.45f;

                // 낮게 깔리는 두 음이 서로 울렁인다.
                sample += 0.16f * Mathf.Sin(2f * Mathf.PI * 55f * t) + 0.12f * Mathf.Sin(2f * Mathf.PI * 57.5f * t);

                // 박자마다 한 번씩 울리는 저음.
                float bassPhase = 2f * Mathf.PI * bassNotes[beat] * inBeat;
                float bassEnvelope = Mathf.Clamp01(inBeat / 0.005f) * Mathf.Exp(-inBeat * 5f) * (1f - inBeat / MusicBeat);
                sample += 0.14f * (Mathf.Sin(bassPhase) + 0.5f * Mathf.Sin(bassPhase * 2f)) * bassEnvelope;

                data[i] = Mathf.Clamp(sample * 0.7f, -1f, 1f);
            }

            return ToClip("BossMusic", data);
        }

        /// <summary>북소리 하나. local은 치고 나서 지난 시간(초)이고, 음수면 아직 치지 않은 것이다.</summary>
        private static float Kick(float local)
        {
            if (local < 0f) return 0f;

            // 높낮이가 120Hz에서 50Hz로 빠르게 떨어진다.
            float phase = 2f * Mathf.PI * (50f * local + 70f * (1f - Mathf.Exp(-30f * local)) / 30f);
            return Mathf.Sin(phase) * Mathf.Exp(-local * 14f);
        }

        private static AudioClip ToClip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>-1에서 1 사이의 잡음. 게임의 무작위 값과 섞이지 않게 따로 계산한다.</summary>
        private static float NextNoise()
        {
            _noiseState = _noiseState * 1664525u + 1013904223u;
            return ((_noiseState >> 8) & 0xFFFFu) / 32768f - 1f;
        }
    }
}
