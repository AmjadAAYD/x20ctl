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

    // Music dynamic spectrum & anti-drone breathing state
    double musicPeakRef = 0.22;
    double musicSilenceSec;
    double musicBassBed;
    double musicInstBed;
    double musicVocalBed;
    double musicSustainedSec;

    // Input-first short tactical envelopes (discrete taps instead of continuous drone)
    double stepTapRemaining;
    double stepTapLevel;
    double jumpTapRemaining;
    double airborneWindowSec;
    double landingTapRemaining;
    double dashRemaining;
    double attackTapRemaining;
    double heavySwingRemaining;

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
            musicPeakRef = 0.22;
            musicSilenceSec = musicBassBed = musicInstBed = musicVocalBed = musicSustainedSec = 0;
            jumpTapRemaining = airborneWindowSec = landingTapRemaining = dashRemaining = attackTapRemaining = heavySwingRemaining = 0;
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
                || (cls.Gunshot > 0.58f && input.RightTrigger > 0.25)
                || (effectiveRawSpike >= 0.08 && effectiveRawRms >= 0.11 && cls.Instruments < 0.65f);

            bool cursorMute = isGameContext && tuning.MenuAutoMute && input.CursorVisible && input.PressedButtons == PadActionButtons.None;
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
                    EvaluateMusicLocked(audio, cls, tuning, roles, effectiveRawRms, effectiveRawSpike, dt, out lg, out rg, out lt, out rt, out ev1, out ev2);
                    break;
                case HapticsGenreProfile.Cinema:
                    EvaluateCinemaLocked(audio, cls, tuning, effectiveRawRms, effectiveRawSpike, out lg, out rg, out lt, out rt, out ev1, out ev2);
                    break;
                case HapticsGenreProfile.SportsArcade:
                    EvaluateSportsLocked(audio, cls, tuning, effectiveRawRms, effectiveRawSpike, isOverlyLoud, out lg, out rg, out lt, out rt, out ev1, out ev2);
                    break;
            }

            prevRightTrigger = input.RightTrigger;

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

        if (cls.Dialogue > 0.50f && cls.Bass < 0.35f && cls.Drums < 0.30f && cls.Gunshot < 0.35f && input.RightTrigger < 0.25)
            return HapticsGenreProfile.Cinema;

        if (cls.TireScreech > 0.55f && input.RightTrigger > 0.30)
            return HapticsGenreProfile.Racing;

        return HapticsGenreProfile.Music;
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
        heavySwingRemaining = Math.Max(0.0, heavySwingRemaining - dt);

        // 1. Input-First Discrete Action Triggers: Jump (A), Dash (B/L3 or FPS RB), Light Attack (X/Y, + RPG RB), Heavy Swing (RPG RT)
        if ((input.PressedButtons & PadActionButtons.A) != 0)
        {
            jumpTapRemaining = 0.028;
            airborneWindowSec = 1.05;
        }
        bool dashBtnPressed = isMeleePrimary
            ? (input.PressedButtons & (PadActionButtons.B | PadActionButtons.L3)) != 0
            : (input.PressedButtons & (PadActionButtons.B | PadActionButtons.RB | PadActionButtons.L3)) != 0;
        bool sprintOnset = input.LeftStickMagnitude > 0.88 && !prevSprinting;
        prevSprinting = input.LeftStickMagnitude > 0.88;
        if (dashBtnPressed || sprintOnset)
            dashRemaining = 0.095;

        bool lightAttackPressed = isMeleePrimary
            ? (input.PressedButtons & (PadActionButtons.X | PadActionButtons.Y | PadActionButtons.RB)) != 0
            : (input.PressedButtons & (PadActionButtons.X | PadActionButtons.Y)) != 0;
        if (lightAttackPressed)
            attackTapRemaining = 0.052;

        if (isMeleePrimary && input.RightTrigger > 0.50 && prevRightTrigger <= 0.50)
            heavySwingRemaining = 0.085;

        // Detect jump landing impact after 0.22s in air
        if (airborneWindowSec is > 0.05 and < 0.82 && (rawSpike > 0.028 || audio.SpectralFlux > 0.28f))
        {
            landingTapRemaining = 0.038;
            airborneWindowSec = 0.0;
        }

        // 2. Walking / Running / Grass footsteps: Right-Grip-ONLY faint 26 ms micro-taps, NEVER VOM VOM VOM!
        // Reject background music themes: require LeftStick movement AND non-silent movement audio, paused while airborne.
        double moveMag = input.LeftStickMagnitude;
        bool onGrassOrDirt = cls.GravelSurface > 0.30f;
        bool movingQuietAudio = rawRms > 0.006 && (onGrassOrDirt || cls.GravelSurface > 0.20f || audio.SpectralFlux > 0.16f || rawSpike > 0.012);
        if (moveMag > 0.25 && airborneWindowSec <= 0.05 && stepCooldownSec <= 0.0 && movingQuietAudio)
        {
            double maxStep = onGrassOrDirt ? 0.13 : 0.14;
            stepTapLevel = Math.Clamp((0.055 + 0.06 * texScale) * comfortTex * (0.45 + 0.55 * Math.Min(1.0, rawLoudness * 1.6)), 0.05, maxStep);
            stepTapRemaining = 0.026;
            stepCooldownSec = onGrassOrDirt
                ? (moveMag > 0.85 ? 0.26 : 0.36)
                : (moveMag > 0.85 ? 0.31 : 0.42);
            ev2 = onGrassOrDirt ? HapticsEventTag.GravelRight : HapticsEventTag.Footstep;
        }

        double walkTapRg = stepTapRemaining > 0 ? stepTapLevel : 0.0;
        double jumpRg = jumpTapRemaining > 0 ? 0.10 * comfortTex : 0.0;
        double landRg = landingTapRemaining > 0 ? 0.16 * comfortTex : 0.0;
        double landLg = landingTapRemaining > 0 && rawLoudness > 0.35 ? 0.08 * comfortTex : 0.0;
        double dashFwooshRg = dashRemaining > 0 ? (dashRemaining / 0.095) * 0.16 * comfortTex : 0.0;
        double swordSnapRg = attackTapRemaining > 0 ? (isMeleePrimary ? 0.28 : 0.22) * punchScale : 0.0;
        double heavySwingRg = heavySwingRemaining > 0 ? 0.34 * punchScale : 0.0;
        double heavySwingLg = heavySwingRemaining > 0 ? 0.18 * punchScale : 0.0;
        if (heavySwingRg > 0.08) ev1 = HapticsEventTag.MeleeClash;
        if (swordSnapRg > 0.08 && ev2 == HapticsEventTag.None) ev2 = HapticsEventTag.MeleeClash;

        // 3. Shooting (FPS RT pulled + gunshot transient): crisp Right Trigger + Right Grip recoil, Left Grip only if heavy.
        bool shootingActive = input.RightTrigger > 0.25 && cls.Gunshot > 0.42f && rawRms > 0.02;
        double gunEnv = shootingActive ? Math.Pow((cls.Gunshot - 0.36) / 0.64, 0.90) * punchScale * Math.Clamp(0.45 + 0.55 * rawLoudness, 0.35, 1.0) : 0.0;
        double gunRt = gunEnv * 0.82;
        double gunRg = gunEnv * 0.38;
        double gunLg = (gunEnv > 0.35 && audio.Bass > 0.55f && isOverlyLoud) ? gunEnv * 0.45 : 0.0;
        if (gunEnv > 0.15) ev1 = HapticsEventTag.Gunshot;

        // 4. Sword Parry / Sharp Clash (loud only): triggers stay quiet on light swings!
        double parryClang = (cls.MeleeClash > 0.55f && (attackTapRemaining > 0 || heavySwingRemaining > 0 || isOverlyLoud) && rawRms > 0.03)
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

        // 5b. Taking Damage / Health Loss (non-button sharp thud outside background music or dialogue):
        bool damageThud = !shootingActive && attackTapRemaining <= 0 && heavySwingRemaining <= 0 && !heavyHitOrExplosion
            && rawSpike >= 0.065 && rawRms >= 0.06 && cls.Instruments < 0.65f && cls.Dialogue < 0.35f;
        double damageImpact = damageThud
            ? Math.Clamp(rawSpike * 3.6 * Math.Clamp(0.45 + 0.55 * rawLoudness, 0.45, 1.0) * punchScale, 0.14, 0.48)
            : 0.0;
        if (damageImpact > 0.12 && ev1 == HapticsEventTag.None) ev1 = HapticsEventTag.MeleeClash;

        double subtleRg = Math.Max(Math.Max(walkTapRg, jumpRg), Math.Max(landRg, Math.Max(dashFwooshRg, swordSnapRg)));
        double duckSubtle = Math.Clamp(1.0 - Math.Max(gunEnv, Math.Max(explosionPower, damageImpact)) * 0.75, 0.15, 1.0);

        // Triggers stay ZERO for walking, grass, jumping, dashing, light hits, heavy swings, normal damage, and quiet music!
        lg = Math.Clamp(explosionPower * 0.90 + gunLg + landLg + heavySwingLg + damageImpact * 0.55, 0.0, 1.0);
        rg = Math.Clamp(subtleRg * duckSubtle + gunRg + explosionPower * 0.65 + parryClang * 0.55 + heavySwingRg + damageImpact * 0.82, 0.0, 1.0);
        lt = Math.Clamp((isOverlyLoud ? explosionPower * 0.45 : 0.0) + (isOverlyLoud ? parryClang * 0.55 : 0.0), 0.0, 1.0);
        rt = Math.Clamp(gunRt + (isOverlyLoud ? explosionPower * 0.45 + parryClang * 0.65 : 0.0), 0.0, 1.0);
    }

    void EvaluateMusicLocked(
        AudioFrame audio,
        AudioClassification cls,
        HapticsTuning tuning,
        MotorRoleMap roles,
        double rawRms,
        double rawSpike,
        double dt,
        out double lg,
        out double rg,
        out double lt,
        out double rt,
        out HapticsEventTag ev1,
        out HapticsEventTag ev2)
    {
        // 1. Anchored un-AGC'd loudness so quiet song intros stay faint instead of jumping to full blast.
        double effLoud;
        if (audio.RawRms > 0f)
        {
            if (rawRms < 0.005)
            {
                musicSilenceSec = Math.Min(10.0, musicSilenceSec + dt);
                if (musicSilenceSec > 2.5)
                    musicPeakRef += (0.22 - musicPeakRef) * (1.0 - Math.Exp(-dt / 4.0));
            }
            else
            {
                musicSilenceSec = 0.0;
                if (rawRms > musicPeakRef)
                {
                    musicPeakRef += (rawRms - musicPeakRef) * (1.0 - Math.Exp(-dt / 0.25));
                }
                else
                {
                    double targetFloor = Math.Max(0.13, rawRms);
                    musicPeakRef += (targetFloor - musicPeakRef) * (1.0 - Math.Exp(-dt / 35.0));
                }
            }

            double physLoud = Math.Clamp((rawRms - 0.005) / Math.Max(0.125, musicPeakRef - 0.005), 0.0, 1.0);
            double spikeBoost = Math.Clamp(rawSpike * 2.2, 0.0, 0.30);
            effLoud = Math.Clamp(physLoud + spikeBoost * (0.35 + 0.65 * physLoud), 0.0, 1.0);
        }
        else
        {
            effLoud = Math.Clamp(audio.Level, 0.0, 1.0);
        }

        if (effLoud < 0.015)
        {
            double clearAlpha = 1.0 - Math.Exp(-dt / 0.12);
            musicBassBed *= 1.0 - clearAlpha;
            musicInstBed *= 1.0 - clearAlpha;
            musicVocalBed *= 1.0 - clearAlpha;
            musicSustainedSec = Math.Max(0.0, musicSustainedSec - dt * 3.0);
            lg = rg = lt = rt = 0.0;
            ev1 = ev2 = HapticsEventTag.None;
            return;
        }

        // 2. Rolling baseline subtraction + anti-drone breath gate so steady chords/pads leave breathing room.
        double bassSig = Math.Clamp(Math.Max(cls.Bass * 0.85, audio.Bass * 0.75) * 0.65 + Math.Max(audio.BassPunch, cls.Drums) * 0.50, 0.0, 1.0);
        double instSig = Math.Clamp(Math.Max(cls.Instruments * 0.85, audio.InstrumentEnergy * 0.80) * 0.72 + audio.TrebleTransient * 0.45, 0.0, 1.0);
        double vocalSig = cls.Vocal > 0.22f
            ? Math.Clamp((cls.Vocal - 0.18) / 0.82 * (0.60 + 0.40 * audio.VocalPresence), 0.0, 1.0)
            : 0.0;

        musicBassBed += (bassSig - musicBassBed) * (1.0 - Math.Exp(-dt / 0.36));
        musicInstBed += (instSig - musicInstBed) * (1.0 - Math.Exp(-dt / 0.42));
        musicVocalBed += (vocalSig - musicVocalBed) * (1.0 - Math.Exp(-dt / 0.30));

        double bassDelta = Math.Clamp(Math.Max(0.0, bassSig - musicBassBed * 0.92 - 0.06) * 3.0, 0.0, 1.0);
        double instDelta = Math.Clamp(Math.Max(0.0, instSig - musicInstBed * 0.92 - 0.05) * 2.8, 0.0, 1.0);
        double vocalOnset = Math.Clamp(Math.Max(0.0, vocalSig - musicVocalBed * 0.88 - 0.04) * 2.8, 0.0, 1.0);

        double bassCrest = Math.Clamp(Math.Max(audio.BeatPulse * 0.95, Math.Max(audio.BassPunch * 0.85, bassDelta)), 0.0, 1.0);
        double instCrest = Math.Clamp(Math.Max(audio.TrebleTransient * 0.90, Math.Max(audio.SpectralFlux * 0.75, instDelta)), 0.0, 1.0);
        double vocalPhrase = vocalSig > 0.02
            ? Math.Clamp(vocalOnset * 0.75 + vocalSig * 0.25 * (0.20 + 0.80 * audio.DynamicContrast), 0.0, 1.0)
            : 0.0;

        bool freshPulse = audio.BeatPulse > 0.45f || rawSpike > 0.045 || bassCrest > 0.48;
        double heldMotor = Math.Max(lastLg, lastRg);
        if (heldMotor > 0.26 && !freshPulse)
            musicSustainedSec = Math.Min(3.0, musicSustainedSec + dt);
        else if (heldMotor < 0.16)
            musicSustainedSec = Math.Max(0.0, musicSustainedSec - dt * 2.8);
        else if (freshPulse)
            musicSustainedSec = Math.Max(0.0, musicSustainedSec - dt * 1.2);

        double antiDrone = Math.Clamp(1.0 - Math.Max(0.0, musicSustainedSec - 0.45) * 0.55, 0.28, 1.0);

        double punchScale = tuning.ImpactPunch / 100.0;
        double voxScale = tuning.VocalFocus / 100.0;
        double texScale = tuning.TextureDetail / 100.0;
        double contrast = Math.Clamp(tuning.DynamicContrast / 100.0, 0.25, 1.0);
        double comfortGain = tuning.ComfortTier switch
        {
            HapticsComfortTier.Feather => 0.74,
            HapticsComfortTier.Balanced => 0.88,
            _ => 1.0,
        };

        if (!roles.IsAllAuto)
        {
            var baseline = AudioHapticsEngine.Evaluate(audio with { RawRms = 0f }, AudioHapticsMode.AutoAdaptive, tuning.MasterIntensity, roles, elapsedSeconds);
            double normMaster = tuning.MasterIntensity > 0 ? 100.0 / tuning.MasterIntensity : 0.0;
            double bLg = baseline.LeftGrip * normMaster;
            double bRg = baseline.RightGrip * normMaster;
            double bLt = baseline.LeftTrigger * normMaster;
            double bRt = baseline.RightTrigger * normMaster;
            double specScale = Math.Pow(effLoud, 1.15) * comfortGain * antiDrone;
            double lgSpec = effLoud < 0.24 ? 0.0 : Math.Pow(Math.Clamp((effLoud - 0.24) / 0.76, 0.0, 1.0), 1.12) * comfortGain * antiDrone;
            double gateV = cls.Vocal > 0.18f || audio.VocalPresence > 0.28f ? 1.0 : 0.20;
            lg = Math.Clamp(bLg * lgSpec, 0.0, 1.0);
            rg = Math.Clamp(bRg * specScale, 0.0, 1.0);
            lt = Math.Clamp((roles.LeftTrigger is MotorAudioRole.LeadVocals or MotorAudioRole.VocalAir ? bLt * gateV : bLt) * specScale, 0.0, 1.0);
            rt = Math.Clamp((roles.RightTrigger is MotorAudioRole.LeadVocals or MotorAudioRole.VocalAir ? bRt * gateV : bRt) * specScale, 0.0, 1.0);
        }
        else
        {
            double bedAllowance = (1.0 - contrast * 0.78) * texScale * antiDrone;

            // Right Grip: faint whisper on quiet intros, medium groove in verses, crisp highs on drops.
            double rgSpectrum = Math.Pow(effLoud, 1.15);
            double rgBed = Math.Max(instSig * 0.55, bassSig * 0.35) * bedAllowance * 0.42;
            double rgPulse = Math.Max(instCrest * 0.72, Math.Max(bassCrest * 0.62, audio.BeatPulse * 0.68)) * (0.45 + 0.55 * punchScale);
            double rgRaw = (rgPulse * 0.85 + rgBed) * rgSpectrum * comfortGain * antiDrone;
            if (effLoud < 0.26)
            {
                double faintFloor = Math.Max(instSig, Math.Max(bassSig * 0.70, vocalSig * 0.60)) * effLoud * 0.55 * comfortGain * antiDrone;
                rgRaw = Math.Clamp(Math.Max(rgRaw, faintFloor), 0.0, 0.16);
            }

            // Left Grip: 0% on faint intros, medium kick pulses, full weight only on loud bass drops.
            double lgGate = Math.Clamp((effLoud - 0.24) / 0.76, 0.0, 1.0);
            double lgSpectrum = Math.Pow(lgGate, 1.12);
            double lgPulse = Math.Clamp(Math.Max(audio.BeatPulse * 1.05, bassCrest * 0.98) * (0.55 + 0.55 * punchScale), 0.0, 1.0);
            double lgBed = bassSig * bedAllowance * 0.20;
            double lgRaw = (lgPulse + lgBed) * lgSpectrum * comfortGain * antiDrone;

            // Triggers: lyrical vocal phrasing + sharp loud drop peaks, capped faint during intros.
            double trigSpectrum = Math.Pow(effLoud, 1.15);
            double vocalDrive = vocalPhrase * voxScale * trigSpectrum * comfortGain * antiDrone;
            double loudDropTrig = (effLoud >= 0.68 && (audio.BeatPulse > 0.65f || rawSpike > 0.08))
                ? Math.Clamp((effLoud - 0.60) / 0.40, 0.0, 1.0) * Math.Max(audio.BeatPulse, rawSpike * 4.5) * 0.55 * punchScale * comfortGain
                : 0.0;
            double ltRaw = Math.Clamp(Math.Max(vocalDrive * 0.88, loudDropTrig * 0.85), 0.0, 1.0);
            double rtRaw = Math.Clamp(Math.Max(vocalDrive * (0.68 + 0.30 * audio.VocalAir), loudDropTrig * 0.95), 0.0, 1.0);
            if (effLoud < 0.26)
            {
                ltRaw = Math.Min(ltRaw, 0.14);
                rtRaw = Math.Min(rtRaw, 0.14);
            }

            double stereoL = Math.Clamp(1.0 - Math.Max(0.0, audio.StereoPan) * 0.45, 0.55, 1.0);
            double stereoR = Math.Clamp(1.0 - Math.Max(0.0, -audio.StereoPan) * 0.45, 0.55, 1.0);
            lg = Math.Clamp(lgRaw * stereoL, 0.0, 1.0);
            rg = Math.Clamp(rgRaw * stereoR, 0.0, 1.0);
            lt = ltRaw;
            rt = rtRaw;
        }

        ev1 = (effLoud >= 0.55 && (cls.Bass > 0.55f || audio.Bass > 0.55f) && audio.BeatPulse > 0.48f) ? HapticsEventTag.BassDrop
            : (vocalPhrase > 0.25 && effLoud >= 0.15 ? HapticsEventTag.Vocal : HapticsEventTag.None);
        ev2 = (ev1 == HapticsEventTag.BassDrop && vocalPhrase > 0.22) ? HapticsEventTag.Vocal : HapticsEventTag.None;
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
            HapticsComfortTier.Feather => resolvedProfile == HapticsGenreProfile.Music ? 0.78 : 0.72,
            HapticsComfortTier.Balanced => 0.90,
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
