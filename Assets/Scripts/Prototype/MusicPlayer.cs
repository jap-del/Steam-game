using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>틀 수 있는 배경음악. 파일 이름은 Assets/Resources/Audio/Music_이름.ogg.</summary>
    public enum MusicTrack
    {
        None,
        Hub,
        Citadel,
        Sanctum,
        Clocktower,
        Boss,
    }

    /// <summary>
    /// 배경음악과 보스 등장 효과음. Assets/Resources/Audio의 음악 파일(Tools/MusicGen이 만든다)을 반복해서 튼다.
    /// 곡을 바꿀 때는 앞 곡이 작아지면서 새 곡이 커진다. 보스전 음악은 보통 판과 분노 판 두 곡을 같은 박자로
    /// 함께 틀어 두고, 보스가 다음 단계로 넘어가면 분노 판으로 넘어간다. 게임이 멈춰 있어도 실제 시간으로 움직인다.
    /// 음악 파일이 없으면 아무것도 틀지 않고 false를 돌려준다(보스전은 Sfx의 예전 음악을 쓴다).
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        /// <summary>음악 음량. 효과음보다 뒤에 깔리도록 낮게 둔다. Sfx.MasterVolume이 곱해진다.</summary>
        public const float Volume = 0.6f;
        private const float StingVolume = 0.9f;
        private const float DefaultFade = 1.2f;
        private const float RageFade = 1.5f;
        private const string Folder = "Audio/";

        private static MusicPlayer _instance;

        private AudioSource[] _decks;
        private float[] _level;
        private float[] _target;
        private float[] _speed;
        private MusicTrack[] _deckTrack;
        private int _active;
        /// <summary>분노 판. 보스전 음악이 있는 자리(_rageDeck)와 같은 크기로 줄고 커지며, _rageMix만큼 섞인다.</summary>
        private AudioSource _rage;
        private int _rageDeck = -1;
        private float _rageMix;
        private float _rageTarget;
        private AudioSource _sting;
        private float _stingLevel;
        private float _stingTarget;
        private float _stingFadeSpeed = 4f;
        /// <summary>예약해 둔 곡이 막 시작하는 동안에는 "멈췄다"고 보지 않는다.</summary>
        private float _graceUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        /// <summary>Play를 시작할 때 한 번 부른다. host에 음악을 내는 부품을 붙이고 음악 파일을 미리 읽어 둔다.</summary>
        public static void Attach(GameObject host)
        {
            _instance = host.AddComponent<MusicPlayer>();
            _instance.Build();
        }

        /// <summary>지금 틀고 있는 곡. 작아지며 사라지는 중인 곡은 치지 않는다.</summary>
        public static MusicTrack Current
        {
            get
            {
                if (_instance == null) return MusicTrack.None;
                int a = _instance._active;
                bool playing = _instance._decks[a].isPlaying || Time.unscaledTime < _instance._graceUntil;
                return _instance._target[a] > 0f && playing ? _instance._deckTrack[a] : MusicTrack.None;
            }
        }

        /// <summary>
        /// 곡을 튼다. 이미 그 곡이면 이어서 튼다. fade초 동안 앞 곡이 작아지고 새 곡이 커진다(0이면 바로).
        /// 그 곡의 파일이 없으면 지금 곡을 끄고 false를 돌려준다.
        /// </summary>
        public static bool Play(MusicTrack track, float fade = DefaultFade)
        {
            if (_instance == null) return false;
            return _instance.PlayTrack(track, fade);
        }

        /// <summary>음악을 fade초 동안 줄이며 끈다.</summary>
        public static void Stop(float fade = DefaultFade)
        {
            if (_instance != null) _instance.FadeOutAll(fade);
        }

        /// <summary>보스전 음악이 나오는 중이면 끈다. 다른 곡은 그대로 둔다.</summary>
        public static void StopBoss(float fade)
        {
            if (_instance != null && Current == MusicTrack.Boss) _instance.FadeOutAll(fade);
        }

        /// <summary>보스가 다음 단계로 넘어갔다. 같은 자리의 분노 판으로 넘어간다.</summary>
        public static bool SetBossRage(bool rage)
        {
            if (_instance == null || _instance._rage.clip == null || Current != MusicTrack.Boss) return false;
            _instance._rageTarget = rage ? 1f : 0f;
            return true;
        }

        /// <summary>보스 등장 효과음을 튼다. 배경음악은 빠르게 줄인다. 파일이 없으면 false.</summary>
        public static bool PlayBossSting()
        {
            if (_instance == null) return false;
            AudioClip clip = Load("Sting_BossIntro");
            if (clip == null) return false;
            _instance.FadeOutAll(0.4f);
            AudioSource sting = _instance._sting;
            sting.clip = clip;
            sting.time = 0f;
            _instance._stingLevel = 1f;
            _instance._stingTarget = 1f;
            _instance.Apply();
            sting.Play();
            return true;
        }

        /// <summary>등장 연출을 건너뛰었으면 효과음을 빠르게 줄여 끈다.</summary>
        public static void FadeBossSting(float fade)
        {
            if (_instance == null || !_instance._sting.isPlaying) return;
            _instance._stingTarget = 0f;
            _instance._stingFadeSpeed = 1f / Mathf.Max(0.01f, fade);
        }

        private void Build()
        {
            _decks = new AudioSource[2];
            _level = new float[2];
            _target = new float[2];
            _speed = new float[2];
            _deckTrack = new MusicTrack[2];
            for (int i = 0; i < 2; i++)
            {
                _decks[i] = NewSource(true);
            }
            _rage = NewSource(true);
            _sting = NewSource(false);

            // 처음 틀 때 멈칫하지 않게 미리 읽어 둔다.
            foreach (MusicTrack track in System.Enum.GetValues(typeof(MusicTrack)))
            {
                if (track != MusicTrack.None) Load(FileName(track));
            }
            Load("Music_BossRage");
            Load("Sting_BossIntro");
        }

        private AudioSource NewSource(bool loop)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.volume = 0f;
            // 효과음 때문에 음악이 끊기지 않게 우선순위를 높인다(숫자가 작을수록 높다).
            source.priority = 16;
            return source;
        }

        private bool PlayTrack(MusicTrack track, float fade)
        {
            if (track == MusicTrack.None)
            {
                FadeOutAll(fade);
                return true;
            }
            if (Current == track) return true;

            AudioClip clip = Load(FileName(track));
            if (clip == null)
            {
                FadeOutAll(fade);
                return false;
            }

            // 지금 곡은 작아지며 사라지고, 다른 쪽 자리에서 새 곡이 커진다.
            float speed = fade > 0f ? 1f / fade : 1000f;
            int old = _active;
            _target[old] = 0f;
            _speed[old] = speed;

            _active = 1 - old;
            _graceUntil = Time.unscaledTime + 0.25f;
            AudioSource deck = _decks[_active];
            deck.Stop();
            deck.clip = clip;
            _deckTrack[_active] = track;
            _level[_active] = fade > 0f ? 0f : 1f;
            _target[_active] = 1f;
            _speed[_active] = speed;

            if (track == MusicTrack.Boss)
            {
                // 분노 판은 같은 순간에 함께 시작해서 박자가 어긋나지 않게 한다.
                AudioClip rage = Load("Music_BossRage");
                _rage.Stop();
                _rage.clip = rage;
                _rageDeck = _active;
                _rageMix = 0f;
                _rageTarget = 0f;
                double start = AudioSettings.dspTime + 0.05;
                deck.PlayScheduled(start);
                if (rage != null) _rage.PlayScheduled(start);
            }
            else
            {
                deck.Play();
            }
            Apply();
            return true;
        }

        private void FadeOutAll(float fade)
        {
            float speed = fade > 0f ? 1f / fade : 1000f;
            for (int i = 0; i < 2; i++)
            {
                _target[i] = 0f;
                _speed[i] = speed;
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < 2; i++)
            {
                _level[i] = Mathf.MoveTowards(_level[i], _target[i], _speed[i] * dt);
                if (_level[i] <= 0f && _target[i] <= 0f && _decks[i].isPlaying && (i != _active || Time.unscaledTime >= _graceUntil)) _decks[i].Stop();
            }
            _rageMix = Mathf.MoveTowards(_rageMix, _rageTarget, dt / RageFade);
            // 보스전 음악이 꺼지면 분노 판도 함께 끈다.
            if (Time.unscaledTime >= _graceUntil && _rage.isPlaying && (_rageDeck < 0 || !_decks[_rageDeck].isPlaying || _deckTrack[_rageDeck] != MusicTrack.Boss))
            {
                _rage.Stop();
                _rageDeck = -1;
            }
            _stingLevel = Mathf.MoveTowards(_stingLevel, _stingTarget, _stingFadeSpeed * dt);
            if (_stingLevel <= 0f && _sting.isPlaying) _sting.Stop();
            Apply();
        }

        private void Apply()
        {
            float volume = Volume * Sfx.MasterVolume;
            for (int i = 0; i < 2; i++)
            {
                float mix = i == _rageDeck ? 1f - _rageMix : 1f;
                _decks[i].volume = volume * _level[i] * mix;
            }
            _rage.volume = _rageDeck >= 0 ? volume * _level[_rageDeck] * _rageMix : 0f;
            _sting.volume = StingVolume * Sfx.MasterVolume * _stingLevel;
        }

        private static string FileName(MusicTrack track)
        {
            return "Music_" + track;
        }

        private static AudioClip Load(string name)
        {
            return Resources.Load<AudioClip>(Folder + name);
        }
    }
}
