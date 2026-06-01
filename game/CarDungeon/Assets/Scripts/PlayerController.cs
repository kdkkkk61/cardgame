using UnityEngine;
using UnityEngine.InputSystem;

namespace CarDungeon
{
    /// <summary>
    /// 탑다운 WASD 이동. 마법사는 필중이라 무빙 = 거리 조절 / 회피 전용
    /// (CORE_COMBAT 섹션 6). 아레나 밖으로 못 나가게 클램프.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public float speed = 5f;
        public Vector2 arenaHalf = new Vector2(7.5f, 4.2f);
        public bool movementLocked = false; // 조준 중 무빙 불가(Slice 2)

        void Update()
        {
            if (movementLocked) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            Vector2 dir = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir.y += 1;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir.y -= 1;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir.x -= 1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1;

            if (dir.sqrMagnitude > 1f) dir.Normalize();
            Vector3 p = transform.position + (Vector3)(dir * speed * Time.deltaTime);
            p.x = Mathf.Clamp(p.x, -arenaHalf.x, arenaHalf.x);
            p.y = Mathf.Clamp(p.y, -arenaHalf.y, arenaHalf.y);
            transform.position = p;
        }
    }
}
