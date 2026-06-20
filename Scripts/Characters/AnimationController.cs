using Godot;
using System;

public partial class AnimationController : Node
{
    private Player _player;
    private AnimationTree _animTree;
    private AnimationPlayer _animPlayer;
    private AnimationNodeStateMachinePlayback _stateMachine;
    private Timer _turnResetTimer;
    
    private const string STATE_IDLE = "idle";
    private const string STATE_RUN = "run";
    private const string STATE_TURN = "turn";
    
    private string _currentState = STATE_IDLE;

    public override void _Ready()
    {
        _player = GetParent<Player>();
        if (_player == null) return;

        _animTree = _player.GetNode<AnimationTree>("AnimationTree");
        if (_animTree == null) return;
        
        _animTree.Active = true;
        _stateMachine = (AnimationNodeStateMachinePlayback)_animTree.Get("parameters/playback");
        
        _animPlayer = _player.GetNode<AnimationPlayer>("Syl/AnimationPlayer");
        if (_animPlayer != null)
        {
            if (_animPlayer.HasAnimation("idle"))
                _animPlayer.GetAnimation("idle").LoopMode = Animation.LoopModeEnum.Linear;
            if (_animPlayer.HasAnimation("run"))
                _animPlayer.GetAnimation("run").LoopMode = Animation.LoopModeEnum.Linear;
            if (_animPlayer.HasAnimation("turn180Right"))
                _animPlayer.GetAnimation("turn180Right").LoopMode = Animation.LoopModeEnum.None;
        }

        _turnResetTimer = new Timer();
        _turnResetTimer.OneShot = true;
        AddChild(_turnResetTimer);
        
        TravelToState(STATE_IDLE);
    }

    private void TravelToState(string newState)
    {
        if (newState == _currentState) return;
        
        // DEBUGGER: This will tell us EXACTLY why it snaps! 
        // If this prints rapidly, your velocity is dropping. If it doesn't print but still snaps, your AnimationTree settings are wrong.
        //GD.Print($"Changing State: {_currentState} -> {newState} | Current Speed: {new Vector3(_player.Velocity.X, 0, _player.Velocity.Z).Length()}");
        
        _stateMachine.Travel(newState);
        _currentState = newState; 
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_player == null || _stateMachine == null) return;

        Vector3 horizontalVelocity = new Vector3(_player.Velocity.X, 0, _player.Velocity.Z);
        float speed = horizontalVelocity.Length();
        
        bool onFloor = _player.IsOnFloor();
        string desired = _currentState;

        // Run trigger
        // FIX: Removed 'onFloor' and lowered threshold to 0.1f. 
        // This stops the engine from switching to Idle if velocity fluctuates or you step on a tiny bump.
        if (speed > 0.1f)
        {
            desired = STATE_RUN;
        }
        else
        {
            desired = STATE_IDLE;
        }

        TravelToState(desired);
    }
}