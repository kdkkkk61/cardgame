using UnityEngine;

namespace CarDungeon
{
    /// <summary>
    /// Zero-Wiring 부트스트랩: 어떤 씬이든 Play만 누르면 전투 환경을 런타임 생성.
    /// 에디터에서 씬 조립이 필요 없음 → 원격(코드)만으로 개발 가능.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            // 이미 생성됐으면 스킵(중복 방지)
            if (Object.FindAnyObjectByType<CombatManager>() != null) return;

            // --- 카메라 (탑다운 직교) ---
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.backgroundColor = new Color(0.11f, 0.12f, 0.15f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            // --- 아레나 바닥 ---
            ProtoSprites.Make("Arena", ProtoSprites.Square(),
                new Color(0.16f, 0.17f, 0.21f), new Vector3(0, 0, 1), 1f, -10)
                .transform.localScale = new Vector3(16.5f, 9.2f, 1f);

            // --- 플레이어 (마법사) — 목업 보라 톤 ---
            var playerGo = ProtoSprites.Make("Player", ProtoSprites.Circle(),
                new Color(0.62f, 0.52f, 0.88f), new Vector3(0, -2f, 0), 0.62f, 5);
            var player = playerGo.AddComponent<PlayerController>();

            // --- 보스 — 목업 빨강 톤 ---
            var bossGo = ProtoSprites.Make("Boss", ProtoSprites.Square(),
                new Color(0.75f, 0.32f, 0.31f), new Vector3(0, 2.8f, 0), 1.1f, 5);
            var boss = bossGo.AddComponent<Boss>();

            // --- 매니저 + HUD ---
            var mgrGo = new GameObject("CombatManager");
            var mgr = mgrGo.AddComponent<CombatManager>();
            var hud = mgrGo.AddComponent<CombatHUD>();
            hud.mgr = mgr;
            mgr.Init(player, boss);

            Debug.Log("[CarDungeon] 부트스트랩 완료 — Slice 1 코어 루프");
        }
    }
}
