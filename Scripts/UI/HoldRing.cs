using Godot;

/// <summary>The hold-progress ring (faint track + accent arc + dot when full).</summary>
public partial class HoldRing : Control
{
    private static readonly Color TrackCol = new("#4a3030");
    private static readonly Color FillCol = new("#ff4444");
    private float _progress;
    private bool _done;

    public HoldRing()
    {
        CustomMinimumSize = new Vector2(26, 26);
        MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    public void SetProgress(float p)
    {
        p = Mathf.Clamp(p, 0f, 1f);
        if (Mathf.IsEqualApprox(p, _progress) && _done == (p >= 1f)) return;
        _progress = p;
        _done = p >= 1f;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var center = Size / 2f;
        float radius = Mathf.Min(Size.X, Size.Y) * 0.5f - 3f;
        if (radius < 2f) return;

        DrawArc(center, radius, 0f, Mathf.Tau, 40, TrackCol, 3f, true);
        if (_progress > 0f)
            DrawArc(center, radius, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * _progress, 40, FillCol, 3f, true);
        if (_done)
            DrawCircle(center, radius * 0.4f, FillCol);
    }
}