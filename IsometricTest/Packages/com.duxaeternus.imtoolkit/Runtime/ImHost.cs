using System;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.UIElements;

namespace ImToolkit
{
    /// <summary>
    /// Auto-created runtime host: owns the UIDocument all IM.* calls render into and pumps the
    /// end-of-frame reconcile via the player loop. Never add this manually.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class ImHost : MonoBehaviour
    {
        static ImHost s_Instance;
        static bool s_LoopHooked;

        PanelSettings m_Settings;
        UIDocument m_Document;

        internal static void Ensure()
        {
            if (s_Instance == null)
            {
                var go = new GameObject("[IM Toolkit]");
                DontDestroyOnLoad(go);
                s_Instance = go.AddComponent<ImHost>();
                s_Instance.Initialize();
            }
            HookLoop();
        }

        void Initialize()
        {
            m_Settings = ScriptableObject.CreateInstance<PanelSettings>();
            m_Settings.name = "[IM Toolkit] PanelSettings";
            var theme = Resources.Load<ThemeStyleSheet>("ImToolkit/ImToolkitTheme");
            if (theme != null)
                m_Settings.themeStyleSheet = theme;
            else
                Debug.LogWarning("[IM Toolkit] Theme not found in package Resources; controls will be unstyled.");
            m_Settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            m_Settings.scale = IM.Scale;
            m_Settings.sortingOrder = IM.SortingOrder;

            m_Document = gameObject.AddComponent<UIDocument>();
            m_Document.panelSettings = m_Settings;

            var root = m_Document.rootVisualElement;
            root.name = "im-root";
            root.AddToClassList(ImStyles.Root);
            root.pickingMode = PickingMode.Ignore; // clicks on empty space reach the game
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.top = 0;
            root.style.right = 0;
            root.style.bottom = 0;

            var sheet = Resources.Load<StyleSheet>("ImToolkit/ImToolkit");
            if (sheet != null)
                root.styleSheets.Add(sheet);

            ImContext.SetLayer(root);
        }

        internal static void ApplyScale(float scale)
        {
            if (s_Instance != null && s_Instance.m_Settings != null)
                s_Instance.m_Settings.scale = scale;
        }

        internal static void ApplySortingOrder(float order)
        {
            if (s_Instance != null && s_Instance.m_Settings != null)
                s_Instance.m_Settings.sortingOrder = order;
        }

        // ---- player loop -----------------------------------------------------
        // EndFrame is inserted at the start of PostLateUpdate: after every script's
        // Update/LateUpdate has drawn, before UI Toolkit lays out and renders.

        static void HookLoop()
        {
            if (s_LoopHooked)
                return;

            var loop = PlayerLoop.GetCurrentPlayerLoop();
            for (int i = 0; i < loop.subSystemList.Length; i++)
            {
                if (loop.subSystemList[i].type != typeof(PostLateUpdate))
                    continue;

                var subs = loop.subSystemList[i].subSystemList ?? Array.Empty<PlayerLoopSystem>();
                foreach (var sub in subs)
                {
                    if (sub.type == typeof(ImHost)) // already hooked (domain reload disabled)
                    {
                        s_LoopHooked = true;
                        return;
                    }
                }

                var expanded = new PlayerLoopSystem[subs.Length + 1];
                expanded[0] = new PlayerLoopSystem { type = typeof(ImHost), updateDelegate = ImContext.EndFrame };
                Array.Copy(subs, 0, expanded, 1, subs.Length);
                loop.subSystemList[i].subSystemList = expanded;
                break;
            }
            PlayerLoop.SetPlayerLoop(loop);
            s_LoopHooked = true;
        }

        static void UnhookLoop()
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            bool changed = false;
            for (int i = 0; i < loop.subSystemList.Length; i++)
            {
                if (loop.subSystemList[i].type != typeof(PostLateUpdate) || loop.subSystemList[i].subSystemList == null)
                    continue;
                var subs = loop.subSystemList[i].subSystemList;
                var filtered = Array.FindAll(subs, s => s.type != typeof(ImHost));
                if (filtered.Length != subs.Length)
                {
                    loop.subSystemList[i].subSystemList = filtered;
                    changed = true;
                }
            }
            if (changed)
                PlayerLoop.SetPlayerLoop(loop);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Instance = null;
            UnhookLoop(); // strip stale entry when domain reload is disabled
            s_LoopHooked = false;
            ImContext.ResetStatics();
        }

        void OnDestroy()
        {
            if (s_Instance != this)
                return;
            s_Instance = null;
            UnhookLoop();
            s_LoopHooked = false;
            ImContext.ClearAll(); // retained elements belong to this panel; rebuild fresh if used again
            ImContext.SetLayer(null);
            if (m_Settings != null)
                Destroy(m_Settings);
        }
    }
}
