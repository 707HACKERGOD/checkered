using Godot;
      using System;
      
      public partial class NpcController : CharacterBody3D
      {
          private NpcEyeTracker _eyeTracker;
          private Area3D _visionArea;
      
          public override void _Ready()
          {
              // 1. Check if we found the tracker
              _eyeTracker = GetNodeOrNull<NpcEyeTracker>("EyeTrackerComponent");
              //if (_eyeTracker == null) GD.PrintErr("NPC Brain: Cannot find EyeTrackerComponent!");
      
              // 2. Check if we found the Area3D
              _visionArea = GetNodeOrNull<Area3D>("VisionArea");
              if (_visionArea == null) GD.PrintErr("NPC Brain: Cannot find VisionArea!");
              else
              {
                  _visionArea.BodyEntered += OnBodyEntered;
                  _visionArea.BodyExited += OnBodyExited;
                  //GD.Print("NPC Brain: Fully initialized and waiting...");
              }
          }
      
          private void OnBodyEntered(Node3D body)
          {
              //GD.Print($"NPC Brain: Something entered my vision! It is called: {body.Name}");
      
              if (body.IsInGroup("Player")) 
              {
                  //GD.Print("NPC Brain: I recognize the Player! Locking on.");
                  _eyeTracker.Target = body;
              }
              else
              {
                  //GD.Print("NPC Brain: It's not the player, ignoring it.");
              }
          }
      
          private void OnBodyExited(Node3D body)
          {
              if (body == _eyeTracker?.Target)
              {
                  //GD.Print("NPC Brain: Player left my vision. Returning to idle.");
                  _eyeTracker.Target = null;
              }
          }
      }