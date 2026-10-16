using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using AIVoice2Bridge.YMM4.Bridge;

namespace AIVoice2Bridge.YMM4;

internal static class WaveValidation
{
    public static async Task ValidateAsync(string path, BridgeAudioResult result, bool verifyHash,
        CancellationToken cancellationToken)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] header = new byte[44];
        await file.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        var valid = header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 8).SequenceEqual("WAVEfmt "u8)
            && header.AsSpan(36, 4).SequenceEqual("data"u8)
            && BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(20)) == 1
            && BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(22)) == 1
            && BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(24)) == 48000
            && BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(34)) == 16
            && BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(40)) == result.PcmBytes
            && file.Length == result.PcmBytes + 44 && file.Length == result.FileBytes
            && result.SampleRate == 48000 && result.Channels == 1 && result.SampleWidth == 2;
        if (!valid) throw new InvalidDataException("A.I.VOICE2 返回的文件不符合已验证的 48 kHz / PCM16 / 单声道 WAV 格式。" );
        if (verifyHash)
        {
            file.Position = 0;
            var actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(file, cancellationToken).ConfigureAwait(false));
            if (!string.Equals(actual, result.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A.I.VOICE2 导出 WAV 的 SHA-256 校验失败。" );
        }
    }
}
