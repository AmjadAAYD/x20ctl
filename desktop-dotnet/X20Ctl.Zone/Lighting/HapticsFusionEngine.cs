namespace X20Ctl.Zone.Lighting;

/// <summary>
/// Universal 4-motor comfort-first Haptics Fusion Engine combining live WASAPI FFT spectral descriptors,
/// embedded ML stem/SFX classification, and live controller inputs (triggers, sticks, buttons, menu cursor)
/// into DualSense-class grip and trigger sensations with battery-friendly anti-fatigue protection.
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
    int footstepPhase;
    bool prevSprinting;
    double sprintBurstRemaining;
    double prevRightTrigger;
    double gearShiftRemaining;

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
            footstepPhase = 0;
            prevSprinting = false;
            sprintBurstRemaining = gearShiftRemaining = 0;
            prevRightTrigger = 0;
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

            // Active one-click Demo Effect overrides background safely so user feels exact signature physics.
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

            bool isGameProfile = profile is HapticsGenreProfile.Racing or HapticsGenreProfile.FpsAction or HapticsGenreProfile.RpgMelee or HapticsGenreProfile.SportsArcade;
            bool cursorMute = isGameProfile && tuning.MenuAutoMute && input.CursorVisible;
            bool idleMute = isGameProfile && tuning.MenuAutoMute && input.IsIdle && cls.Explosion < 0.55f && cls.Gunshot < 0.55f;
            bool wantMute = cursorMute || idleMute;
            double muteTarget = wantMute ? 1.0 : 0.0;
            menuMuteFactor += (muteTarget - menuMuteFactor) * (1.0 - Math.Exp(-dt / (wantMute ? 0.08 : 0.14)));

            if (menuMuteFactor > 0.96)
            {
                avgDuty *= Math.Exp(-dt / 1.5);
                lastLg = lastRg = lastLt = lastRt = 0;
                return new HapticsEvaluation(HapticsFrame.Zero, cls, HapticsEventTag.MenuMuted, HapticsEventTag.None, profile, true, avgDuty);
            }

            var resolved = profile == HapticsGenreProfile.Auto ? InferAutoProfile(audio, cls, input) : profile;

            double lg = 0, rg = 0, lt = 0, rt = 0;
            var ev1 = HapticsEventTag.None;
            var ev2 = HapticsEventTag.None;

            switch (resolved)
            {
                case HapticsGenreProfile.Racing:
                    EvaluateRacingLocked(audio, cls, input, tuning, dt, out lg, out rg, out lt, out rt, out ev1, out ev2);
                    break;
                case HapticsGenreProfile.FpsAction:
                    EvaluateFpsLocked(audio, cls, input, tuning, dt, out lg, out rg, out lt, out rt, out ev1, out ev2);
                    break;
                case HapticsGenreProfile.Music:
                    EvaluateMusicLocked(audio, cls, tuning, roles, out lg, out rg, out lt, out rt, out ev1, out ev2);
                    break;
                case HapticsGenreProfile.Cinema:
                    EvaluateCinemaLocked(audio, cls, tuning, out lg, out rg, out lt, out rt, out ev1, out ev2);
                    break;
                case HapticsGenreProfile.RpgMelee:
                    EvaluateRpgLocked(audio, cls, tuning, out lg, out rg, out lt, out rt, out ev1, out ev2);
                    break;
                case HapticsGenreProfile.SportsArcade:
                    EvaluateSportsLocked(audio, cls, tuning, out lg, out rg, out lt, out rt, out ev1, out ev2);
                    break;
            }

            double passThrough = Math.Clamp(1.0 - menuMuteFactor, 0.0, 1.0);
            var rawFrame = new HapticsFrame(lg * passThrough, rg * passThrough, lt * passThrough, rt * passThrough);
            return SmoothAndBudgetLocked(rawFrame, cls, ev1, ev2, resolved, wantMute, tuning, dt);
        }
    }

    static HapticsGenreProfile InferAutoProfile(AudioFrame audio, AudioClassification cls, LivePadInput input)
    {
        // Music dominates when clear vocals, rhythmic groove, or harmony instruments lead without heavy gunfire/screech.
        if ((cls.Vocal > 0.35f || cls.Drums > 0.38f || cls.Instruments > 0.42f)
            && cls.Gunshot < 0.45f && cls.TireScreech < 0.45f)
            return HapticsGenreProfile.Music;

        if (cls.Dialogue > 0.48f && cls.Gunshot < 0.35f && input.RightTrigger < 0.25)
            return HapticsGenreProfile.Cinema;

        if (cls.TireScreech > 0.42f || (cls.EngineRumble > 0.45f && input.RightTrigger > 0.30))
            return HapticsGenreProfile.Racing;

        return HapticsGenreProfile.FpsAction;
    }

    void EvaluateRacingLocked(
        AudioFrame audio,
        AudioClassification cls,
        LivePadInput input,
        HapticsTuning tuning,
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
        double pan = Math.Clamp(audio.StereoPan, -1.0, 1.0);
        double leftBias = Math.Clamp(1.0 - Math.Max(0.0, pan) * 0.72, 0.25, 1.0);
        double rightBias = Math.Clamp(1.0 - Math.Max(0.0, -pan) * 0.72, 0.25, 1.0);

        ev1 = HapticsEventTag.None;
        ev2 = HapticsEventTag.None;

        // 1. Dirt / Gravel / Curb tactile micro-pebbles (stochastic crisp taps, never steady drone).
        gravelPhase += dt * (28.0 + 18.0 * cls.GravelSurface);
        double pebbleMask = 0.35 + 0.65 * Math.Abs(Math.Sin(gravelPhase * 1.73 + Math.Cos(gravelPhase * 0.61)));
        double gravel = cls.GravelSurface > 0.26f
            ? Math.Pow((cls.GravelSurface - 0.22) / 0.78, 1.15) * (0.28 + 0.72 * texScale) * comfortTex * pebbleMask
            : 0.0;

        if (gravel > 0.06)
            ev2 = pan < -0.15 ? HapticsEventTag.GravelLeft : HapticsEventTag.GravelRight;

        // 2. Throttle-gated engine hum: gently fades ("fwoosh, then rest") on steady highway cruise.
        double engActive = cls.EngineRumble > 0.35f && input.RightTrigger > 0.08 ? cls.EngineRumble * Math.Sqrt(input.RightTrigger) : 0.0;
        if (engActive > 0.25 && cls.BoostSurge < 0.40f && cls.GravelSurface < 0.35f)
            steadyEngineSec = Math.Min(3.0, steadyEngineSec + dt);
        else
            steadyEngineSec = Math.Max(0.0, steadyEngineSec - dt * 1.5);
        double cruiseFade = Math.Clamp(1.0 - Math.Max(0.0, steadyEngineSec - 0.7) / 1.4, 0.18, 1.0);
        double engineTouch = engActive * 0.16 * texScale * comfortTex * cruiseFade;

        // 3. Gear shift kick when throttle high and spectral centroid or RPM drops briskly.
        gearShiftRemaining = Math.Max(0.0, gearShiftRemaining - dt);
        if (gearShiftRemaining <= 0 && input.RightTrigger > 0.65 && prevRightTrigger > 0.65
            && audio.CentroidVelocity < -0.32f && cls.EngineRumble > 0.30f)
        {
            gearShiftRemaining = 0.065;
        }
        prevRightTrigger = input.RightTrigger;
        double shiftKick = gearShiftRemaining > 0 ? 0.58 * punchScale : 0.0;
        if (shiftKick > 0.15) ev1 = HapticsEventTag.GearShift;

        // 4. Nitro / Turbo Boost surge: sculpted "fwoosh, then rest" across triggers -> grips.
        bool boostOn = cls.BoostSurge > 0.42f;
        if (boostOn)
            boostHoldSec = Math.Min(3.0, boostHoldSec + dt);
        else
            boostHoldSec = Math.Max(0.0, boostHoldSec - dt * 2.2);
        double boostEnvelope = boostOn
            ? cls.BoostSurge * Math.Clamp(1.0 - Math.Max(0.0, boostHoldSec - 0.35) / 1.1, 0.15, 1.0)
            : 0.0;
        double boostVal = boostEnvelope * 0.72 * punchScale;
        if (boostVal > 0.18) ev1 = HapticsEventTag.BoostSurge;

        // 5. ABS Brake chatter on Left Trigger when braking + tire screech / lockup.
        absPhase += dt * 14.0 * 2.0 * Math.PI;
        double absGate = (input.LeftTrigger > 0.32 && (cls.TireScreech > 0.32f || audio.HighFreqResonance > 0.42f))
            ? Math.Max(cls.TireScreech, audio.HighFreqResonance)
            : 0.0;
        double absPulse = absGate > 0.05 ? (0.45 + 0.55 * (0.5 + 0.5 * Math.Sin(absPhase))) * absGate * 0.75 * punchScale : 0.0;
        if (absPulse > 0.15)
            ev1 = HapticsEventTag.AbsBrake;

        // 6. Collisions / Explosions.
        double crash = cls.Explosion > 0.40f ? cls.Explosion * 0.90 * punchScale : 0.0;
        if (crash > 0.25) ev1 = HapticsEventTag.Explosion;

        // Priority gate: heavy impact or ABS ducks subtle background pebbles cleanly.
        double priorityDuck = Math.Clamp(1.0 - Math.Max(crash, boostVal) * 0.55, 0.25, 1.0);
        double roadAndEngine = (gravel + engineTouch) * priorityDuck;

        lg = Math.Clamp(roadAndEngine * leftBias + boostVal * 0.75 + crash * leftBias + shiftKick * 0.35, 0.0, 1.0);
        rg = Math.Clamp(roadAndEngine * rightBias + boostVal * 0.75 + crash * rightBias + shiftKick * 0.35, 0.0, 1.0);
        lt = Math.Clamp(absPulse + gravel * leftBias * 0.40 + boostVal * 0.45 + shiftKick * 0.40, 0.0, 1.0);
        rt = Math.Clamp(engineTouch * 1.15 + boostVal * 0.85 + shiftKick * 0.90 + gravel * rightBias * 0.35, 0.0, 1.0);
    }

    void EvaluateFpsLocked(
        AudioFrame audio,
        AudioClassification cls,
        LivePadInput input,
        HapticsTuning tuning,
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
        double pan = Math.Clamp(audio.StereoPan, -1.0, 1.0);
        double leftBias = Math.Clamp(1.0 - Math.Max(0.0, pan) * 0.78, 0.22, 1.0);
        double rightBias = Math.Clamp(1.0 - Math.Max(0.0, -pan) * 0.78, 0.22, 1.0);

        ev1 = HapticsEventTag.None;
        ev2 = HapticsEventTag.None;

        // 1. Crisp Gunshot Recoil: RT trigger active OR very clear gunshot transient.
        double shootBias = input.RightTrigger > 0.20 ? 1.0 : 0.42;
        double gunshot = cls.Gunshot > 0.38f ? Math.Pow((cls.Gunshot - 0.32) / 0.68, 0.90) * shootBias * punchScale : 0.0;
        double bassWeight = Math.Clamp(cls.Bass * 0.8 + audio.Bass * 0.4, 0.15, 1.0);
        double recoilRt = gunshot * 0.92;
        double recoilGrip = gunshot * bassWeight * 0.60;
        if (gunshot > 0.18) ev1 = HapticsEventTag.Gunshot;

        // 2. Explosions & Heavy Shockwaves: stereo-panned across grips with smooth rolloff.
        double blast = cls.Explosion > 0.40f ? cls.Explosion * 0.95 * punchScale : 0.0;
        if (blast > 0.22) ev1 = HapticsEventTag.Explosion;

        // 3. Running Footsteps & Sprint "Fwoosh, Then Rest" (micro-taps ONLY, never VOM VOM VOM!).
        stepCooldownSec = Math.Max(0.0, stepCooldownSec - dt);
        sprintBurstRemaining = Math.Max(0.0, sprintBurstRemaining - dt);
        double moveMag = input.LeftStickMagnitude;
        bool movingFast = moveMag > 0.85;
        if (movingFast && !prevSprinting)
            sprintBurstRemaining = 0.18;
        prevSprinting = movingFast;

        double stepTap = 0.0;
        int sideIdx = footstepPhase & 1;
        if (moveMag > 0.28 && stepCooldownSec <= 0.0 && gunshot < 0.15 && blast < 0.20)
        {
            // Trigger tiny 28 ms feather micro-tap on clear low-mid/flux step rhythm
            if (audio.SpectralFlux > 0.22f || cls.Drums > 0.25f)
            {
                stepTap = (0.09 + 0.06 * texScale) * comfortTex;
                stepCooldownSec = movingFast ? 0.26 : 0.38;
                footstepPhase++;
                ev2 = HapticsEventTag.Footstep;
            }
        }
        double fwoosh = sprintBurstRemaining > 0.0 ? (sprintBurstRemaining / 0.18) * 0.14 * comfortTex : 0.0;

        // 4. Reload / parry click in Left Trigger.
        double clashClick = cls.MeleeClash > 0.45f ? cls.MeleeClash * 0.35 * punchScale : 0.0;
        if (clashClick > 0.14 && ev2 == HapticsEventTag.None) ev2 = HapticsEventTag.MeleeClash;

        double duckTex = Math.Clamp(1.0 - Math.Max(gunshot, blast), 0.15, 1.0);
        lg = Math.Clamp(blast * leftBias + recoilGrip * leftBias + (sideIdx == 0 ? stepTap : stepTap * 0.2) * duckTex + fwoosh * 0.5, 0.0, 1.0);
        rg = Math.Clamp(blast * rightBias + recoilGrip * rightBias + (sideIdx == 1 ? stepTap : stepTap * 0.2) * duckTex + fwoosh * 0.5, 0.0, 1.0);
        lt = Math.Clamp(clashClick + blast * leftBias * 0.35, 0.0, 1.0);
        rt = Math.Clamp(recoilRt + clashClick * 0.5 + blast * rightBias * 0.25, 0.0, 1.0);
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
        // Music stays expressive and dynamic, pairing AI-cleaned vocals (triggers) with breathing bass/drums (grips).
        var baseline = AudioHapticsEngine.Evaluate(audio, AudioHapticsMode.AutoAdaptive, tuning.MasterIntensity, roles, elapsedSeconds);
        double punchScale = tuning.ImpactPunch / 100.0;
        double voxScale = tuning.VocalFocus / 100.0;

        if (!roles.IsAllAuto)
        {
            // Honor explicit per-motor role routing while suppressing false non-vocal trigger bleed via classifier.
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

            // Model-gated vocal clarity eliminates guitars/synths falsely singing in triggers!
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
        out double lg,
        out double rg,
        out double lt,
        out double rt,
        out HapticsEventTag ev1,
        out HapticsEventTag ev2)
    {
        double punchScale = tuning.ImpactPunch / 100.0;
        double pan = Math.Clamp(audio.StereoPan, -1.0, 1.0);
        double leftBias = Math.Clamp(1.0 - Math.Max(0.0, pan) * 0.75, 0.25, 1.0);
        double rightBias = Math.Clamp(1.0 - Math.Max(0.0, -pan) * 0.75, 0.25, 1.0);

        // Dialogue-safe ducking: spoken dialogue without explosion keeps controller calm and distraction-free.
        bool dialogueActive = cls.Dialogue > 0.42f && cls.Explosion < 0.38f && cls.Gunshot < 0.38f;
        double duck = dialogueActive ? 0.14 : 1.0;

        double lfe = Math.Max(cls.Explosion, Math.Max(0f, cls.Bass - 0.35f)) * 0.90 * punchScale * duck;
        double sharp = Math.Max(cls.Gunshot, cls.MeleeClash) * 0.70 * punchScale * duck;

        lg = Math.Clamp(lfe * leftBias, 0.0, 1.0);
        rg = Math.Clamp(lfe * rightBias, 0.0, 1.0);
        lt = Math.Clamp(sharp * leftBias * 0.65, 0.0, 1.0);
        rt = Math.Clamp(sharp * rightBias * 0.80, 0.0, 1.0);

        ev1 = dialogueActive ? HapticsEventTag.DialogueDuck
            : (cls.Explosion > 0.42f ? HapticsEventTag.Explosion
            : (cls.Gunshot > 0.42f ? HapticsEventTag.Gunshot : HapticsEventTag.None));
        ev2 = HapticsEventTag.None;
    }

    void EvaluateRpgLocked(
        AudioFrame audio,
        AudioClassification cls,
        HapticsTuning tuning,
        out double lg,
        out double rg,
        out double lt,
        out double rt,
        out HapticsEventTag ev1,
        out HapticsEventTag ev2)
    {
        double punchScale = tuning.ImpactPunch / 100.0;
        double comfortTex = ComfortTextureScale(tuning.ComfortTier);
        double pan = Math.Clamp(audio.StereoPan, -1.0, 1.0);
        double leftBias = Math.Clamp(1.0 - Math.Max(0.0, pan) * 0.72, 0.28, 1.0);
        double rightBias = Math.Clamp(1.0 - Math.Max(0.0, -pan) * 0.72, 0.28, 1.0);

        double parry = cls.MeleeClash > 0.36f ? cls.MeleeClash * 0.82 * punchScale : 0.0;
        double heavyStomp = Math.Max(cls.Explosion, Math.Max(0f, cls.Bass - 0.45f) * 0.70f) * punchScale;
        double spellSwell = cls.BoostSurge > 0.45f ? cls.BoostSurge * 0.35 * comfortTex : 0.0;

        lg = Math.Clamp(heavyStomp * leftBias * 0.85 + parry * 0.25 + spellSwell, 0.0, 1.0);
        rg = Math.Clamp(heavyStomp * rightBias * 0.85 + parry * 0.30 + spellSwell, 0.0, 1.0);
        lt = Math.Clamp(parry * 0.85 + heavyStomp * 0.20, 0.0, 1.0);
        rt = Math.Clamp(parry * 0.90 + spellSwell * 0.55, 0.0, 1.0);

        ev1 = parry > 0.25 ? HapticsEventTag.MeleeClash
            : (heavyStomp > 0.30 ? HapticsEventTag.Explosion : HapticsEventTag.None);
        ev2 = spellSwell > 0.14 ? HapticsEventTag.BoostSurge : HapticsEventTag.None;
    }

    void EvaluateSportsLocked(
        AudioFrame audio,
        AudioClassification cls,
        HapticsTuning tuning,
        out double lg,
        out double rg,
        out double lt,
        out double rt,
        out HapticsEventTag ev1,
        out HapticsEventTag ev2)
    {
        double punchScale = tuning.ImpactPunch / 100.0;
        double comfortTex = ComfortTextureScale(tuning.ComfortTier);
        double hit = Math.Max(cls.Gunshot, Math.Max(cls.Drums - 0.30f, 0f)) * 0.75 * punchScale;
        double crowd = Math.Max(0f, cls.Bass - 0.50f) * 0.28 * comfortTex;

        lg = Math.Clamp(hit * 0.70 + crowd, 0.0, 1.0);
        rg = Math.Clamp(hit * 0.70 + crowd, 0.0, 1.0);
        lt = Math.Clamp(hit * 0.55, 0.0, 1.0);
        rt = Math.Clamp(hit * 0.75, 0.0, 1.0);
        ev1 = hit > 0.24 ? HapticsEventTag.BassDrop : HapticsEventTag.None;
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

        // Anti-fatigue duty governor: gently tames sustained >80% buzz after 1.2 s while keeping transient spikes free.
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

        // Smooth release prevents harsh buzzing clicks while preserving crisp attack edges.
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
                    (0.16 + 0.14 * Math.Max(0, Math.Sin(u * 42.0))) * tex,
                    (0.16 + 0.14 * Math.Max(0, Math.Cos(u * 37.0))) * tex,
                    0.08 * tex,
                    0.12 * tex),
                u < 0.5 ? HapticsEventTag.GravelLeft : HapticsEventTag.GravelRight,
                HapticsEventTag.None),

            HapticsDemoKind.GunshotBurst => (
                new HapticsFrame(
                    Math.Exp(-((u * 3.0) % 1.0) * 7.5) * 0.48 * punch,
                    Math.Exp(-((u * 3.0) % 1.0) * 7.5) * 0.54 * punch,
                    0.0,
                    Math.Exp(-((u * 3.0) % 1.0) * 9.0) * 0.92 * punch),
                HapticsEventTag.Gunshot,
                HapticsEventTag.None),

            HapticsDemoKind.NitroBoost => (
                new HapticsFrame(
                    Math.Sin(u * Math.PI) * 0.55 * punch,
                    Math.Sin(u * Math.PI) * 0.55 * punch,
                    Math.Sin(u * Math.PI) * 0.35 * punch,
                    Math.Sin(u * Math.PI) * 0.85 * punch),
                HapticsEventTag.BoostSurge,
                HapticsEventTag.GearShift),

            HapticsDemoKind.AbsBrake => (
                new HapticsFrame(
                    0.20 * (0.5 + 0.5 * Math.Sin(u * 55.0)),
                    0.16 * (0.5 + 0.5 * Math.Sin(u * 55.0)),
                    (0.42 + 0.45 * (0.5 + 0.5 * Math.Sin(u * 65.0))) * punch,
                    0.0),
                HapticsEventTag.AbsBrake,
                HapticsEventTag.None),

            HapticsDemoKind.ExplosionWave => (
                new HapticsFrame(
                    Math.Exp(-u * 3.2) * 0.92 * punch,
                    Math.Exp(-Math.Max(0, u - 0.08) * 3.0) * 0.88 * punch,
                    Math.Exp(-u * 5.0) * 0.40 * punch,
                    Math.Exp(-u * 5.0) * 0.40 * punch),
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
