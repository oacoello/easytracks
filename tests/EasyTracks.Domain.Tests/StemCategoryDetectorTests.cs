using EasyTracks.Domain.Models;
using EasyTracks.Domain.Services;
using Xunit;

namespace EasyTracks.Domain.Tests;

public class StemCategoryDetectorTests
{
    private readonly StemCategoryDetector _detector = new();

    [Theory]
    [InlineData("01_Click.wav", StemCategory.Click)]
    [InlineData("Song_Metronomo.mp3", StemCategory.Click)]
    [InlineData("Guide_Cues_Vocal.wav", StemCategory.Guide)]
    [InlineData("02_Drums_Stereo.wav", StemCategory.Drums)]
    [InlineData("Bateria_Acustica.wav", StemCategory.Drums)]
    [InlineData("Bass_DI.wav", StemCategory.Bass)]
    [InlineData("Bajo_Electrico.wav", StemCategory.Bass)]
    [InlineData("Gtr_Acoustic_L.wav", StemCategory.AcousticGuitar)]
    [InlineData("Guitarra_Acustica.wav", StemCategory.AcousticGuitar)]
    [InlineData("EG_Overdrive_R.wav", StemCategory.ElectricGuitar)]
    [InlineData("Guitarra_Electrica.wav", StemCategory.ElectricGuitar)]
    [InlineData("Grand_Piano.wav", StemCategory.Piano)]
    [InlineData("Keys_Rhodes.wav", StemCategory.Keys)]
    [InlineData("Synth_Lead_Arp.wav", StemCategory.Synth)]
    [InlineData("Violins_Strings.wav", StemCategory.Strings)]
    [InlineData("Trumpet_Brass.wav", StemCategory.Brass)]
    [InlineData("Lead_Vox.wav", StemCategory.LeadVocal)]
    [InlineData("Coros_BGVs.wav", StemCategory.BackingVocals)]
    [InlineData("Tambourine_Perc.wav", StemCategory.Percussion)]
    [InlineData("Riser_FX.wav", StemCategory.SoundEffects)]
    [InlineData("Unknown_Track_12.wav", StemCategory.Other)]
    public void DetectCategory_ShouldRecognizeCommonStemNames(string fileName, StemCategory expectedCategory)
    {
        var (category, confidence) = _detector.DetectCategory(fileName);

        Assert.Equal(expectedCategory, category);
        if (expectedCategory != StemCategory.Other)
        {
            Assert.True(confidence > 0.5);
        }
    }
}
