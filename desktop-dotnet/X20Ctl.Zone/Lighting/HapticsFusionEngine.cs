namespace X20Ctl.Zone.Lighting;

/// <summary>
/// Universal 4-motor comfort-first Haptics Fusion Engine.
/// In games: uses Right-Grip-first micro-taps, un-AGC'd RawRms loudness scaling,
/// input-first action pulses (walk, grass, jump, dash, attack), automatic game BGM &amp; dialogue/cutscene
/// rejection, menu cursor silencing, and reserves Left Grip + LT/RT triggers for heavy or overly loud impacts
/// (plus expressive lyrical triggers in Music mode).
/// </summary>
public sealed class HapticsFusionEngine
{
    readonly HapticsAudioClassifier classifier;
    readonly object gate = new();

    double elapsedSeconds;
    double avgDuty;
    double sustainedHighSec;
    double steadyEngineSec;
    double boostHoldSec;
    double stepCooldownSec;
    double menuMuteFactor;
    double absPhase;
    double gravelPhase;
    bool prevSprinting;
    double prevRightTrigger;
    double gearShiftRemaining;

    // Input-first short tactical envelopes (discrete taps instead of continuous drone)
    double stepTapRemaining;
    double stepTapLevel;
    double jumpTapRemaining;
    double airborneWindowSec;
    double landingTapRemaining;
    double dashRemaining;
    double attackTapRemaining;

    HapticsDemoKind? activeDemo;
    double demoRemainingSec;
    double demoTotalSec;

    double lastLg, lastRg, lastLt, lastRt;

    public HapticsFusionEngine(HapticsAudioClassifier? classifier = null)
    {
        this.classifier = classifier ?? new HapticsAudioClassifier();
    }

    public bool HasEmbeddedModel => classifier.UsingEmbeddedWeights;

    public void Reset()
    {
        classifier.Reset();
        lock (gate)
        {
            elapsedSeconds = avgDuty = sustainedHighSec = steadyEngineSec = boostHoldSec = 0;
            stepCooldownSec = menuMuteFactor = absPhase = gravelPhase = 0;
            prevSprinting = false;
            prevRightTrigger = 0;
            gearShiftRemaining = stepTapRemaining = stepTapLevel = 0;
            jumpTapRemaining = airborneWindowSec = landingTapRemaining = dashRemaining = attackTapRemaining = 0;
            activeDemo = null;
            demoRemainingSec = demoTotalSec = 0;
            lastLg = lastRg = lastLt = lastRt = 0;
        }
    }

    public void TriggerDemoEffect(HapticsDemoKind kind)
    {
        lock (gate)
        {
            activeDemo = kind;
            demoTotalSec = kind switch
            {
                HapticsDemoKind.GunshotBurst => 0.85,
                HapticsDemoKind.DirtRoad => 1.35,
                HapticsDemoKind.NitroBoost => 1.25,
                HapticsDemoKind.AbsBrake => 1.10,
                HapticsDemoKind.ExplosionWave => 1.15,
                HapticsDemoKind.BassDrop => 1.20,
                _ => 1.0,
            };
            demoRemainingSec = demoTotalSec;
        }
    }

    public HapticsEvaluation Evaluate(
        AudioFrame audio,
        LivePadInput input,
        HapticsGenreProfile profile,
        HapticsTuning rawTuning,
        MotorRoleMap roles = default,
        double dtSeconds = 0.020)
    {
        double dt = double.IsFinite(dtSeconds) && dtSeconds > 0 ? Math.Clamp(dtSeconds, 0.002, 0.20) : 0.020;
        var tuning = rawTuning.Normalized();
        var cls = classifier.Classify(audio, dt);

        lock (gate)
        {
            elapsedSeconds += dt;

            if (activeDemo is { } demo && demoRemainingSec > 0)
            {
                double tNorm = 1.0 - demoRemainingSec / Math.Max(0.1, demoTotalSec);
                demoRemainingSec = Math.Max(0.0, demoRemainingSec - dt);
                if (demoRemainingSec <= 0.001) activeDemo = null;
                var demoEval = EvaluateDemoLocked(demo, tNorm, cls, tuning);
                return SmoothAndBudgetLocked(demoEval.Motors, cls, demoEval.Primary, demoEval.Secondary, profile, false, tuning, dt);
            }

            if (profile == HapticsGenreProfile.Off || tuning.MasterIntensity <= 0)
            {
                avgDuty *= Math.Exp(-dt / 1.5);
                return new HapticsEvaluation(HapticsFrame.Zero, cls, HapticsEventTag.None, HapticsEventTag.None, profile, false, avgDuty);
            }

            var resolved = profile == HapticsGenreProfile.Auto ? InferAutoProfile(audio, cls, input) : profile;
            bool isGameContext = resolved is HapticsGenreProfile.Racing or HapticsGenreProfile.FpsAction or HapticsGenreProfile.RpgMelee or HapticsGenreProfile.SportsArcade
                || (input.IsGameForeground && resolved != HapticsGenreProfile.Music);

            double effectiveRawRms = audio.RawRms > 0f ? audio.RawRms : audio.Level * 0.28;
            double effectiveRawSpike = audio.RawPeakSpike > 0f ? audio.RawPeakSpike : (audio.RawRms <= 0f ? audio.BeatPulse * 0.14 : 0.0);
            bool isOverlyLoud = effectiveRawRms >= 0.17 || effectiveRawSpike >= 0.11;
            bool majorImpactPresent = (cls.Explosion > 0.52f && (effectiveRawRms >= 0.14 || effectiveRawSpike >= 0.10))
                || (cls.Gunshot > 0.58f && input.RightTrigger > 0.25);

            bool cursorMute = isGameContext && tuning.MenuAutoMute && input.CursorVisible;
            bool idleMute = isGameContext && tuning.MenuAutoMute && input.IsIdle && !majorImpactPresent;
            bool wantMute = cursorMute || idleMute;
            double muteTarget = wantMute ? 1.0 : 0.0;
            menuMuteFactor += (muteTarget - menuMuteFactor) * (1.0 - Math.Exp(-dt / (wantMute ? 0.08 : 0.14)));

            if (menuMuteFactor > 0.96)
            {
                avgDuty *= Math.Exp(-dt / 1.5);
                lastLg = lastRg = lastLt = lastRt = 0;
                return new HapticsEvaluation(HapticsFrame.Zero, cls, HapticsEventTag.MenuMuted, HapticsEventTag.None, resolved, true, avgDuty);
            }

            // Cutscene & character speech auto-ducking across ALL game modes (never buzz when characters talk!).
            if (isGameContext && (cls.Dialogue > 0.30f || cls.Vocal > 0.30f) && !majorImpactPresent && input.PressedButtons == PadActionButtons.None)
            {
                return SmoothAndBudgetLocked(HapticsFrame.Zero, cls, HapticsEventTag.DialogueDuck, HapticsEventTag.None, resolved, false, tuning, dt);
            }

            double lg = 0, rg = 0, lt = 0, rt = 0;
            var ev1 = HapticsEventTag.None;
            var ev2 = HapticsEventTag.None;

            switch (resolved)
            {
                case HapticsGenreProfile.Racing:
                    EvaluateRacingLocked(audio, cls, input, tuning, effectiveRawRms, effectiveRawSpike, isOverlyLoud, dt, out lg, out rg, out lt, out rt, out ev1, out ev2);
                    break;
                case HapticsGenreProfile.FpsAction:
                    EvaluateActionLocked(audio, cls, input, tuning, effectiveRawRms, effectiveRawSpike, isOverlyLoud, isMeleePrimary: false, dt, out lg, out rg, out lt, out rt, out ev1, out ev2);
                    break;
                case HapticsGenreProfile.RpgMelee:
                    EvaluateActionLocked(audio, cls, input, tuning, effectiveRawRms, effectiveRawSpike, isOverlyLoud, isMeleePrimary: true, dt, out lg, out rg, out lt, out rt, out ev1, out ev2);
                    break;
                case HapticsGenreProfile.Music:
                    EvaluateMusicLocked(audio, cls, tuning, roles, out lg, out rg, out lt, out rt, out ev1, out ev2);
                    break;
                case HapticsGenreProfile.Cinema:
                    EvaluateCinemaLocked(audio, cls, tuning, effectiveRawRms, effectiveRawSpike, out lg, out rg, out lt, out rt, out ev1, out ev2);
                    break;
                case HapticsGenreProfile.SportsArcade:
                    EvaluateSportsLocked(audio, cls, tuning, effectiveRawRms, effectiveRawSpike, isOverlyLoud, out lg, out rg, out lt, out rt, out ev1, out ev2);
                    break;
            }

            double passThrough = Math.Clamp(1.0 - menuMuteFactor, 0.0, 1.0);
            var rawFrame = new HapticsFrame(lg * passThrough, rg * passThrough, lt * passThrough, rt * passThrough);
            return SmoothAndBudgetLocked(rawFrame, cls, ev1, ev2, resolved, wantMute, tuning, dt);
        }
    }

    static HapticsGenreProfile InferAutoProfile(AudioFrame audio, AudioClassification cls, LivePadInput input)
    {
        // Strict boundary: active foreground games NEVER drift into expressive Music mode because of BGM themes.
        if (input.IsGameForeground)
        {
            if (cls.TireScreech > 0.48f || (cls.EngineRumble > 0.50f && input.RightTrigger > 0.35))
                return HapticsGenreProfile.Racing;
            if (cls.MeleeClash > 0.50f || (input.PressedButtons & (PadActionButtons.X | PadActionButtons.Y)) != 0)
                return HapticsGenreProfile.RpgMelee;
            return HapticsGenreProfile.FpsAction;
        }

        if ((cls.Vocal > 0.35f || cls.Drums > 0.38f || cls.Instruments > 0.42f)
            && cls.Gunshot < 0.45f && cls.TireScreech < 0.45f)
            return HapticsGenreProfile.Music;

        if (cls.Dialogue > 0.48f && cls.Gunshot < 0.35f && input.RightTrigger < 0.25)
            return HapticsGenreProfile.Cinema;

        if (cls.TireScreech > 0.45f || (cls.EngineRumble > 0.48f && input.RightTrigger > 0.30))
            return HapticsGenreProfile.Racing;

        return HapticsGenreProfile.FpsAction;
    }

    void EvaluateRacingLocked(
        AudioFrame audio,
        AudioClassification cls,
        LivePadInput input,
        HapticsTuning tuning,
        double rawRms,
        double rawSpike,
        bool isOverlyLoud,
        double dt,
        out double lg,
        out double rg,
        out double lt,
        out double rt,
        out HapticsEventTag ev1,
        out HapticsEventTag ev2)
    {
        double texScale = tuning.TextureDetail / 100.0;
        double punchScale = tuning.ImpactPunch / 100.0;
        double comfortTex = ComfortTextureScale(tuning.ComfortTier);
        double rawLoudness = Math.Clamp(rawRms / 0.22, 0.0, 1.0);
        double pan = Math.Clamp(audio.StereoPan, -1.0, 1.0);

        ev1 = HapticsEventTag.None;
        ev2 = HapticsEventTag.None;

        // 1. Dirt / Gravel / Grass surface: faint spaced micro-taps on RIGHT grip ONLY for gentle driving.
        // Left grip stays silent unless a genuinely rough curb/offroad rumble hits (rawLoudness > 0.45).
        gravelPhase += dt * (24.0 + 14.0 * cls.GravelSurface);
        double pebblePulse = Math.Max(0.0, Math.Sin(gravelPhase * 1.73 + Math.Cos(gravelPhase * 0.61)));
        bool gravelAudio = cls.GravelSurface > 0.28f && rawRms > 0.010;
        double dirtLight = gravelAudio
            ? Math.Clamp((cls.GravelSurface - 0.22) / 0.78 * (0.06 + 0.09 * texScale) * comfortTex * (0.45 + 0.55 * rawLoudness) * (pebblePulse > 0.45 ? 1.0 : 0.0), 0.0, 0.18)
            : 0.0;
        double dirtHeavyLeft = gravelAudio && rawLoudness > 0.48 && pan < 0.20
            ? dirtLight * 0.65 * Math.Clamp((rawLoudness - 0.48) / 0.52, 0.0, 1.0)
            : 0.0;
        if (dirtLight > 0.04)
            ev2 = pan < -0.20 ? HapticsEventTag.GravelLeft : HapticsEventTag.GravelRight;

        // 2. Throttle-gated engine: ultra-faint Right Grip whisper on acceleration, fading to 0 after 0.6s steady cruise.
        double engActive = cls.EngineRumble > 0.38f && input.RightTrigger > 0.12 && rawRms > 0.015
            ? cls.EngineRumble * Math.Sqrt(input.RightTrigger) * rawLoudness
            : 0.0;
        if (engActive > 0.20 && cls.BoostSurge < 0.45f)
            steadyEngineSec = Math.Min(3.0, steadyEngineSec + dt);
        else
            steadyEngineSec = Math.Max(0.0, steadyEngineSec - dt * 2.0);
        double cruiseFade = Math.Clamp(1.0 - Math.Max(0.0, steadyEngineSec - 0.45) / 0.85, 0.0, 1.0);
        double engineWhisper = engActive * 0.09 * texScale * comfortTex * cruiseFade;

        // 3. Gear shift kick: brief 55 ms snap on Right Grip (+ light RT detail on strong throttle).
        gearShiftRemaining = Math.Max(0.0, gearShiftRemaining - dt);
        if (gearShiftRemaining <= 0 && input.RightTrigger > 0.65 && prevRightTrigger > 0.65
            && audio.CentroidVelocity < -0.32f && cls.EngineRumble > 0.30f)
        {
            gearShiftRemaining = 0.055;
        }
        prevRightTrigger = input.RightTrigger;
        double shiftGrip = gearShiftRemaining > 0 ? 0.34 * punchScale : 0.0;
        double shiftTrig = gearShiftRemaining > 0 && isOverlyLoud ? 0.42 * punchScale : 0.0;
        if (shiftGrip > 0.10) ev1 = HapticsEventTag.GearShift;

        // 4. Nitro / Turbo Boost: "fwoosh, then rest" into both grips + RT only during bright peak surge.
        bool boostOn = cls.BoostSurge > 0.50f && rawLoudness > 0.28;
        if (boostOn)
            boostHoldSec = Math.Min(3.0, boostHoldSec + dt);
        else
            boostHoldSec = Math.Max(0.0, boostHoldSec - dt * 2.4);
        double boostEnv = boostOn
            ? cls.BoostSurge * Math.Clamp(1.0 - Math.Max(0.0, boostHoldSec - 0.30) / 0.85, 0.0, 1.0)
            : 0.0;
        double boostGrip = boostEnv * 0.52 * punchScale;
        double boostRt = isOverlyLoud ? boostEnv * 0.55 * punchScale : 0.0;
        if (boostGrip > 0.12) ev1 = HapticsEventTag.BoostSurge;

        // 5. ABS Brake chatter: strictly requires braking (LT > 0.45) + tire screech + real loudness.
        absPhase += dt * 14.0 * 2.0 * Math.PI;
        double absGate = (input.LeftTrigger > 0.45 && (cls.TireScreech > 0.42f || audio.HighFreqResonance > 0.50f) && rawLoudness > 0.25)
            ? Math.Max(cls.TireScreech, audio.HighFreqResonance)
            : 0.0;
        double absPulseLt = absGate > 0.05 ? (0.45 + 0.55 * (0.5 + 0.5 * Math.Sin(absPhase))) * absGate * 0.68 * punchScale : 0.0;
        if (absPulseLt > 0.14) ev1 = HapticsEventTag.AbsBrake;

        // 6. Crashes / Overly loud impacts: heavy Left Grip + Right Grip + Trigger detail ONLY when truly loud.
        double crash = cls.Explosion > 0.48f && isOverlyLoud ? cls.Explosion * 0.82 * punchScale * rawLoudness : 0.0;
        if (crash > 0.20) ev1 = HapticsEventTag.Explosion;

        // Racing default: triggers stay ZERO during normal driving, dirt, curbs, and coasting.
        lg = Math.Clamp(crash + boostGrip * 0.65 + dirtHeavyLeft, 0.0, 1.0);
        rg = Math.Clamp(dirtLight + engineWhisper + shiftGrip + boostGrip + crash * 0.80, 0.0, 1.0);
        lt = Math.Clamp(absPulseLt + (isOverlyLoud ? crash * 0.35 : 0.0), 0.0, 1.0);
        rt = Math.Clamp(boostRt + shiftTrig + (isOverlyLoud ? crash * 0.35 : 0.0), 0.0, 1.0);
    }

    void EvaluateActionLocked(
        AudioFrame audio,
        AudioClassification cls,
        LivePadInput input,
        HapticsTuning tuning,
        double rawRms,
        double rawSpike,
        bool isOverlyLoud,
        bool isMeleePrimary,
        double dt,
        out double lg,
        out double rg,
        out double lt,
        out double rt,
        out HapticsEventTag ev1,
        out HapticsEventTag ev2)
    {
        double texScale = tuning.TextureDetail / 100.0;
        double punchScale = tuning.ImpactPunch / 100.0;
        double comfortTex = ComfortTextureScale(tuning.ComfortTier);
        double rawLoudness = Math.Clamp(rawRms / 0.22, 0.0, 1.0);

        ev1 = HapticsEventTag.None;
        ev2 = HapticsEventTag.None;

        // Tick down discrete tactical pulse timers cleanly
        stepCooldownSec = Math.Max(0.0, stepCooldownSec - dt);
        stepTapRemaining = Math.Max(0.0, stepTapRemaining - dt);
        jumpTapRemaining = Math.Max(0.0, jumpTapRemaining - dt);
        airborneWindowSec = Math.Max(0.0, airborneWindowSec - dt);
        landingTapRemaining = Math.Max(0.0, landingTapRemaining - dt);
        dashRemaining = Math.Max(0.0, dashRemaining - dt);
        attackTapRemaining = Math.Max(0.0, attackTapRemaining - dt);

        // 1. Input-First Discrete Action Triggers: Jump (A), Dash (B/RB/L3), Sword/Attack (X/Y)
        if ((input.PressedButtons & PadActionButtons.A) != 0)
        {
            jumpTapRemaining = 0.028;
            airborneWindowSec = 1.05;
        }
        bool dashBtnPressed = (input.PressedButtons & (PadActionButtons.B | PadActionButtons.RB | PadActionButtons.L3)) != 0;
        bool sprintOnset = input.LeftStickMagnitude > 0.88 && !prevSprinting;
        prevSprinting = input.LeftStickMagnitude > 0.88;
        if (dashBtnPressed || sprintOnset)
            dashRemaining = 0.095;

        if ((input.PressedButtons & (PadActionButtons.X | PadActionButtons.Y)) != 0)
            attackTapRemaining = 0.052;

        // Detect jump landing impact after 0.22s in air
        if (airborneWindowSec is > 0.05 and < 0.82 && (rawSpike > 0.04 || audio.SpectralFlux > 0.34f))
        {
            landingTapRemaining = 0.038;
            airborneWindowSec = 0.0;
        }

        // 2. Walking / Running / Grass footsteps: Right-Grip-ONLY faint 26 ms micro-taps, NEVER VOM VOM VOM!
        // Reject background music themes: require LeftStick movement AND non-silent movement audio, paused while airborne.
        double moveMag = input.LeftStickMagnitude;
        bool movingQuietAudio = rawRms > 0.008 && (cls.GravelSurface > 0.20f || audio.SpectralFlux > 0.16f || rawSpike > 0.012);
        if (moveMag > 0.25 && airborneWindowSec <= 0.05 && stepCooldownSec <= 0.0 && movingQuietAudio)
        {
            stepTapLevel = Math.Clamp((0.06 + 0.06 * texScale) * comfortTex * (0.45 + 0.55 * Math.Min(1.0, rawLoudness * 1.6)), 0.05, 0.14);
            stepTapRemaining = 0.026;
            stepCooldownSec = moveMag > 0.85 ? 0.31 : 0.42;
            ev2 = HapticsEventTag.Footstep;
        }

        double walkTapRg = stepTapRemaining > 0 ? stepTapLevel : 0.0;
        double jumpRg = jumpTapRemaining > 0 ? 0.10 * comfortTex : 0.0;
        double landRg = landingTapRemaining > 0 ? 0.16 * comfortTex : 0.0;
        double landLg = landingTapRemaining > 0 && rawLoudness > 0.35 ? 0.08 * comfortTex : 0.0;
        double dashFwooshRg = dashRemaining > 0 ? (dashRemaining / 0.095) * 0.16 * comfortTex : 0.0;
        double swordSnapRg = attackTapRemaining > 0 ? (isMeleePrimary ? 0.28 : 0.22) * punchScale : 0.0;
        if (swordSnapRg > 0.08 && ev2 == HapticsEventTag.None) ev2 = HapticsEventTag.MeleeClash;

        // 3. Shooting (FPS RT pulled + gunshot transient): crisp Right Trigger + Right Grip recoil, Left Grip only if heavy.
        bool shootingActive = input.RightTrigger > 0.25 && cls.Gunshot > 0.42f && rawRms > 0.02;
        double gunEnv = shootingActive ? Math.Pow((cls.Gunshot - 0.36) / 0.64, 0.90) * punchScale * Math.Clamp(0.45 + 0.55 * rawLoudness, 0.35, 1.0) : 0.0;
        double gunRt = gunEnv * 0.82;
        double gunRg = gunEnv * 0.38;
        double gunLg = (gunEnv > 0.35 && audio.Bass > 0.55f && isOverlyLoud) ? gunEnv * 0.45 : 0.0;
        if (gunEnv > 0.15) ev1 = HapticsEventTag.Gunshot;

        // 4. Sword Parry / Sharp Clash (loud only): triggers stay quiet on light swings!
        double parryClang = (cls.MeleeClash > 0.55f && (attackTapRemaining > 0 || isOverlyLoud) && rawRms > 0.03)
            ? cls.MeleeClash * 0.55 * punchScale * Math.Clamp(rawLoudness, 0.3, 1.0)
            : 0.0;
        if (parryClang > 0.18 && ev1 == HapticsEventTag.None) ev1 = HapticsEventTag.MeleeClash;

        // 5. Heavy Damage Taken / Boss Slams / Loud Explosions (break through cleanly even without button input!):
        bool heavyHitOrExplosion = (cls.Explosion > 0.48f && isOverlyLoud)
            || (rawSpike >= 0.12 && audio.Bass > 0.55f && cls.Instruments < 0.70f);
        double explosionPower = heavyHitOrExplosion
            ? Math.Clamp(Math.Max(cls.Explosion, rawSpike * 4.0) * Math.Clamp(0.5 + 0.5 * rawLoudness, 0.5, 1.0) * punchScale, 0.0, 0.90)
            : 0.0;
        if (explosionPower > 0.20) ev1 = HapticsEventTag.Explosion;

        double subtleRg = Math.Max(Math.Max(walkTapRg, jumpRg), Math.Max(landRg, Math.Max(dashFwooshRg, swordSnapRg)));
        double duckSubtle = Math.Clamp(1.0 - Math.Max(gunEnv, explosionPower) * 0.75, 0.15, 1.0);

        // Triggers and Left Grip stay ZERO for walking, grass, jumping, dashing, light hits, and quiet music themes!
        lg = Math.Clamp(explosionPower * 0.90 + gunLg + landLg, 0.0, 1.0);
        rg = Math.Clamp(subtleRg * duckSubtle + gunRg + explosionPower * 0.65 + parryClang * 0.55, 0.0, 1.0);
        lt = Math.Clamp((isOverlyLoud ? explosionPower * 0.45 : 0.0) + (isOverlyLoud ? parryClang * 0.55 : 0.0), 0.0, 1.0);
        rt = Math.Clamp(gunRt + (isOverlyLoud ? explosionPower * 0.45 + parryClang * 0.65 : 0.0), 0.0, 1.0);
    }

    void EvaluateMusicLocked(
        AudioFrame audio,
        AudioClassification cls,
        HapticsTuning tuning,
        MotorRoleMap roles,
        out double lg,
        out double rg,
        out double lt,
        out double rt,
        out HapticsEventTag ev1,
        out HapticsEventTag ev2)
    {
        var baseline = AudioHapticsEngine.Evaluate(audio, AudioHapticsMode.AutoAdaptive, tuning.MasterIntensity, roles, elapsedSeconds);
        double punchScale = tuning.ImpactPunch / 100.0;
        double voxScale = tuning.VocalFocus / 100.0;

        if (!roles.IsAllAuto)
        {
            double gateV = cls.Vocal > 0.18f || audio.VocalPresence > 0.28f ? 1.0 : 0.20;
            lg = baseline.LeftGrip;
            rg = baseline.RightGrip;
            lt = roles.LeftTrigger is MotorAudioRole.LeadVocals or MotorAudioRole.VocalAir ? baseline.LeftTrigger * gateV : baseline.LeftTrigger;
            rt = roles.RightTrigger is MotorAudioRole.LeadVocals or MotorAudioRole.VocalAir ? baseline.RightTrigger * gateV : baseline.RightTrigger;
        }
        else
        {
            double dynShape = Math.Clamp(0.45 + 0.55 * audio.DynamicContrast, 0.35, 1.15);
            double bassGroove = Math.Clamp(Math.Pow(Math.Max(cls.Bass, audio.Bass) * 0.72 + cls.Drums * 0.45, 1.25) * dynShape * punchScale, 0.0, 0.95);
            double instLayer = Math.Clamp(Math.Pow(cls.Instruments, 1.20) * (0.45 + 0.55 * audio.DynamicContrast), 0.0, 0.75);
            double stereoL = Math.Clamp(1.0 - Math.Max(0.0, audio.StereoPan) * 0.45, 0.5, 1.0);
            double stereoR = Math.Clamp(1.0 - Math.Max(0.0, -audio.StereoPan) * 0.45, 0.5, 1.0);

            double cleanVocal = cls.Vocal > 0.22f
                ? Math.Clamp((cls.Vocal - 0.16) / 0.84 * (0.65 + 0.35 * audio.VocalPresence) * voxScale, 0.0, 1.0)
                : 0.0;

            lg = Math.Clamp(Math.Max(bassGroove * stereoL, instLayer * 0.65 * stereoL), 0.0, 1.0);
            rg = Math.Clamp(Math.Max(bassGroove * stereoR, instLayer * 0.75 * stereoR + cls.Drums * 0.22), 0.0, 1.0);
            lt = Math.Clamp(cleanVocal * 0.88, 0.0, 1.0);
            rt = Math.Clamp(Math.Max(cleanVocal * 0.78, audio.VocalAir * cls.Vocal * 0.90), 0.0, 1.0);
        }

        ev1 = (cls.Bass > 0.60f && audio.BeatPulse > 0.50f) ? HapticsEventTag.BassDrop
            : (cls.Vocal > 0.45f ? HapticsEventTag.Vocal : HapticsEventTag.None);
        ev2 = (ev1 == HapticsEventTag.BassDrop && cls.Vocal > 0.40f) ? HapticsEventTag.Vocal : HapticsEventTag.None;
    }

    void EvaluateCinemaLocked(
        AudioFrame audio,
        AudioClassification cls,
        HapticsTuning tuning,
        double rawRms,
        double rawSpike,
        out double lg,
        out double rg,
        out double lt,
        out double rt,
        out HapticsEventTag ev1,
        out HapticsEventTag ev2)
    {
        double punchScale = tuning.ImpactPunch / 100.0;
        double rawLoudness = Math.Clamp(rawRms / 0.22, 0.0, 1.0);
        bool overlyLoud = rawRms >= 0.17 || rawSpike >= 0.11;
        double pan = Math.Clamp(audio.StereoPan, -1.0, 1.0);
        double leftBias = Math.Clamp(1.0 - Math.Max(0.0, pan) * 0.75, 0.25, 1.0);
        double rightBias = Math.Clamp(1.0 - Math.Max(0.0, -pan) * 0.75, 0.25, 1.0);

        bool dialogueActive = (cls.Dialogue > 0.35f || cls.Vocal > 0.38f) && cls.Explosion < 0.42f && !overlyLoud;
        if (dialogueActive)
        {
            lg = rg = lt = rt = 0.0;
            ev1 = HapticsEventTag.DialogueDuck;
            ev2 = HapticsEventTag.None;
            return;
        }

        double lfe = cls.Explosion > 0.42f ? cls.Explosion * 0.85 * punchScale * Math.Clamp(0.4 + 0.6 * rawLoudness, 0.4, 1.0) : 0.0;
        double sharp = overlyLoud && (cls.Gunshot > 0.45f || cls.Explosion > 0.55f) ? Math.Max(cls.Gunshot, cls.Explosion) * 0.55 * punchScale : 0.0;

        lg = Math.Clamp(lfe * leftBias, 0.0, 1.0);
        rg = Math.Clamp(lfe * rightBias + Math.Max(0f, cls.Bass - 0.55f) * 0.25 * rawLoudness, 0.0, 1.0);
        lt = Math.Clamp(sharp * leftBias * 0.65, 0.0, 1.0);
        rt = Math.Clamp(sharp * rightBias * 0.75, 0.0, 1.0);

        ev1 = cls.Explosion > 0.42f ? HapticsEventTag.Explosion : (sharp > 0.20 ? HapticsEventTag.Gunshot : HapticsEventTag.None);
        ev2 = HapticsEventTag.None;
    }

    void EvaluateSportsLocked(
        AudioFrame audio,
        AudioClassification cls,
        HapticsTuning tuning,
        double rawRms,
        double rawSpike,
        bool isOverlyLoud,
        out double lg,
        out double rg,
        out double lt,
        out double rt,
        out HapticsEventTag ev1,
        out HapticsEventTag ev2)
    {
        double punchScale = tuning.ImpactPunch / 100.0;
        double rawLoudness = Math.Clamp(rawRms / 0.22, 0.0, 1.0);
        double hit = (cls.Gunshot > 0.45f || rawSpike >= 0.09) ? Math.Max(cls.Gunshot, rawSpike * 3.0) * 0.45 * punchScale * Math.Clamp(rawLoudness, 0.3, 1.0) : 0.0;

        lg = Math.Clamp(isOverlyLoud ? hit * 0.85 : 0.0, 0.0, 1.0);
        rg = Math.Clamp(hit, 0.0, 1.0);
        lt = Math.Clamp(isOverlyLoud ? hit * 0.35 : 0.0, 0.0, 1.0);
        rt = Math.Clamp(isOverlyLoud ? hit * 0.45 : 0.0, 0.0, 1.0);
        ev1 = hit > 0.14 ? HapticsEventTag.BassDrop : HapticsEventTag.None;
        ev2 = HapticsEventTag.None;
    }

    HapticsEvaluation SmoothAndBudgetLocked(
        HapticsFrame target,
        AudioClassification cls,
        HapticsEventTag ev1,
        HapticsEventTag ev2,
        HapticsGenreProfile resolvedProfile,
        bool isMenuMuted,
        HapticsTuning tuning,
        double dt)
    {
        double master = tuning.MasterIntensity / 100.0;
        double trigScale = tuning.TriggerBalance / 100.0;

        double tierMax = tuning.ComfortTier switch
        {
            HapticsComfortTier.Feather => resolvedProfile == HapticsGenreProfile.Music ? 0.88 : 0.72,
            HapticsComfortTier.Balanced => 0.92,
            _ => 1.0,
        };

        double lg = Math.Clamp(target.LeftGrip * master, 0.0, tierMax);
        double rg = Math.Clamp(target.RightGrip * master, 0.0, tierMax);
        double lt = Math.Clamp(target.LeftTrigger * master * trigScale, 0.0, tierMax);
        double rt = Math.Clamp(target.RightTrigger * master * trigScale, 0.0, tierMax);

        double peak = Math.Max(Math.Max(lg, rg), Math.Max(lt, rt));
        if (peak > 0.80)
            sustainedHighSec = Math.Min(4.0, sustainedHighSec + dt);
        else
            sustainedHighSec = Math.Max(0.0, sustainedHighSec - dt * 1.8);

        if (sustainedHighSec > 1.2)
        {
            double fatigueScale = Math.Clamp(1.0 - (sustainedHighSec - 1.2) * 0.25, 0.62, 1.0);
            lg *= fatigueScale;
            rg *= fatigueScale;
            lt *= fatigueScale;
            rt *= fatigueScale;
        }

        double attAlpha = 1.0 - Math.Exp(-dt / 0.012);
        double relAlpha = 1.0 - Math.Exp(-dt / 0.055);
        lastLg = StepMotor(lastLg, lg, attAlpha, relAlpha);
        lastRg = StepMotor(lastRg, rg, attAlpha, relAlpha);
        lastLt = StepMotor(lastLt, lt, attAlpha, relAlpha);
        lastRt = StepMotor(lastRt, rt, attAlpha, relAlpha);

        double frameLoad = (lastLg + lastRg + lastLt + lastRt) * 0.25;
        avgDuty += (frameLoad - avgDuty) * (1.0 - Math.Exp(-dt / 4.0));

        return new HapticsEvaluation(
            new HapticsFrame(lastLg, lastRg, lastLt, lastRt),
            cls,
            ev1,
            ev2,
            resolvedProfile,
            isMenuMuted,
            avgDuty);
    }

    static double StepMotor(double cur, double tgt, double att, double rel)
    {
        double next = cur + (tgt - cur) * (tgt > cur ? att : rel);
        return next < 0.008 ? 0.0 : Math.Clamp(next, 0.0, 1.0);
    }

    static double ComfortTextureScale(HapticsComfortTier tier) => tier switch
    {
        HapticsComfortTier.Feather => 0.42,
        HapticsComfortTier.Balanced => 0.72,
        _ => 1.0,
    };

    static (HapticsFrame Motors, HapticsEventTag Primary, HapticsEventTag Secondary) EvaluateDemoLocked(
        HapticsDemoKind kind,
        double u,
        AudioClassification cls,
        HapticsTuning tuning)
    {
        double punch = Math.Clamp(tuning.ImpactPunch / 100.0, 0.2, 1.0);
        double tex = Math.Clamp(tuning.TextureDetail / 100.0, 0.2, 1.0);
        return kind switch
        {
            HapticsDemoKind.DirtRoad => (
                new HapticsFrame(
                    0.0,
                    (0.10 + 0.10 * Math.Max(0, Math.Cos(u * 37.0))) * tex,
                    0.0,
                    0.0),
                HapticsEventTag.GravelRight,
                HapticsEventTag.None),

            HapticsDemoKind.GunshotBurst => (
                new HapticsFrame(
                    Math.Exp(-((u * 3.0) % 1.0) * 7.5) * 0.35 * punch,
                    Math.Exp(-((u * 3.0) % 1.0) * 7.5) * 0.50 * punch,
                    0.0,
                    Math.Exp(-((u * 3.0) % 1.0) * 9.0) * 0.90 * punch),
                HapticsEventTag.Gunshot,
                HapticsEventTag.None),

            HapticsDemoKind.NitroBoost => (
                new HapticsFrame(
                    Math.Sin(u * Math.PI) * 0.50 * punch,
                    Math.Sin(u * Math.PI) * 0.55 * punch,
                    0.0,
                    Math.Sin(u * Math.PI) * 0.75 * punch),
                HapticsEventTag.BoostSurge,
                HapticsEventTag.GearShift),

            HapticsDemoKind.AbsBrake => (
                new HapticsFrame(
                    0.0,
                    0.14 * (0.5 + 0.5 * Math.Sin(u * 55.0)),
                    (0.42 + 0.45 * (0.5 + 0.5 * Math.Sin(u * 65.0))) * punch,
                    0.0),
                HapticsEventTag.AbsBrake,
                HapticsEventTag.None),

            HapticsDemoKind.ExplosionWave => (
                new HapticsFrame(
                    Math.Exp(-u * 3.2) * 0.92 * punch,
                    Math.Exp(-Math.Max(0, u - 0.08) * 3.0) * 0.85 * punch,
                    Math.Exp(-u * 5.0) * 0.45 * punch,
                    Math.Exp(-u * 5.0) * 0.45 * punch),
                HapticsEventTag.Explosion,
                HapticsEventTag.None),

            HapticsDemoKind.BassDrop or _ => (
                new HapticsFrame(
                    Math.Exp(-u * 2.8) * 0.85 * punch,
                    Math.Exp(-u * 2.8) * 0.85 * punch,
                    0.35 * Math.Sin(u * Math.PI),
                    0.30 * Math.Sin(u * Math.PI)),
                HapticsEventTag.BassDrop,
                cls.Vocal > 0.2f ? HapticsEventTag.Vocal : HapticsEventTag.None),
        };
    }
}
