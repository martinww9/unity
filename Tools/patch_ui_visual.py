#!/usr/bin/env python3
"""Visual styling patches for UI.unity and RoomButtonPrefab."""
from pathlib import Path

UI = Path(__file__).resolve().parents[1] / "Assets/Scenes/UI/UI.unity"
PREFAB = Path(__file__).resolve().parents[1] / "Assets/Prefabs/RoomButtonPrefab.prefab"

ACCENT = "{r: 0.239, g: 0.545, b: 0.992, a: 1}"
ACCENT_PRESS = "{r: 0.18, g: 0.42, b: 0.78, a: 1}"
TEXT_PRIMARY = "{r: 0.941, g: 0.957, b: 0.973, a: 1}"
TEXT_SECONDARY = "{r: 0.659, g: 0.722, b: 0.8, a: 1}"
BG_PRIMARY = "{r: 0.102, g: 0.137, b: 0.196, a: 1}"
PANEL_SURFACE = "{r: 0.141, g: 0.204, b: 0.278, a: 1}"
INPUT_BG = "{r: 0.176, g: 0.243, b: 0.322, a: 1}"
LIST_BG = "{r: 0.09, g: 0.12, b: 0.17, a: 1}"

BUTTON_COLORS = f"""  m_Colors:
    m_NormalColor: {ACCENT}
    m_HighlightedColor: {{r: 0.35, g: 0.6, b: 1, a: 1}}
    m_PressedColor: {ACCENT_PRESS}
    m_SelectedColor: {{r: 0.35, g: 0.6, b: 1, a: 1}}
    m_DisabledColor: {{r: 0.4, g: 0.4, b: 0.4, a: 0.5}}"""

SECONDARY_BUTTON_COLORS = f"""  m_Colors:
    m_NormalColor: {{r: 0, g: 0, b: 0, a: 0}}
    m_HighlightedColor: {{r: 0.239, g: 0.545, b: 0.992, a: 0.15}}
    m_PressedColor: {{r: 0.18, g: 0.42, b: 0.78, a: 0.25}}
    m_SelectedColor: {{r: 0.239, g: 0.545, b: 0.992, a: 0.15}}
    m_DisabledColor: {{r: 0.4, g: 0.4, b: 0.4, a: 0.5}}"""

MENU_BG_YAML = """
--- !u!1 &900001001
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 900001002}
  - component: {fileID: 900001003}
  - component: {fileID: 900001004}
  m_Layer: 5
  m_Name: MenuBackground
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &900001002
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 900001001}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 294713342}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0, y: 0}
  m_AnchorMax: {x: 1, y: 1}
  m_AnchoredPosition: {x: 0, y: 0}
  m_SizeDelta: {x: 0, y: 0}
  m_Pivot: {x: 0.5, y: 0.5}
--- !u!222 &900001003
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 900001001}
  m_CullTransparentMesh: 1
--- !u!114 &900001004
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 900001001}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Image
  m_Material: {fileID: 0}
  m_Color: """ + BG_PRIMARY + """
  m_RaycastTarget: 1
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {fileID: 0}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
--- !u!114 &1578197413
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1578197411}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Image
  m_Material: {fileID: 0}
  m_Color: """ + PANEL_SURFACE + """
  m_RaycastTarget: 1
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {fileID: 10907, guid: 0000000000000000f000000000000000, type: 0}
  m_Type: 1
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
"""


def replace_button_colors_block(text: str, go_marker: str, primary: bool) -> str:
    idx = text.find(go_marker)
    if idx < 0:
        return text
    colors_idx = text.find("  m_Colors:", idx)
    if colors_idx < 0:
        return text
    end = text.find("  m_SpriteState:", colors_idx)
    block = BUTTON_COLORS if primary else SECONDARY_BUTTON_COLORS
    return text[:colors_idx] + block + "\n" + text[end:]


def main():
    text = UI.read_text(encoding="utf-8")

    if "900001001" not in text:
        text = text.replace("--- !u!1660057539 &9223372036854775807", MENU_BG_YAML + "\n--- !u!1660057539 &9223372036854775807")
        text = text.replace(
            "  m_Children:\n  - {fileID: 1578197412}\n  - {fileID: 1118614769}\n  - {fileID: 655270624}",
            "  m_Children:\n  - {fileID: 900001002}\n  - {fileID: 1578197412}\n  - {fileID: 1118614769}\n  - {fileID: 655270624}",
        )
        text = text.replace(
            "  m_Component:\n  - component: {fileID: 1578197412}\n  - component: {fileID: 1578197414}\n  - component: {fileID: 1578197415}",
            "  m_Component:\n  - component: {fileID: 1578197412}\n  - component: {fileID: 1578197414}\n  - component: {fileID: 1578197413}\n  - component: {fileID: 1578197415}",
        )

    # Title
    text = text.replace(
        "m_GameObject: {fileID: 1064196084}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: f4688fdb7df04437aeb418b961361dc5, type: 3}",
        "m_GameObject: {fileID: 1064196084}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: f4688fdb7df04437aeb418b961361dc5, type: 3}",
    )
    if "Trivia Estructuras" not in text:
        text = text.replace(
            '  m_text: "Texto de prueba"',
            '  m_text: "Trivia Estructuras de Datos"',
            1,
        )
    # patch first large TMP in panel inicio - find m_text near 1064196086
    import re
    text = re.sub(
        r"(m_GameObject: \{fileID: 1064196084\}[\s\S]*?m_text: )([^\n]+)",
        r'\1"Trivia Estructuras de Datos"',
        text,
        count=1,
    )
    text = re.sub(
        r"(m_GameObject: \{fileID: 1064196084\}[\s\S]*?m_fontColor: )\{[^}]+\}",
        rf"\1{TEXT_PRIMARY}",
        text,
        count=1,
    )
    text = re.sub(
        r"(m_GameObject: \{fileID: 1064196084\}[\s\S]*?m_fontSize: )[\d.]+",
        r"\g<1>42",
        text,
        count=1,
    )

    # Primary buttons: Join, ConfirmarHost
    text = replace_button_colors_block(text, "m_Name: Join_Button", True)
    text = replace_button_colors_block(text, "m_Name: ButtonConfirmarHost", True)
    text = replace_button_colors_block(text, "m_Name: Host_Button", False)
    text = replace_button_colors_block(text, "m_Name: ButtonRefrescar", False)
    text = replace_button_colors_block(text, "m_Name: ButtonVolver", False)

    for img_id in ["101872140", "2101278653", "1628586172"]:
        text = text.replace(
            f"m_GameObject: {{fileID: 101872137}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc",
            f"m_GameObject: {{fileID: 101872137}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc",
        )

    # Button target graphic colors
    for marker, color in [
        ("m_GameObject: {fileID: 101872137}", ACCENT),
        ("m_GameObject: {fileID: 2101278650}", "{r: 0, g: 0, b: 0, a: 0}"),
        ("m_GameObject: {fileID: 1628586169}", ACCENT),
    ]:
        idx = text.find(marker)
        if idx < 0:
            continue
        img_idx = text.find("guid: fe87c0e1cc204ed48ad3b37840f39efc", idx)
        if img_idx < 0:
            continue
        color_idx = text.find("m_Color:", img_idx)
        if color_idx < 0 or color_idx > idx + 800:
            continue
        end = text.find("\n", color_idx)
        text = text[:color_idx] + f"  m_Color: {color}" + text[end:]

    # TMP on buttons - white
    for name in ["Join_Button", "Host_Button", "ButtonConfirmarHost", "ButtonRefrescar", "ButtonVolver"]:
        idx = text.find(f"m_Name: {name}")
        if idx < 0:
            continue
        tmp_idx = text.find("guid: f4688fdb7df04437aeb418b961361dc5", idx)
        if tmp_idx < 0 or tmp_idx > idx + 2000:
            continue
        fc = text.find("m_fontColor:", tmp_idx)
        if fc > 0 and fc < tmp_idx + 1500:
            end = text.find("\n", fc)
            label_color = ACCENT if name == "Host_Button" else TEXT_PRIMARY
            if name in ("ButtonRefrescar", "ButtonVolver", "Host_Button"):
                label_color = ACCENT
            text = text[:fc] + f"  m_fontColor: {label_color}" + text[end:]

    # Browser header TMP
    idx = text.find("m_GameObject: {fileID: 1776393799}")
    if idx >= 0:
        text = re.sub(
            r"(m_GameObject: \{fileID: 1776393799\}[\s\S]*?m_text: )([^\n]+)",
            r'\1"Salas disponibles"',
            text,
            count=1,
        )

    # Join button label
    idx = text.find("m_Father: {fileID: 101872138}")
    if idx >= 0:
        text = re.sub(
            r"(m_Father: \{fileID: 101872138\}[\s\S]{0,1200}?m_text: )([^\n]+)",
            r'\1"Unirse a sala"',
            text,
            count=1,
        )

    UI.write_text(text, encoding="utf-8")

    prefab = PREFAB.read_text(encoding="utf-8")
    prefab = prefab.replace("m_SizeDelta: {x: 160, y: 30}", "m_SizeDelta: {x: 640, y: 52}")
    prefab = replace_button_colors_block(prefab, "m_Name: RoomButtonPrefab", True)
    idx = prefab.find("m_GameObject: {fileID: 6068650536980358826}")
    if idx >= 0:
        img = prefab.find("m_Color:", prefab.find("fe87c0e1cc204ed48ad3b37840f39efc", idx))
        if img > 0:
            end = prefab.find("\n", img)
            prefab = prefab[:img] + f"  m_Color: {ACCENT}" + prefab[end:]
    PREFAB.write_text(prefab, encoding="utf-8")
    print("Visual patches applied.")


if __name__ == "__main__":
    main()
