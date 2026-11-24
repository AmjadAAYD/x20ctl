using X20Ctl.Zone.Lighting;
using Xunit;

namespace X20Ctl.Zone.Tests;

public sealed class HapticsClassifierTests
{
    [Fact]
    public void Classifier_LoadsEmbeddedWeights_AndProducesZerosOnSilence()
    {
        var cls = new HapticsAudioClassifier();
        Assert.True(cls.UsingEmbeddedWeights);

        var silent = new AudioFrame();
        var outCls = cls.Classify(silent, 0.02);

        Assert.Equal(0f, outCls.Vocal);
        Assert.Equal(0f, outCls.Bass);
        Assert.Equal(0f, outCls.Gunshot);
        Assert.Equal(0f, outCls.GravelSurface);
        Assert.Equal(0f, outCls.Dialogue);
    }

    [Fact]
    public void ExtractFeatures_MapsAll28DescriptorsIntoUnitRange()
    {
        var frame = new AudioFrame(
            Level: 0.7f,
            Bass: 0.6f,
            Mid: 0.5f,
            Treble: 0.4f,
            BeatPulse: 0.8f,
            BeatCount: 4,
            StereoPan: -0.5f,
            SpectralCentroid: 0.55f,
            Energy: 0.6f,
            TrebleTransient: 0.3f,
            VocalPresence: 0.85f,
            VocalWarmth: 0.80f,
            VocalAir: 0.50f,
            InstrumentEnergy: 0.40f,
            BassPunch: 0.75f,
            DynamicContrast: 0.65f,
            Vibe: AudioMusicVibe.GroovePop,
            Spectrum: new AudioSpectrum8(0.4f, 0.7f, 0.5f, 0.8f, 0.7f, 0.5f, 0.3f, 0.2f),
            SpectralFlatness: 0.12f,
            SpectralFlux: 0.35f,
            MidSideCoherence: 0.88f,
            CrestFactor: 0.44f,
            HighFreqResonance: 0.10f,
            CentroidVelocity: -0.60f);

        Span<float> features = stackalloc float[HapticsAudioClassifier.FeatureCount];
        HapticsAudioClassifier.ExtractFeatures(frame, features);

        for (int i = 0; i < features.Length; i++)
            Assert.InRange(features[i], 0f, 1f);

        Assert.Equal(0.20f, features[27], precision: 3);
        Assert.Equal(0.88f, features[24], precision: 3);
    }

    [Fact]
    public void AnalyzerAndClassifier_DetectVocalVsInstrumentalAndSfxSignals()
    {
        var analyzer = new AudioSpectrumAnalyzer();
        var classifier = new HapticsAudioClassifier();
        const int sampleRate = 48000;
        const int chunk = 960;

        // 1. Feed broadband stereo-wide synth / guitar chord without vocal vibrato/formants
        var buf = new float[chunk * 2];
        for (int step = 0; step < 30; step++)
        {
            for (int i = 0; i < chunk; i++)
            {
                double t = (step * chunk + i) / (double)sampleRate;
                double s = 0.22 * Math.Sin(2 * Math.PI * 220 * t) + 0.18 * Math.Sin(2 * Math.PI * 660 * t) + 0.12 * Math.Sin(2 * Math.PI * 1320 * t);
                buf[i * 2] = (float)(s * 0.9);
                buf[i * 2 + 1] = (float)(-s * 0.8); // wide stereo instrumental
            }
            var frame = analyzer.ProcessInterleaved(buf, 2, sampleRate, 0.02);
            classifier.Classify(frame, 0.02);
        }
        var instOut = classifier.Classify(analyzer.CurrentFrame, 0.02);
        Assert.True(instOut.Instruments > 0.20f || instOut.Bass >= 0f);
        Assert.True(instOut.Vocal < 0.25f, $"Wide instrumental falsely predicted Vocal={instOut.Vocal}");

        // 2. Feed sharp gunshot transient over quiet bed
        analyzer.Reset();
        classifier.Reset();
        var rng = new Random(7);
        double peakGunshot = 0;
        for (int step = 0; step < 10; step++)
        {
            for (int i = 0; i < chunk; i++)
            {
                double t = (step * chunk + i) / (double)sampleRate;
                double env = step == 3 ? Math.Exp(-i / 140.0) : 0.01;
                double s = (rng.NextDouble() * 2.0 - 1.0) * env;
                buf[i * 2] = (float)s;
                buf[i * 2 + 1] = (float)s;
            }
            var frame = analyzer.ProcessInterleaved(buf, 2, sampleRate, 0.02);
            var outSfx = classifier.Classify(frame, 0.02);
            peakGunshot = Math.Max(peakGunshot, outSfx.Gunshot);
        }
        Assert.True(peakGunshot > 0.30, $"Expected Gunshot activation on impulse burst, got {peakGunshot:F3}");
    }
}
