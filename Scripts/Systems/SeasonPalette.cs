using Godot;
using System;

[GlobalClass]
public partial class SeasonPalette : Resource
{
    [Export] public Gradient TopColor;     
    [Export] public Gradient HorizonColor; 
    [Export] public Gradient SunColor;     
}