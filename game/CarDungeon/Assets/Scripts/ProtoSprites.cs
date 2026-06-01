using UnityEngine;

namespace CarDungeon
{
    /// <summary>
    /// 에셋 없이 런타임에 단색 스프라이트를 생성하는 헬퍼.
    /// 프로토타입용 — 나중에 실제 픽셀 아트로 교체(ART_GUIDE 참조).
    /// </summary>
    public static class ProtoSprites
    {
        private static Sprite _square;
        private static Sprite _circle;

        public static Sprite Square()
        {
            if (_square == null)
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var px = new Color[16];
                for (int i = 0; i < 16; i++) px[i] = Color.white;
                tex.SetPixels(px); tex.Apply();
                tex.filterMode = FilterMode.Point;
                _square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
            }
            return _square;
        }

        public static Sprite Circle()
        {
            if (_circle == null)
            {
                int s = 64;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
                float r = s / 2f;
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x - r + 0.5f, dy = y - r + 0.5f;
                    bool inside = dx * dx + dy * dy <= (r - 1f) * (r - 1f);
                    tex.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
                tex.Apply();
                _circle = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
            }
            return _circle;
        }

        /// <summary>월드에 색칠된 스프라이트 오브젝트 생성.</summary>
        public static GameObject Make(string name, Sprite sprite, Color color,
            Vector3 pos, float scale, int sortOrder)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortOrder;
            return go;
        }
    }
}
