using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 덧그림(눈, 목도리)이 바탕 그림을 따라 켜지고 꺼지고 투명해지게 한다.
    /// 색은 따라가지 않는다. 그래서 적이 노랗게 깜빡여도 눈빛은 그대로다.
    /// </summary>
    public sealed class SpriteOverlay : MonoBehaviour
    {
        private SpriteRenderer _source;
        private SpriteRenderer _own;

        public void Follow(SpriteRenderer source, SpriteRenderer own)
        {
            _source = source;
            _own = own;
            Sync();
        }

        private void LateUpdate()
        {
            Sync();
        }

        private void Sync()
        {
            if (_source == null || _own == null) return;

            _own.enabled = _source.enabled;
            Color color = _own.color;
            color.a = _source.color.a;
            _own.color = color;
        }
    }
}
