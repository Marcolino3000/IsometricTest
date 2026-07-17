# IM Toolkit

Immediate-mode UI on top of **UI Toolkit** — a drop-in replacement for `OnGUI`/`GUILayout`
debug overlays. Call `IM.*` from any `Update()`; the first call creates a hidden host with a
runtime panel, and each frame your calls are reconciled into a retained `VisualElement` tree.

No UXML, no USS, no PanelSettings asset, no scene setup.

```csharp
using ImToolkit;

public class DebugOverlay : MonoBehaviour
{
    float m_Speed = 4f;

    void Update()
    {
        using (IM.Window("Debug"))
        {
            IM.Label($"FPS: {1f / Time.smoothDeltaTime:0}");
            m_Speed = IM.Slider("Speed", m_Speed, 0f, 10f);
            if (IM.Button("Reset"))
                m_Speed = 4f;
        }
    }
}
```

## Migrating from IMGUI

| IMGUI (`OnGUI`) | IM Toolkit (any `Update`) |
| --- | --- |
| `GUILayout.Label(text)` | `IM.Label(text)` |
| `if (GUILayout.Button("X"))` | `if (IM.Button("X"))` |
| `GUILayout.Toggle(v, "X")` | `v = IM.Toggle("X", v)` |
| `GUILayout.HorizontalSlider(v, 0, 1)` | `v = IM.Slider("X", v, 0f, 1f)` |
| `GUILayout.TextField(s)` | `s = IM.TextField("X", s)` |
| `GUILayout.BeginHorizontal()` … `EndHorizontal()` | `using (IM.Row()) { … }` |
| `GUILayout.BeginScrollView` | `using (IM.Scroll(maxHeight: 200)) { … }` |
| `GUILayout.Window(id, rect, fn, "Title")` | `using (IM.Window("Title")) { … }` |
| `GUILayout.Space(8)` / `FlexibleSpace()` | `IM.Space(8)` / `IM.FlexibleSpace()` |
| `EditorGUILayout.Foldout` | `if (IM.Foldout("Section")) { … }` |

Differences worth knowing:

- **Clicks report on the frame after the press** (one-frame latency, same as any
  immediate-over-retained bridge). Irrelevant for debug UI.
- **Widgets keep retained state for free**: foldout open/closed, scroll position, text cursor
  and window drag positions survive because the underlying elements persist across frames.
- **Stop calling = disappears.** A window that isn't drawn this frame is hidden (state kept),
  exactly like IMGUI semantics.

## API

**Display** — `Label`, `Header`, `Image(texture)`, `ProgressBar(value, title, min, max)`,
`Space(px)`, `FlexibleSpace()`, `Separator()`

**Input** — `Button(text)`, `Toggle(label, value)`, `Foldout(label)`,
`Slider(label, float|int, min, max)`, `TextField(label, value, multiline)`,
`FloatField(label, value)`, `IntField(label, value)`,
`Dropdown(label, index, options)`, `EnumPopup(label, enumValue)`

**Layout scopes** (use with `using`) — `Window(title)`, `Row()`, `Column()`,
`Box(title)`, `Scroll(maxHeight)`, `Indent()`

**Config** — `IM.Scale` (overlay zoom), `IM.SortingOrder` (default 100, above game UI),
`IM.Clear()` (drop all retained windows/state)

Widgets drawn outside a `Window` scope land in a plain top-left panel, like bare `GUILayout`
calls. Windows are draggable by their title bar, come to the front when clicked, and are keyed
by title — pass the optional `id:` argument when the title text is dynamic. The same applies to
`Foldout` and to value fields whose position in the call order changes between frames.

## How it works

Each `IM.*` call advances a cursor through a retained node list per container. Matching kind
(and id) at the cursor → the existing element is reused and updated; a match further ahead →
skipped nodes are removed (widgets that stopped being drawn); no match → a new element is
created at the cursor. At the end of the frame (a player-loop hook after all `Update`/
`LateUpdate` calls, before UI Toolkit renders), leftover nodes are pruned and anything not
drawn this frame is hidden.

Value fields are two-way: a user edit wins for that frame and is returned to the caller; when
your code passes a different value than it last passed, it is pushed into the field without
triggering events.

## Notes & limits

- Runtime only (play mode). Calls in edit mode log a single warning and no-op.
- Call from `Update`/`LateUpdate` (any script, any order). Coroutine draws after
  `WaitForEndOfFrame` arrive too late for that frame's reconcile.
- Style overrides: target the `.im-*` USS classes (see `Runtime/Resources/ImToolkit/ImToolkit.uss`)
  from your own theme, or restyle standard controls under `.im-root`.
- Requires Unity 6+ (runtime `FloatField`/`DropdownField` etc.). Input works with both the
  Input System package and legacy input — UI Toolkit runtime panels handle this natively.
