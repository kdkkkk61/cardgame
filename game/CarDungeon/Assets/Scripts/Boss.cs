using UnityEngine;

namespace CarDungeon
{
    /// <summary>
    /// Slice 1: 제자리 보스. HP만 보유, 사이클 종료 시 패턴 A(자동명중) 발동.
    /// Slice 2에서 추적/정주 이동 + 패턴 B(바닥 장판) 추가 예정.
    /// </summary>
    public class Boss : MonoBehaviour
    {
        public int maxHP = 600;   // 임시 — 전투 3~5분 되도록 튜닝(MAGE_PROTOTYPE 4)
        public int hp;

        public float slowTimer = 0f; // 냉기폭발/메테오 슬로우 잔여(초)

        SpriteRenderer _sr;
        Color _baseColor;
        float _flash = 0f;

        void Awake()
        {
            hp = maxHP;
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _baseColor = _sr.color;
        }

        void Update()
        {
            if (slowTimer > 0f) slowTimer -= Time.deltaTime;
            if (_flash > 0f && _sr != null)
            {
                _flash -= Time.deltaTime * 4f;
                _sr.color = Color.Lerp(_baseColor, Color.white, Mathf.Clamp01(_flash));
            }
        }

        public void Flash() { _flash = 1f; }

        public void TakeDamage(int dmg)
        {
            hp = Mathf.Max(0, hp - dmg);
        }

        public void ApplySlow(float seconds)
        {
            slowTimer = Mathf.Max(slowTimer, seconds);
        }

        public bool IsDead => hp <= 0;
        public float HpRatio => maxHP > 0 ? (float)hp / maxHP : 0f;
    }
}
