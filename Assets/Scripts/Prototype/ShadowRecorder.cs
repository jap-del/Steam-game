using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>한 물리 틱 동안의 플레이어 상태. 분신이 그대로 재현한다.</summary>
    public struct ShadowFrame
    {
        public Vector2 Position;
        public int Facing;
        public bool Attacked;

        public ShadowFrame(Vector2 position, int facing, bool attacked)
        {
            Position = position;
            Facing = facing;
            Attacked = attacked;
        }
    }

    /// <summary>
    /// 플레이어 상태를 매 틱 기록하고, 정해진 틱 수만큼 지난 상태를 돌려주는 원형 버퍼.
    /// 유니티 오브젝트에 의존하지 않아 따로 테스트할 수 있다.
    /// </summary>
    public sealed class ShadowRecorder
    {
        private readonly ShadowFrame[] _frames;
        private int _head;
        private int _count;

        public ShadowRecorder(int delayTicks)
        {
            _frames = new ShadowFrame[Mathf.Max(1, delayTicks)];
        }

        public int DelayTicks => _frames.Length;

        /// <summary>분신이 나타나기까지 기록이 얼마나 찼는지 (0~1).</summary>
        public float Fill => (float)_count / _frames.Length;

        /// <summary>
        /// 현재 상태를 기록한다. 기록이 지연 시간만큼 쌓였으면 그만큼 과거의 상태를 돌려주고 true를 반환한다.
        /// </summary>
        public bool Push(ShadowFrame current, out ShadowFrame delayed)
        {
            if (_count < _frames.Length)
            {
                _frames[(_head + _count) % _frames.Length] = current;
                _count++;
                delayed = default;
                return false;
            }

            delayed = _frames[_head];
            _frames[_head] = current;
            _head = (_head + 1) % _frames.Length;
            return true;
        }

        /// <summary>
        /// 분신이 ticksAhead 틱 뒤에 재현할 상태를 미리 본다. 0이면 바로 다음에 재현할 상태다.
        /// 기록이 아직 다 차지 않았으면 false.
        /// </summary>
        public bool TryPeek(int ticksAhead, out ShadowFrame frame)
        {
            if (_count < _frames.Length || ticksAhead < 0 || ticksAhead >= _frames.Length)
            {
                frame = default;
                return false;
            }

            frame = _frames[(_head + ticksAhead) % _frames.Length];
            return true;
        }

        public void Clear()
        {
            _head = 0;
            _count = 0;
        }
    }
}
