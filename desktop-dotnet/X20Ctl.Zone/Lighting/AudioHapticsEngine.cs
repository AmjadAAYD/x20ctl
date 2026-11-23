namespace X20Ctl.Zone.Lighting;

public enum AudioHapticsMode
{
    QuadConcert,
    TriggerPulse,
    StereoWave,
    SubwooferDrop,
    Heartbeat,
}

public readonly record struct HapticsFrame(
    double LeftGrip,
    double RightGrip,
    double LeftTrigger,
    double RightTrigger)
{
    public static HapticsFrame Zero => new(0, 0, 0, 0);
    public bool IsSilent => LeftGrip <= 0.001 && RightGrip <= 0.001 && LeftTrigger <= 0.001 && RightTrigger <= 0.001;
}

/// <summary>
/// Pure 4-motor audio haptics choreography engine mapping live stereo FFT bands and beats
/// onto Left Grip, Right Grip, Left Trigger, and Right Trigger actuators independently.
/// </summary>
public static class AudioHapticsEngine
{
    public static HapticsFrame Evaluate(AudioFrame audio, AudioHapticsMode mode, int strengthPercent, double elapsedSeconds = 0.0)
    {
        double scale = Math.Clamp(strengthPercent, 0, 100) / 100.0;
        if (scale <= 0.0) return HapticsFrame.Zero;

        double bass = Math.Clamp(audio.Bass, 0.0, 1.0);
        double mid = Math.Clamp(audio.Mid, 0.0, 1.0);
        double treble = Math.Clamp(audio.Treble, 0.0, 1.0);
        double beat = Math.Clamp(audio.BeatPulse, 0.0, 1.0);
        double transient = Math.Clamp(audio.TrebleTransient, 0.0, 1.0);
        double energy = Math.Clamp(audio.Energy, 0.0, 1.0);

        // Extract left/right weights even when stereo channels aren't populated separately.
        double leftBal = audio.LeftLevel > 0.0 || audio.RightLevel > 0.0
            ? Math.Clamp(audio.LeftLevel, 0.0, 1.0)
            : Math.Clamp(audio.Level * (1.0 - Math.Max(0.0, audio.StereoPan) * 0.65), 0.0, 1.0);
        double rightBal = audio.LeftLevel > 0.0 || audio.RightLevel > 0.0
            ? Math.Clamp(audio.RightLevel, 0.0, 1.0)
            : Math.Clamp(audio.Level * (1.0 - Math.Max(0.0, -audio.StereoPan) * 0.65), 0.0, 1.0);

        double lg = 0, rg = 0, lt = 0, rt = 0;

        switch (mode)
        {
            case AudioHapticsMode.TriggerPulse:
                // Fingertip percussion: triggers snap crisply to kicks & hi-hats; grips add soft bass cushion.
                lg = bass * 0.55;
                rg = mid * 0.40;
                lt = Math.Max(beat * 1.0, bass * 0.90);
                rt = Math.Max(transient * 1.0, Math.Max(treble * 0.95, mid * 0.55));
                break;

            case AudioHapticsMode.StereoWave:
            {
                // 3D Stereo stage + alternating LT/RT ping-pong on beats.
                bool pingLeft = (Math.Abs(audio.BeatCount) & 1) == 0;
                double kickBounce = beat * 0.85;
                lg = Math.Clamp(leftBal * 0.85 + bass * 0.35, 0.0, 1.0);
                rg = Math.Clamp(rightBal * 0.85 + mid * 0.35, 0.0, 1.0);
                lt = Math.Clamp(leftBal * 0.60 + (pingLeft ? kickBounce : transient * 0.45), 0.0, 1.0);
                rt = Math.Clamp(rightBal * 0.60 + (!pingLeft ? kickBounce : transient * 0.45), 0.0, 1.0);
                break;
            }

            case AudioHapticsMode.SubwooferDrop:
                // Calm verses tickle triggers lightly; heavy chorus/drops slam all 4 motors like cannons.
                if (beat > 0.36 || energy >= 0.55 || bass > 0.72)
                {
                    double slam = Math.Clamp(Math.Max(beat, Math.Max(bass * 0.95, energy * 0.88)), 0.0, 1.0);
                    lg = slam;
                    rg = Math.Clamp(slam * 0.88 + treble * 0.25, 0.0, 1.0);
                    lt = slam;
                    rt = Math.Clamp(Math.Max(slam * 0.92, transient), 0.0, 1.0);
                }
                else
                {
                    lg = 0;
                    rg = 0;
                    lt = bass * 0.32;
                    rt = Math.Max(treble * 0.35, mid * 0.25);
                }
                break;

            case AudioHapticsMode.Heartbeat:
                // Lub-Dub double pulse per beat: crisp triggers first ("lub"), then deep grips ("DUB").
                if (beat >= 0.55)
                {
                    lg = beat * 0.25;
                    rg = beat * 0.20;
                    lt = beat;
                    rt = beat * 0.92;
                }
                else if (beat >= 0.16)
                {
                    double dub = Math.Clamp((beat - 0.16) / 0.39, 0.0, 1.0);
                    lg = Math.Max(0.45 + 0.55 * dub, bass * 0.75);
                    rg = Math.Max(0.35 + 0.45 * dub, mid * 0.55);
                    lt = 0;
                    rt = transient * 0.5;
                }
                else
                {
                    double quietBreath = 0.05 * (0.5 + 0.5 * Math.Sin(elapsedSeconds * 3.0)) * Math.Max(bass, mid);
                    lg = quietBreath;
                    rg = 0;
                    lt = treble > 0.35 ? treble * 0.28 : 0;
                    rt = treble > 0.35 ? treble * 0.28 : 0;
                }
                break;

            case AudioHapticsMode.QuadConcert:
            default:
                // Full 4D Concert Stage: each of the 4 physical motors gets a specialized musical stem.
                lg = Math.Max(bass * 0.95, beat * 0.92);
                rg = Math.Max(mid * 0.88, treble * 0.45);
                lt = Math.Max(beat * 0.96, bass * leftBal * 0.90);
                rt = Math.Max(transient * 0.98, Math.Max(treble * 0.88, rightBal * mid * 0.65));
                break;
        }

        static double Gate(double v, double gain)
        {
            double scaled = Math.Clamp(v * gain, 0.0, 1.0);
            return scaled >= 0.06 ? scaled : 0.0;
        }

        return new HapticsFrame(
            Gate(lg, scale),
            Gate(rg, scale),
            Gate(lt, scale),
            Gate(rt, scale));
    }
}
