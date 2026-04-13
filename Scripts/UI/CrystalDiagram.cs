using Godot;
using System;

public partial class CrystalDiagram : Control
{
    private ItemData _targetItem;
    private const float AtomRadius = 4.0f;
    private const float LineWidth = 2.0f;

    public void SetTargetItem(ItemData item)
    {
        _targetItem = item;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_targetItem?.Physics == null || !_targetItem.IsSolid)
        {
            // Draw a placeholder or nothing
            return;
        }

        Vector2 center = Size / 2;
        float scale = Mathf.Min(Size.X, Size.Y) * 0.3f;
        Color color = _targetItem.ThemeColor;
        color.A = 0.8f;

        switch (_targetItem.Physics.Lattice)
        {
            case LatticeType.Amorphous:
                DrawAmorphous(center, scale, color);
                break;
            case LatticeType.SimpleCubic:
            case LatticeType.BCC:
            case LatticeType.FCC:
                DrawCubicProjection(center, scale, color, _targetItem.Physics.Lattice);
                break;
            case LatticeType.Hexagonal:
                DrawHexagonal(center, scale, color);
                break;
        }
    }

    private void DrawCubicProjection(Vector2 center, float scale, Color color, LatticeType type)
    {
        // Isometric offset
        Vector2 offset = new Vector2(scale * 0.6f, -scale * 0.4f);

        // Compute the bounding box of the cube to center it properly
        // The cube extends from front[0] to back[2] (roughly)
        // Shift the entire drawing left by half the horizontal offset
        Vector2 shift = new Vector2(-offset.X * 0.5f, 0);
        Vector2 drawCenter = center + shift;

        // Front face corners
        Vector2[] front = {
            drawCenter + new Vector2(-scale, -scale),
            drawCenter + new Vector2( scale, -scale),
            drawCenter + new Vector2( scale,  scale),
            drawCenter + new Vector2(-scale,  scale)
        };

        // Back face corners
        Vector2[] back = {
            front[0] + offset,
            front[1] + offset,
            front[2] + offset,
            front[3] + offset
        };

        // Draw edges
        for (int i = 0; i < 4; i++)
        {
            int next = (i + 1) % 4;
            DrawLine(front[i], front[next], color, LineWidth);
            DrawLine(back[i], back[next], color, LineWidth);
            DrawLine(front[i], back[i], color, LineWidth);
            DrawCircle(front[i], AtomRadius, Colors.White);
            DrawCircle(back[i], AtomRadius, Colors.White);
        }

        // Additional nodes for BCC / FCC
        if (type == LatticeType.BCC)
        {
            DrawCircle(drawCenter + offset / 2, AtomRadius * 1.5f, Colors.Yellow);
        }
        else if (type == LatticeType.FCC)
        {
            DrawCircle(drawCenter, AtomRadius, Colors.Cyan);               // front face center
            DrawCircle(drawCenter + offset, AtomRadius, Colors.Cyan);      // back face center
        }
    }

    private void DrawAmorphous(Vector2 center, float scale, Color color)
    {
        // Use item ID as seed for consistent random pattern
        GD.Seed((ulong)_targetItem.Id);
        Vector2 prev = center;

        for (int i = 0; i < 8; i++)
        {
            Vector2 randOffset = new Vector2(
                GD.Randf() * scale * 1.5f - scale * 0.75f,
                GD.Randf() * scale * 1.5f - scale * 0.75f);
            Vector2 next = center + randOffset;
            DrawLine(prev, next, color, LineWidth * 0.8f);
            DrawCircle(next, AtomRadius * 0.8f, Colors.LightGray);
            prev = next;
        }
        GD.Randomize(); // Reset seed
    }

    private void DrawHexagonal(Vector2 center, float scale, Color color)
    {
        Vector2[] points = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.DegToRad(60 * i - 30);
            points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * scale;
        }

        for (int i = 0; i < 6; i++)
        {
            int next = (i + 1) % 6;
            DrawLine(points[i], points[next], color, LineWidth);
            DrawCircle(points[i], AtomRadius, Colors.White);
        }
        DrawCircle(center, AtomRadius * 1.2f, Colors.Orange);
    }

    public override void _Ready()
    {
        Resized += OnResized;
    }

    private void OnResized()
    {
        QueueRedraw();
    }
}