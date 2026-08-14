using Godot;

public static class AnimationFixer
{
    public static void StripBlendShapeTracks(AnimationPlayer animPlayer)
    {
        if (animPlayer == null) return;
        string[] animNames = animPlayer.GetAnimationList();
        foreach (string animName in animNames)
        {
            Animation anim = animPlayer.GetAnimation(animName);
            if (anim == null) continue;

            for (int i = anim.GetTrackCount() - 1; i >= 0; i--)
            {
                if (anim.TrackGetType(i) == Animation.TrackType.BlendShape)
                    anim.RemoveTrack(i);
            }
        }
    }
}