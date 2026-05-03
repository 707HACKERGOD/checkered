using Godot;

public partial class VirtualJoystick : Control
{
    [Signal] public delegate void JoystickMovedEventHandler(Vector2 direction);

    [Export] public float Deadzone = 0.2f;
    [Export] public int OuterSize = 400;

    private TextureRect _outerCircle;
    private TextureRect _knob;
    private Vector2 _center;
    private float _maxTravel;            // knob can move this far from center
    private int _touchIndex = -1;

    // Let the camera know which touch belongs to the joystick
    public static int ActiveTouchIndex => _instance?._touchIndex ?? -1;
    private static VirtualJoystick _instance;

    public override void _Ready()
    {
        _instance = this;
        MouseFilter = Control.MouseFilterEnum.Stop;
        Size = new Vector2(OuterSize, OuterSize);

        // Create circle textures so it looks nice
        _outerCircle = new TextureRect
        {
            Texture = CreateCircleTexture(OuterSize, new Color(0.2f, 0.2f, 0.2f, 0.6f)),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        _outerCircle.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(_outerCircle);

        _knob = new TextureRect
        {
            Texture = CreateCircleTexture((int)(OuterSize * 0.3), Colors.White),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Size = new Vector2(OuterSize * 0.3f, OuterSize * 0.3f)
        };
        _knob.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(_knob);

        _center = new Vector2(OuterSize / 2f, OuterSize / 2f);
        _knob.Position = _center - _knob.Size / 2;

        // Maximum distance the knob can travel = outer radius – knob radius
        _maxTravel = (OuterSize * 0.5f) - (_knob.Size.X * 0.5f);
    }

    private Texture2D CreateCircleTexture(int size, Color color)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(Colors.Transparent);
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - radius, dy = y - radius;
                if (dx * dx + dy * dy <= radius * radius)
                    image.SetPixel(x, y, color);
            }
        return ImageTexture.CreateFromImage(image);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventScreenTouch touch && touch.Pressed)
        {
            if (!GetGlobalRect().HasPoint(touch.Position))
                return;

            if (IsTouchOnAnyButton(touch.Position))
                return;               // let the button handle it, don't claim

            _touchIndex = touch.Index;
            TouchOwnership.Claim(_touchIndex);
            UpdateKnob(touch.Position);
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventScreenTouch touchUp && !touchUp.Pressed)
        {
            if (_touchIndex == -1 || touchUp.Index != _touchIndex)
                return;

            TouchOwnership.Release(_touchIndex);
            _touchIndex = -1;
            _knob.Position = _center - _knob.Size / 2;
            EmitSignal(SignalName.JoystickMoved, Vector2.Zero);
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventScreenDrag drag && _touchIndex != -1 && drag.Index == _touchIndex)
        {
            UpdateKnob(drag.Position);
            GetViewport().SetInputAsHandled();
        }
    }

    private void UpdateKnob(Vector2 screenPos)
    {
        Vector2 localPos = screenPos - GlobalPosition;
        Vector2 offset = localPos - _center;
        float distance = offset.Length();
        Vector2 dir = distance > 0.001f ? offset / distance : Vector2.Zero;

        if (distance > _maxTravel)
            offset = dir * _maxTravel;

        _knob.Position = _center + offset - _knob.Size / 2;

        float strength = Mathf.Clamp(distance / _maxTravel, 0f, 1f);
        if (strength < Deadzone) strength = 0f;

        EmitSignal(SignalName.JoystickMoved, dir * strength);
    }

    private bool IsTouchOnAnyButton(Vector2 screenPos)
    {
        foreach (var container in MobileUIController.ButtonContainers)
        {
            if (container != null && container.GetGlobalRect().HasPoint(screenPos))
                return true;
        }
        return false;
    }
}