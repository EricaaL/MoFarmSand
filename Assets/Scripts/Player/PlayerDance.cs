using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Dance emote wheel. Hold Q, move the mouse out toward a slice, release Q (or left-click) to dance.
/// Right-click or releasing Q over the centre cancels. Moving or planting ends the dance through the Animator.
/// </summary>
public class PlayerDance : MonoBehaviour
{
    private static bool open;
    private static bool swallowClick; // the selecting click must not also throw / harvest

    /// <summary>True while the wheel is open (and until the selecting click is released), so the world ignores the click.</summary>
    public static bool WheelOpen => open || (swallowClick && Mouse.current != null && Mouse.current.leftButton.isPressed);

    private static readonly int DanceAHash = Animator.StringToHash("DanceA");
    private static readonly int DanceBHash = Animator.StringToHash("DanceB");

    private const int RoomySlices = 5;      // up to this many slices the wheel keeps its base radius
    private const float RadiusPerExtraSlice = 0.1f;
    private const int SmallLabelSlices = 6; // above this many slices the labels shrink so they don't overlap

    [SerializeField] private DanceLibrary library;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController controller;
    [Tooltip("Placeholder clips of the DanceA / DanceB states in MainCharacter.controller; the chosen dance is swapped in over one of them.")]
    [SerializeField] private AnimationClip slotA;
    [SerializeField] private AnimationClip slotB;
    [SerializeField] private float crossFade = 0.15f;
    [SerializeField] private float radius = 170f;   // authored at 1080p, scaled by ImGuiKit
    [SerializeField] private float deadZone = 45f;

    private readonly List<DanceSlot> playable = new List<DanceSlot>();
    private AnimatorOverrideController overrides;
    private bool nextIsB;     // dances alternate between the two states so one dance blends into the next
    private Vector2 center;   // GUI space (y down)
    private int hover = -1;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (controller == null) controller = GetComponent<PlayerController>();
        if (animator != null && animator.runtimeAnimatorController != null && slotA != null && slotB != null)
        {
            overrides = new AnimatorOverrideController(animator.runtimeAnimatorController);
            animator.runtimeAnimatorController = overrides;
        }
    }

    /// <summary>The dances shown on the wheel (edited in the Inspector).</summary>
    public DanceLibrary Library => library;

    /// <summary>Wheel radius in authored pixels: grows with the slice count so ten labels still fit.</summary>
    private float WheelRadius(int slices) => radius * (1f + Mathf.Max(0, slices - RoomySlices) * RadiusPerExtraSlice);

    private void OnDisable() { Close(); swallowClick = false; }

    private void Update()
    {
        if (library == null || Keyboard.current == null || Mouse.current == null) return;

        if (open && (RunDirector.ModalOpen || !CanDance)) { Close(); return; }

        if (!open)
        {
            if (Keyboard.current.qKey.wasPressedThisFrame && !RunDirector.ModalOpen && CanDance) Open();
            return;
        }

        hover = PieMenuMath.SliceAt(MouseGui() - center, playable.Count, ImGuiKit.S(deadZone));

        if (Mouse.current.rightButton.wasPressedThisFrame) { Close(); return; }

        bool click = Mouse.current.leftButton.wasPressedThisFrame;
        if (click || Keyboard.current.qKey.wasReleasedThisFrame)
        {
            if (hover >= 0) Play(playable[hover]);
            swallowClick = click;
            Close();
        }
    }

    private bool CanDance => controller == null || controller.CanMove;

    private void Open()
    {
        library.CollectPlayable(playable);
        if (playable.Count == 0) return; // nothing assigned in the Inspector yet
        open = true;
        hover = -1;
        float margin = ImGuiKit.S(WheelRadius(playable.Count)) + ImGuiKit.S(30f);
        Vector2 m = MouseGui();
        center = new Vector2(
            Mathf.Clamp(m.x, margin, Mathf.Max(margin, Screen.width - margin)),
            Mathf.Clamp(m.y, margin, Mathf.Max(margin, Screen.height - margin)));
    }

    private void Close() { open = false; hover = -1; }

    private void Play(DanceSlot dance)
    {
        if (animator == null || overrides == null || dance == null || dance.clip == null) return;
        overrides[nextIsB ? slotB : slotA] = dance.clip;
        animator.CrossFadeInFixedTime(nextIsB ? DanceBHash : DanceAHash, crossFade);
        nextIsB = !nextIsB;
        if (controller != null) controller.PlayThrowSquash(); // small pop so the start reads on camera
    }

    private static Vector2 MouseGui()
    {
        Vector2 p = Mouse.current.position.ReadValue();
        return new Vector2(p.x, Screen.height - p.y);
    }

    // ---------------------------------------------------------------- wheel drawing (IMGUI, presentation only)

    private void OnGUI()
    {
        if (!open || library == null || Event.current.type != EventType.Repaint) return;

        int n = playable.Count;
        float r = ImGuiKit.S(WheelRadius(n));
        bool compact = n > SmallLabelSlices;
        float inner = ImGuiKit.S(deadZone);

        // Game palette: an orange ring around a cream wheel, the hovered slice in orange.
        ImGuiKit.Circle(center, r + ImGuiKit.S(12f), ImGuiKit.Orange);
        ImGuiKit.Circle(center, r, ImGuiKit.Cream);

        if (hover >= 0) DrawWedge(hover, n, inner, r, ImGuiKit.Orange);

        for (int i = 0; i < n; i++)
        {
            Vector2 edge = PieMenuMath.SliceDirection(i, n);
            float half = 180f / n * Mathf.Deg2Rad;
            Vector2 d = Rotate(edge, half);
            ImGuiKit.Line(center + d * inner, center + d * r, ImGuiKit.S(3f), new Color(ImGuiKit.Brown.r, ImGuiKit.Brown.g, ImGuiKit.Brown.b, 0.5f));

            Vector2 mid = center + edge * (r * 0.66f);
            float labelWidth = compact ? 110f : 160f;
            Rect label = new Rect(mid.x - ImGuiKit.S(labelWidth * 0.5f), mid.y - ImGuiKit.S(20f), ImGuiKit.S(labelWidth), ImGuiKit.S(40f));
            Color text = ImGuiKit.Text; text.a = i == hover ? 1f : 0.85f;
            ImGuiKit.Label(label, playable[i].displayName, compact ? 18f : 22f, text, TextAnchor.MiddleCenter, true);
        }

        ImGuiKit.Circle(center, inner, ImGuiKit.PanelLight);
        ImGuiKit.Label(new Rect(center.x - inner, center.y - ImGuiKit.S(14f), inner * 2f, ImGuiKit.S(28f)),
            hover >= 0 ? "Release" : "Cancel", 16f, ImGuiKit.Text, TextAnchor.MiddleCenter, true);

        Vector2 cursor = MouseGui();
        Vector2 toCursor = Vector2.ClampMagnitude(cursor - center, r);
        if (toCursor.magnitude > inner)
            ImGuiKit.Line(center + toCursor.normalized * inner, center + toCursor, ImGuiKit.S(4f), ImGuiKit.HoverOrange);
        ImGuiKit.Circle(center + toCursor, ImGuiKit.S(7f), ImGuiKit.HoverOrange);
    }

    /// <summary>Fills a slice with thin radial strokes (IMGUI has no wedge primitive).</summary>
    private void DrawWedge(int index, int count, float inner, float outer, Color color)
    {
        color.a = 0.9f;
        float width = 360f / count;
        float start = PieMenuMath.SliceCenterAngle(index, count) - width * 0.5f;
        int steps = Mathf.Max(8, Mathf.CeilToInt(width / 3f));
        float stroke = outer * width / steps * Mathf.Deg2Rad + ImGuiKit.S(2f);
        for (int s = 0; s <= steps; s++)
        {
            float a = (start + width * s / steps) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
            ImGuiKit.Line(center + dir * inner, center + dir * outer, stroke, color);
        }
    }

    private static Vector2 Rotate(Vector2 v, float radians)
    {
        float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}
