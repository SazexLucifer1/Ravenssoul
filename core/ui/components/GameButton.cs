using Godot;

namespace Utopia;

/// <summary>
/// The project's button. Adds game-UI states on top of Godot's built-in
/// idle/hover/focus/pressed/disabled theme states: selected, busy, error, and
/// cooldown, plus press feedback and a repeat-input guard.
///
/// Connect to <see cref="Activated"/>, not BaseButton.Pressed: Activated
/// is filtered by the repeat guard, busy state, and cooldown.
/// </summary>
[GlobalClass]
public partial class GameButton : Button
{
    [Signal] public delegate void ActivatedEventHandler();

    public enum State { Idle, Hover, Focus, Pressed, Selected, Disabled, Busy, Error, Cooldown }

    /// <summary>
    /// Presses within this window after an activation are ignored (double clicks,
    /// key repeat, mashing).
    /// </summary>
    [Export(PropertyHint.Range, "0.0,2.0,0.05")] public float RepeatGuardSeconds { get; set; } = 0.3f;
    /// <summary>Translation key shown while busy (e.g. "Saving…").</summary>
    [Export] public string BusyTextKey { get; set; } = "UI_BUSY";
    /// <summary>
    /// Hovering with the mouse moves keyboard focus here, so only one element
    /// ever looks highlighted.
    /// </summary>
    [Export] public bool FocusOnHover { get; set; } = true;

    private bool _busy;
    private string _idleText = "";
    private float _cooldownTotal;
    private float _cooldownLeft;
    private bool _errorActive;
    private ulong _lastActivationMsec;
    private bool _activatedOnce;
    private Tween _feedbackTween;

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;
        Pressed += OnPressed;
        Resized += UpdatePivot;
        MouseEntered += OnMouseEntered;
        SetProcess(false);
        UpdatePivot();
    }

    public State GetState()
    {
        if (_busy)
            return State.Busy;
        if (_cooldownLeft > 0.0f)
            return State.Cooldown;
        if (_errorActive)
            return State.Error;
        if (Disabled)
            return State.Disabled;
        if (ToggleMode && ButtonPressed)
            return State.Selected;
        if (IsPressed())
            return State.Pressed;
        if (HasFocus())
            return State.Focus;
        if (IsHovered())
            return State.Hover;
        return State.Idle;
    }

    public bool IsBusy() => _busy;

    /// <summary>Busy: disabled and relabeled until the operation finishes.</summary>
    public void SetBusy(bool value)
    {
        if (value == _busy)
            return;
        _busy = value;
        if (value)
        {
            _idleText = Text;
            Text = BusyTextKey;
        }
        else
        {
            Text = _idleText;
        }
        Disabled = value;
    }

    /// <summary>Briefly marks the button as failed (color pulse; shake unless reduced motion).</summary>
    public void ShowError()
    {
        _errorActive = true;
        Color danger = GetThemeColor("font_color", "DangerLabel");
        KillFeedback();
        _feedbackTween = CreateTween();
        Modulate = danger;
        if (!UiMotion.Reduced())
        {
            float origin = Position.X;
            foreach (float offset in new[] { 6.0f, -6.0f, 3.0f, 0.0f })
                _feedbackTween.TweenProperty(this, "position:x", origin + offset, 0.04);
        }
        _feedbackTween.TweenProperty(this, "modulate", Colors.White, 0.4);
        _feedbackTween.TweenCallback(Callable.From(() => _errorActive = false));
    }

    /// <summary>Disables the button for <paramref name="seconds"/> and draws a draining overlay.</summary>
    public void StartCooldown(float seconds)
    {
        if (seconds <= 0.0f)
            return;
        _cooldownTotal = seconds;
        _cooldownLeft = seconds;
        Disabled = true;
        SetProcess(true);
        QueueRedraw();
    }

    public float CooldownRatio() => _cooldownTotal <= 0.0f ? 0.0f : _cooldownLeft / _cooldownTotal;

    public override void _Process(double delta)
    {
        _cooldownLeft = Mathf.Max(0.0f, _cooldownLeft - (float)delta);
        QueueRedraw();
        if (_cooldownLeft <= 0.0f)
        {
            SetProcess(false);
            Disabled = _busy;
        }
    }

    public override void _Draw()
    {
        if (_cooldownLeft > 0.0f)
            DrawRect(new Rect2(Vector2.Zero, new Vector2(Size.X * CooldownRatio(), Size.Y)), new Color(0, 0, 0, 0.45f));
    }

    private void OnPressed()
    {
        ulong now = Time.GetTicksMsec();
        if (_busy || _cooldownLeft > 0.0f)
            return;
        if (_activatedOnce && now - _lastActivationMsec < (ulong)(RepeatGuardSeconds * 1000.0f))
            return;
        _activatedOnce = true;
        _lastActivationMsec = now;
        PlayPressFeedback();
        EmitSignalActivated();
    }

    private void PlayPressFeedback()
    {
        float duration = UiMotion.PressSeconds();
        if (Mathf.IsZeroApprox(duration) || !IsInsideTree())
            return;
        KillFeedback();
        _feedbackTween = CreateTween();
        Scale = new Vector2(0.96f, 0.96f);
        _feedbackTween.TweenProperty(this, "scale", Vector2.One, duration);
    }

    private void OnMouseEntered()
    {
        if (FocusOnHover && !Disabled && IsVisibleInTree())
            GrabFocus();
    }

    private void UpdatePivot() => PivotOffset = Size * 0.5f;

    private void KillFeedback()
    {
        if (_feedbackTween != null && _feedbackTween.IsValid())
            _feedbackTween.Kill();
        Scale = Vector2.One;
        Modulate = Colors.White;
    }
}
