using Godot;

public partial class SelectableBodyPart : Control
{
    private Color _fillColor = new Color(0.133f, 0.545f, 0.133f);
    private Color _borderColor = new Color(0.29f, 0.188f, 0.188f);
    private int _borderWidth = 2;
    private int _cornerRadius = 0;
    private bool _isSelected = false;

    private bool _isEllipse = false;
    public bool IsEllipse
    {
        get => _isEllipse;
        set { _isEllipse = value; QueueRedraw(); }
    }

    public Color FillColor
    {
        get => _fillColor;
        set { _fillColor = value; QueueRedraw(); }
    }

    public Color BorderColor
    {
        get => _borderColor;
        set { _borderColor = value; QueueRedraw(); }
    }

    public int BorderWidth
    {
        get => _borderWidth;
        set { _borderWidth = value; QueueRedraw(); }
    }

    public int CornerRadius
    {
        get => _cornerRadius;
        set { _cornerRadius = value; QueueRedraw(); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; QueueRedraw(); }
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        
        if (_isEllipse)
        {
            // Draw ellipse fill
            DrawCircle(rect.GetCenter(), rect.Size.X * 0.5f, _fillColor);
            // Draw ellipse border
            Color borderCol = _isSelected ? new Color(1, 0, 0) : _borderColor;
            int borderW = _isSelected ? 3 : _borderWidth;
            if (borderW > 0)
                DrawArc(rect.GetCenter(), rect.Size.X * 0.5f, 0, 360, 32, borderCol, borderW);
        }
        else
        {
            // Draw rounded rectangle fill
            if (_cornerRadius > 0)
            {
                DrawStyleBox(new StyleBoxFlat
                {
                    BgColor = _fillColor,
                    CornerRadiusTopLeft = _cornerRadius,
                    CornerRadiusTopRight = _cornerRadius,
                    CornerRadiusBottomLeft = _cornerRadius,
                    CornerRadiusBottomRight = _cornerRadius
                }, rect);
            }
            else
            {
                DrawRect(rect, _fillColor);
            }
            
            // Draw border
            Color borderCol = _isSelected ? new Color(1, 0, 0) : _borderColor;
            int borderW = _isSelected ? 3 : _borderWidth;
            if (borderW > 0)
            {
                if (_cornerRadius > 0)
                {
                    DrawStyleBox(new StyleBoxFlat
                    {
                        BgColor = Colors.Transparent,
                        BorderColor = borderCol,
                        BorderWidthLeft = borderW,
                        BorderWidthRight = borderW,
                        BorderWidthTop = borderW,
                        BorderWidthBottom = borderW,
                        CornerRadiusTopLeft = _cornerRadius,
                        CornerRadiusTopRight = _cornerRadius,
                        CornerRadiusBottomLeft = _cornerRadius,
                        CornerRadiusBottomRight = _cornerRadius
                    }, rect);
                }
                else
                {
                    DrawRect(rect, borderCol, false, borderW);
                }
            }
        }
    }
}