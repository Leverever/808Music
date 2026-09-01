using _808Music.Infrastructure.Audio;
using System.Text;

namespace RS1_2024_25.Tests.Infrastructure;

public sealed class TagLibAudioMetadataReaderTests
{
    [Fact]
    public async Task ReadAsync_ReadsWaveDurationAndResetsInputStream()
    {
        await using var wave = CreateOneSecondWave();
        var reader = new TagLibAudioMetadataReader();

        var metadata = await reader.ReadAsync(wave, "track.wav", "audio/wav");

        Assert.InRange(metadata.Duration.TotalSeconds, 0.99, 1.01);
        Assert.Equal(0, wave.Position);
    }

    private static MemoryStream CreateOneSecondWave()
    {
        const int sampleRate = 8000;
        const short channels = 1;
        const short bitsPerSample = 16;
        const int sampleCount = sampleRate;
        const int dataLength = sampleCount * channels * (bitsPerSample / 8);

        var stream = new MemoryStream(44 + dataLength);
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataLength);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * (bitsPerSample / 8));
            writer.Write((short)(channels * (bitsPerSample / 8)));
            writer.Write(bitsPerSample);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataLength);
            writer.Write(new byte[dataLength]);
        }

        stream.Position = 0;
        return stream;
    }
}
