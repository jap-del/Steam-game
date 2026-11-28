using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 그림 판의 칸을 넘겨 캐릭터의 움직임을 보여 준다. 붙은 스프라이트의 그림만 바꾸고,
    /// 색과 보이기 여부, 좌우 뒤집기는 원래 코드가 정하던 대로 둔다.
    /// 새 그림은 색이 칠해져 있어서 스프라이트 색은 흰색이면 그대로, 회색이면 어둡게 보인다.
    /// 맞았을 때나 공격 예고의 번쩍임은 같은 모양의 흰 실루엣을 위에 겹쳐 SetFlash의 색으로 칠한다.
    /// </summary>
    public sealed class PixelRig : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private SpriteRenderer _flash;
        private string _sheetName;
        private ArtSheet _sheet;
        private ArtClip _clip;
        private bool _hasClip;
        private bool _manualFrame;
        private float _clipTime;
        private int _frame;
        private Color _flashColor = Color.clear;

        /// <summary>지금 보이는 칸. 분신이 플레이어와 같은 칸을 보여 줄 때 쓴다.</summary>
        public int Frame => _frame;

        /// <summary>게임 시간이 멈춰 있어도 움직이게 실제 시간을 쓴다. 보스 등장 연출 동안 켠다.</summary>
        public bool UnscaledTime { get; set; }

        /// <summary>
        /// renderer를 그림 판 sheetName의 움직이는 그림으로 바꾼다. 그림의 발밑이 몸 충돌체(bodySize)의 바닥 가운데에 온다.
        /// 그림 판이 없으면 아무것도 바꾸지 않고 null을 돌려준다.
        /// </summary>
        public static PixelRig Attach(SpriteRenderer renderer, string sheetName, Vector2 bodySize)
        {
            if (renderer == null) return null;
            ArtSheet sheet = ArtLibrary.Get(sheetName);
            if (sheet == null || sheet.FrameCount < 2) return null;

            var rig = renderer.gameObject.AddComponent<PixelRig>();
            rig.Setup(renderer, sheetName, sheet, bodySize);
            return rig;
        }

        private void Setup(SpriteRenderer renderer, string sheetName, ArtSheet sheet, Vector2 bodySize)
        {
            _renderer = renderer;
            _sheetName = sheetName;
            _sheet = sheet;

            Transform target = renderer.transform;
            target.localScale = Vector3.one;
            Vector3 position = target.localPosition;
            target.localPosition = new Vector3(position.x, position.y - bodySize.y * 0.5f, position.z);
            renderer.color = Color.white;

            if (sheet.HasSilhouettes)
            {
                // 번쩍임 층은 몸 그림의 자식이라 좌우 뒤집기를 그대로 따라간다.
                var go = new GameObject("Flash");
                go.transform.SetParent(target, false);
                _flash = go.AddComponent<SpriteRenderer>();
                _flash.sortingOrder = renderer.sortingOrder + 1;
                _flash.sharedMaterial = renderer.sharedMaterial;
                _flash.enabled = false;
            }

            Begin("idle");
            Show();
        }

        /// <summary>움직임을 바꾼다. 이미 그 움직임이면 이어서 보여 준다. 없는 이름이면 서 있는 그림을 쓴다.</summary>
        public void Play(string clipName)
        {
            if (_hasClip && !_manualFrame && _clip.Name == clipName) return;
            Begin(clipName);
        }

        /// <summary>같은 움직임이라도 첫 칸부터 다시 보여 준다.</summary>
        public void Restart(string clipName)
        {
            Begin(clipName);
        }

        /// <summary>움직임 대신 정해진 칸을 보여 준다. 다음에 Play를 부르면 다시 움직인다.</summary>
        public void ShowFrame(int frame)
        {
            if (_sheet == null) return;
            _manualFrame = true;
            _frame = Mathf.Clamp(frame, 0, _sheet.FrameCount - 1);
        }

        /// <summary>실루엣 번쩍임의 색. 알파가 세기다. Color.clear면 끈다.</summary>
        public void SetFlash(Color color)
        {
            _flashColor = color;
        }

        private void Begin(string clipName)
        {
            if (_sheet == null) return;

            ArtClip clip;
            if (!_sheet.TryGetClip(clipName, out clip))
            {
                if (!_sheet.TryGetClip("idle", out clip)) return;
                // 없는 움직임을 달라고 했는데 이미 서 있는 그림이면 처음으로 돌리지 않는다.
                if (_hasClip && !_manualFrame && _clip.Name == clip.Name) return;
            }

            _clip = clip;
            _hasClip = true;
            _manualFrame = false;
            _clipTime = 0f;
            _frame = clip.Start;
        }

        private void LateUpdate()
        {
            if (_renderer == null) return;

            if (_sheet == null || !_sheet.IsAlive)
            {
                // 플레이를 멈췄다 다시 켜는 사이에 그림이 사라졌으면 다시 읽는다.
                _sheet = ArtLibrary.Get(_sheetName);
                if (_sheet == null) return;
            }

            if (_hasClip && !_manualFrame)
            {
                _clipTime += UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                int step = Mathf.FloorToInt(_clipTime * _clip.Fps);
                int index = _clip.Loop ? step % _clip.Count : Mathf.Min(step, _clip.Count - 1);
                _frame = _clip.Start + index;
            }
            Show();
        }

        private void Show()
        {
            _renderer.sprite = _sheet.Frame(_frame);
            if (_flash == null) return;

            bool visible = _renderer.enabled && _flashColor.a > 0.01f;
            _flash.enabled = visible;
            if (!visible) return;

            _flash.sprite = _sheet.Silhouette(_frame);
            Color color = _flashColor;
            color.a *= _renderer.color.a;
            _flash.color = color;
        }
    }
}
