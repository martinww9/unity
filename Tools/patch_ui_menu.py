#!/usr/bin/env python3
"""Patch UI.unity: unify menu canvas hierarchy and Spawner references."""
from pathlib import Path

SCENE = Path(__file__).resolve().parents[1] / "Assets/Scenes/UI/UI.unity"

# fileIDs
RT_CANVAS_MENU = "294713342"
RT_PANEL_INICIO = "1578197412"
RT_PANEL_BROWSER = "1118614769"
RT_PANEL_CREAR = "655270624"
RT_CANVAS_BROWSER = "810832675"
RT_CANVAS_CREAR = "1124320186"
GO_CANVAS_MENU = "294713341"
GO_PANEL_INICIO = "1578197411"
GO_PANEL_BROWSER = "1118614768"
GO_PANEL_CREAR = "655270623"
GO_CANVAS_BROWSER = "810832671"
GO_CANVAS_CREAR = "1124320182"
GO_LOBBYS = "560874745"
RT_CONTENT = "1273758043"
SPAWNER = "834509285"

# Colors
PANEL_SURFACE = "m_Color: {r: 0.141, g: 0.204, b: 0.278, a: 1}"
BROWSER_PANEL_OLD = "m_Color: {r: 1, g: 1, b: 1, a: 0.392}"


def patch_children_block(text: str, rt_id: str, new_children: list[str]) -> str:
    marker = f"--- !u!224 &{rt_id}"
    start = text.find(marker)
    if start < 0:
        raise RuntimeError(f"RectTransform {rt_id} not found")
    child_start = text.find("  m_Children:\n", start)
    if child_start < 0:
        raise RuntimeError(f"m_Children for {rt_id} not found")
    child_end = text.find("\n  m_Father:", child_start)
    lines = ["  m_Children:"] + [f"  - {{fileID: {c}}}" for c in new_children]
    return text[:child_start] + "\n".join(lines) + text[child_end:]


def patch_father(text: str, rt_id: str, father_id: str) -> str:
    marker = f"--- !u!224 &{rt_id}"
    start = text.find(marker)
    if start < 0:
        raise RuntimeError(f"RectTransform {rt_id} not found")
    old = text.find("  m_Father:", start)
    end = text.find("\n", old)
    return text[:old] + f"  m_Father: {{fileID: {father_id}}}" + text[end:]


def main():
    text = SCENE.read_text(encoding="utf-8")

    text = text.replace("m_Name: CanvasInicio", "m_Name: CanvasMenu", 1)

    # CanvasMenu children: PanelInicio, PanelBrowser, PanelCrearSala (background added in Editor script)
    text = patch_children_block(
        text, RT_CANVAS_MENU, [RT_PANEL_INICIO, RT_PANEL_BROWSER, RT_PANEL_CREAR]
    )
    text = patch_father(text, RT_PANEL_BROWSER, RT_CANVAS_MENU)
    text = patch_father(text, RT_PANEL_CREAR, RT_CANVAS_MENU)

    text = patch_children_block(text, RT_CANVAS_BROWSER, [])
    text = patch_children_block(text, RT_CANVAS_CREAR, [])

    # Hide extra canvases and secondary panels by default
    for go_id in (GO_CANVAS_BROWSER, GO_CANVAS_CREAR, GO_PANEL_BROWSER, GO_PANEL_CREAR):
        text = text.replace(
            f"m_GameObject: {{fileID: {go_id}}}\n  m_Enabled: 1",
            f"m_GameObject: {{fileID: {go_id}}}\n  m_Enabled: 1",
        )
    for pat in [
        (GO_CANVAS_BROWSER, "  m_IsActive: 1\n", "  m_IsActive: 0\n"),
        (GO_CANVAS_CREAR, "  m_IsActive: 1\n", "  m_IsActive: 0\n"),
        (GO_PANEL_BROWSER, "  m_IsActive: 1\n", "  m_IsActive: 0\n", 1),
        (GO_PANEL_CREAR, "  m_IsActive: 1\n", "  m_IsActive: 0\n", 1),
    ]:
        go, old, new = pat[0], pat[1], pat[2]
        idx = text.find(f"--- !u!1 &{go_id if (go_id := go) else go}")
        # simpler: only first PanelBrowser active flag in its GameObject block
    # Direct replacements on GameObject blocks
    text = text.replace(
        f"--- !u!1 &{GO_CANVAS_BROWSER}\nGameObject:",
        f"--- !u!1 &{GO_CANVAS_BROWSER}\nGameObject:",
    )
    blocks = [
        (f"m_Name: CanvasBrowser\n", f"m_Name: CanvasBrowser\n"),
    ]
    text = text.replace(
        f"  m_Name: CanvasBrowser\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1",
        f"  m_Name: CanvasBrowser\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 0",
    )
    text = text.replace(
        f"  m_Name: CanvasCrearSala\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1",
        f"  m_Name: CanvasCrearSala\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 0",
    )
    # PanelBrowser inactive (second occurrence after CanvasBrowser)
    text = text.replace(
        f"  m_Name: PanelBrowser\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1",
        f"  m_Name: PanelBrowser\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 0",
        1,
    )
    text = text.replace(
        f"  m_Name: PanelCrearSala\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1",
        f"  m_Name: PanelCrearSala\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 0",
        1,
    )

    # Scene roots: remove old canvas roots
    text = text.replace(
        "  m_Roots:\n  - {fileID: 832575519}\n  - {fileID: 1890316663}\n  - {fileID: 834509284}\n  - {fileID: 2074246885}\n  - {fileID: 810832675}\n  - {fileID: 294713342}\n  - {fileID: 1124320186}\n",
        "  m_Roots:\n  - {fileID: 832575519}\n  - {fileID: 1890316663}\n  - {fileID: 834509284}\n  - {fileID: 2074246885}\n  - {fileID: 294713342}\n",
    )

    # Canvas scaler match 0.5
    text = text.replace(
        "m_GameObject: {fileID: 294713341}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: 0cd44c1031e13a943bb63640046fad76, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.CanvasScaler\n  m_UiScaleMode: 1\n  m_ReferencePixelsPerUnit: 100\n  m_ScaleFactor: 1\n  m_ReferenceResolution: {x: 1920, y: 1080}\n  m_ScreenMatchMode: 0\n  m_MatchWidthOrHeight: 0\n",
        "m_GameObject: {fileID: 294713341}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: 0cd44c1031e13a943bb63640046fad76, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.CanvasScaler\n  m_UiScaleMode: 1\n  m_ReferencePixelsPerUnit: 100\n  m_ScaleFactor: 1\n  m_ReferenceResolution: {x: 1920, y: 1080}\n  m_ScreenMatchMode: 0\n  m_MatchWidthOrHeight: 0.5\n",
        1,
    )

    # Panel sizes and colors
    text = text.replace(
        "m_GameObject: {fileID: 1578197411}\n  m_LocalRotation: {x: -0, y: -0, z: -0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 0.5729331, y: 0.5729331, z: 0.5729331}\n  m_ConstrainProportionsScale: 0\n  m_Children:\n  - {fileID: 1064196085}\n  - {fileID: 101872138}\n  - {fileID: 2101278651}\n  - {fileID: 1207963397}\n  m_Father: {fileID: 294713342}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n  m_AnchorMin: {x: 0.5, y: 0.5}\n  m_AnchorMax: {x: 0.5, y: 0.5}\n  m_AnchoredPosition: {x: 0, y: 0}\n  m_SizeDelta: {x: 1763.86, y: 1767.494}\n",
        "m_GameObject: {fileID: 1578197411}\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children:\n  - {fileID: 1064196085}\n  - {fileID: 101872138}\n  - {fileID: 2101278651}\n  - {fileID: 1207963397}\n  m_Father: {fileID: 294713342}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n  m_AnchorMin: {x: 0.5, y: 0.5}\n  m_AnchorMax: {x: 0.5, y: 0.5}\n  m_AnchoredPosition: {x: 0, y: 0}\n  m_SizeDelta: {x: 520, y: 400}\n",
    )
    text = text.replace(
        "m_GameObject: {fileID: 1118614768}\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children:\n  - {fileID: 1776393800}\n  - {fileID: 560874746}\n  - {fileID: 1777977738}\n  m_Father: {fileID: 294713342}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n  m_AnchorMin: {x: 0.5, y: 0.5}\n  m_AnchorMax: {x: 0.5, y: 0.5}\n  m_AnchoredPosition: {x: 0, y: 0.0000014305115}\n  m_SizeDelta: {x: 1096.3096, y: 851.6583}\n",
        "m_GameObject: {fileID: 1118614768}\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children:\n  - {fileID: 1776393800}\n  - {fileID: 560874746}\n  - {fileID: 1777977738}\n  m_Father: {fileID: 294713342}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n  m_AnchorMin: {x: 0.5, y: 0.5}\n  m_AnchorMax: {x: 0.5, y: 0.5}\n  m_AnchoredPosition: {x: 0, y: 0}\n  m_SizeDelta: {x: 720, y: 520}\n",
    )
    text = text.replace(
        "m_GameObject: {fileID: 655270623}\n  m_LocalRotation: {x: -0, y: -0, z: -0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 0.8862702, y: 0.9995693, z: 0.5729331}\n  m_ConstrainProportionsScale: 0\n  m_Children:\n  - {fileID: 888759165}\n  m_Father: {fileID: 294713342}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n  m_AnchorMin: {x: 0.5, y: 0.5}\n  m_AnchorMax: {x: 0.5, y: 0.5}\n  m_AnchoredPosition: {x: 0, y: -0.0000076293945}\n  m_SizeDelta: {x: 720, y: 480}\n",
        "m_GameObject: {fileID: 655270623}\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children:\n  - {fileID: 888759165}\n  m_Father: {fileID: 294713342}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n  m_AnchorMin: {x: 0.5, y: 0.5}\n  m_AnchorMax: {x: 0.5, y: 0.5}\n  m_AnchoredPosition: {x: 0, y: 0}\n  m_SizeDelta: {x: 520, y: 400}\n",
    )

    text = text.replace(BROWSER_PANEL_OLD, PANEL_SURFACE, 1)

    # Add Image to PanelInicio if only LayoutGroup - check 1578197415 is layout only
    # Panel browser image color already patched

    # Spawner references
    text = text.replace(
        f"  _menuCanvas: {{fileID: {GO_CANVAS_BROWSER}}}\n  _menuCamera: {{fileID: 1890316660}}\n  _panelPrincipal: {{fileID: {GO_CANVAS_MENU}}}\n  _panelCrearSala: {{fileID: {GO_CANVAS_CREAR}}}\n  _panelBrowser: {{fileID: {GO_CANVAS_BROWSER}}}\n  _inputNombreSala: {{fileID: 2011799706}}\n  _roomListPanel: {{fileID: {GO_CANVAS_BROWSER}}}\n  _roomListContent: {{fileID: {RT_CONTENT}}}\n",
        f"  _canvasMenu: {{fileID: {GO_CANVAS_MENU}}}\n  _menuCamera: {{fileID: 1890316660}}\n  _panelInicio: {{fileID: {GO_PANEL_INICIO}}}\n  _panelCrearSala: {{fileID: {GO_PANEL_CREAR}}}\n  _panelBrowser: {{fileID: {GO_PANEL_BROWSER}}}\n  _inputNombreSala: {{fileID: 2011799706}}\n  _roomListPanel: {{fileID: {GO_LOBBYS}}}\n  _roomListContent: {{fileID: {RT_CONTENT}}}\n",
    )

    SCENE.write_text(text, encoding="utf-8")
    print("Patched", SCENE)


if __name__ == "__main__":
    main()
