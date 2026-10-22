using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>Inspector panel for the emote wheel: drag animation clips into up to <see cref="DanceLibrary.MaxSlots"/> slots.</summary>
[CustomEditor(typeof(DanceLibrary))]
public class DanceLibraryEditor : Editor
{
    private static readonly Color[] SliceColors =
    {
        new Color(1f, 0.75f, 0.3f), new Color(0.75f, 0.45f, 1f), new Color(0.35f, 0.85f, 0.95f),
        new Color(1f, 0.5f, 0.7f), new Color(0.55f, 0.9f, 0.45f), new Color(1f, 0.45f, 0.4f),
        new Color(0.45f, 0.6f, 1f), new Color(0.95f, 0.9f, 0.4f), new Color(0.6f, 0.95f, 0.8f),
        new Color(0.85f, 0.65f, 0.5f),
    };

    private const float Gap = 4f;

    private SerializedProperty slots;
    private ReorderableList list;

    private void OnEnable()
    {
        slots = serializedObject.FindProperty("slots");
        list = new ReorderableList(serializedObject, slots, true, true, true, true)
        {
            elementHeightCallback = ElementHeight,
            drawElementCallback = DrawElement,
            drawHeaderCallback = rect => EditorGUI.LabelField(rect,
                $"Dance slots ({slots.arraySize} / {DanceLibrary.MaxSlots})  -  top of the wheel first, then clockwise"),
            onCanAddCallback = l => l.count < DanceLibrary.MaxSlots,
            onAddCallback = AddSlot,
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        list.DoLayoutList();
        FillBlankNames();
        serializedObject.ApplyModifiedProperties();
    }

    private void AddSlot(ReorderableList l)
    {
        int index = l.count;
        slots.arraySize++;
        SerializedProperty slot = slots.GetArrayElementAtIndex(index);
        slot.FindPropertyRelative("displayName").stringValue = "";
        slot.FindPropertyRelative("clip").objectReferenceValue = null;
        slot.FindPropertyRelative("color").colorValue = SliceColors[index % SliceColors.Length];
        l.index = index;
    }

    private float ElementHeight(int index)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float height = line + Gap;
        if (Warning(slots.GetArrayElementAtIndex(index)) != null) height += line * 1.6f;
        return height;
    }

    private void DrawElement(Rect rect, int index, bool active, bool focused)
    {
        SerializedProperty slot = slots.GetArrayElementAtIndex(index);
        float line = EditorGUIUtility.singleLineHeight;
        rect.y += Gap * 0.5f;

        float nameWidth = rect.width * 0.28f, colorWidth = rect.width * 0.14f;
        float clipWidth = rect.width - nameWidth - colorWidth - Gap * 2f;
        Rect nameRect = new Rect(rect.x, rect.y, nameWidth, line);
        Rect clipRect = new Rect(nameRect.xMax + Gap, rect.y, clipWidth, line);
        Rect colorRect = new Rect(clipRect.xMax + Gap, rect.y, colorWidth, line);

        EditorGUI.PropertyField(nameRect, slot.FindPropertyRelative("displayName"), GUIContent.none);
        EditorGUI.PropertyField(clipRect, slot.FindPropertyRelative("clip"), GUIContent.none);
        EditorGUI.PropertyField(colorRect, slot.FindPropertyRelative("color"), GUIContent.none);

        string warning = Warning(slot);
        if (warning != null)
        {
            Rect helpRect = new Rect(rect.x, rect.y + line + 2f, rect.width, line * 1.5f);
            EditorGUI.HelpBox(helpRect, warning, MessageType.Warning);
        }
    }

    private static string Warning(SerializedProperty slot)
    {
        AnimationClip clip = slot.FindPropertyRelative("clip").objectReferenceValue as AnimationClip;
        if (clip == null) return "No clip: this slot is skipped on the wheel.";
        if (!clip.isHumanMotion) return "Not a Humanoid clip: it won't play on the dog. Set the FBX Rig to Humanoid.";
        return null;
    }

    /// <summary>A slot with a clip but no label takes the clip's name, so dropping a clip in is enough.</summary>
    private void FillBlankNames()
    {
        for (int i = 0; i < slots.arraySize; i++)
        {
            SerializedProperty slot = slots.GetArrayElementAtIndex(i);
            SerializedProperty label = slot.FindPropertyRelative("displayName");
            AnimationClip clip = slot.FindPropertyRelative("clip").objectReferenceValue as AnimationClip;
            if (clip != null && string.IsNullOrWhiteSpace(label.stringValue)) label.stringValue = clip.name;
        }
    }
}
