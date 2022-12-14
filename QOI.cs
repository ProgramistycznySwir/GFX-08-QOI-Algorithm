
using System.Runtime.InteropServices;

namespace QOI_Algorithm;

public static class QOI
{
    // Actually I can't declare result buffer on stack with this approach.
    // const long MAX_BUFFER_SIZE = 524_288; // 0.5MiB
    const int INDEX_SIZE = 64;

    public static Span<byte> Encode(Span<byte> bmp, int width, int height, bool hasAlpha)
    {
        Span<Color> index = stackalloc Color[INDEX_SIZE];

        int maxLenght = (bmp.Length / 4) * (3 + 1 + (hasAlpha?1:0));
        Span<byte> result = new byte[maxLenght];

        int currLenght = WriteHeader(result, width, height, hasAlpha);

        int run = 0;
        int lastPixelIdx = bmp.Length - 4;
        Color prev = new(0, 0, 0, 255);
        Color curr;
        for(int i = 0; i < bmp.Length; i+= 4, prev = curr)
        {
            curr = new Color(bmp.Slice(i, 4));

            if(curr.Equals(prev))
            {
                run++;
                if(run is 62 && i == lastPixelIdx)
                {
                    currLenght += QOI_OP_RUN(result.Slice(currLenght), run - 1);
                    run = 0;
                }
                continue;
            }
            else
            {
                // Wrap-up run.
                if(run > 0)
                {
                    currLenght += QOI_OP_RUN(result.Slice(currLenght), run - 1);
                    run = 0;
                }

                int indexPos = QOI.CalculateHashTableIndex(curr);
                if(curr.Equals(index[indexPos]))
                {
                    currLenght += QOI_OP_INDEX(result.Slice(currLenght), indexPos);
                    continue;
                }

                index[indexPos] = curr;

                if(curr.A != prev.A)
                {
                    currLenght += QOI_OP_RGBA(result.Slice(currLenght), curr);
                    continue;
                }
                
                
                int vr = curr.R - prev.R;
                int vg = curr.G - prev.G;
                int vb = curr.B - prev.B;

                int vgr = vr - vg;
                int vgb = vb - vg;

                if (vr is > -3 and < 2 &&
                        vg is > -3 and < 2 &&
                        vb is > -3 and < 2)
                    currLenght += QOI_OP_DIFF(result.Slice(currLenght), vr, vg, vb);
                else if (vgr is > -9 and < 8 &&
                        vg is > -33 and < 32 &&
                        vgb is > -9 and < 8)
                    currLenght += QOI_OP_LUMA(result.Slice(currLenght), vg, vgr, vgb);
                else
                    currLenght += QOI_OP_RGB(result.Slice(currLenght), curr);
            }
        }
        
        currLenght += WritePadding(result.Slice(currLenght));

        return result.Slice(0, currLenght);
    }

    public const string MAGIC_STRING = "qoif";
    public static readonly byte[] MAGIC = CalculateMagic(MAGIC_STRING.AsSpan());
    public static bool IsValidMagic(byte[] magic) => magic.SequenceEqual(MAGIC);    
    private static byte[] CalculateMagic(ReadOnlySpan<char> chars) => new byte[] { (byte)chars[0], (byte)chars[1], (byte)chars[2], (byte)chars[3] };

    static int WriteHeader(Span<byte> buffer, int width, int height, bool hasAlpha, bool isLinearColorSpace = true)
    {
        const byte SIZE = 14;

        buffer[0] = MAGIC[0];
        buffer[1] = MAGIC[1];
        buffer[2] = MAGIC[2];
        buffer[3] = MAGIC[3];

        buffer[4] = (byte)(width >> 24);
        buffer[5] = (byte)(width >> 16);
        buffer[6] = (byte)(width >> 8);
        buffer[7] = (byte)width;

        buffer[8] = (byte)(height >> 24);
        buffer[9] = (byte)(height >> 16);
        buffer[10] = (byte)(height >> 8);
        buffer[11] = (byte)height;

        buffer[12] = (byte)(hasAlpha ? 4 : 3);
        buffer[13] = (byte)(isLinearColorSpace ? 1 : 0);

        return SIZE;
    }
    public static readonly byte[] PADDING = {0, 0, 0, 0, 0, 0, 0, 1};
    static int WritePadding(Span<byte> buffer)
    {
        const byte SIZE = 8;

        PADDING.CopyTo(buffer);

        return SIZE;
    }


    static int QOI_OP_RGB(Span<byte> buffer, Color pixel)
    {
        const byte TAG = 0b11111110;
        const byte SIZE = 4;
        buffer[0] = TAG;
        buffer[1] = pixel.R;
        buffer[2] = pixel.G;
        buffer[3] = pixel.B;
        return SIZE;
    }
    static int QOI_OP_RGBA(Span<byte> buffer, Color pixel)
    {
        const byte TAG = 0b11111111;
        const byte SIZE = 5;
        buffer[0] = TAG;
        buffer[1] = pixel.R;
        buffer[2] = pixel.G;
        buffer[3] = pixel.B;
        buffer[4] = pixel.A;
        return SIZE;
    }
    static int QOI_OP_INDEX(Span<byte> buffer, int position)
    {
        const byte TAG = 0b00000000;
        const byte SIZE = 1;
        buffer[0] = (byte)(TAG | position);
        return SIZE;
    }
    static int QOI_OP_DIFF(Span<byte> buffer, int diffR, int diffG, int diffB)
    {
        const byte TAG = 0b01000000;
        const byte SIZE = 1;
        int r = (diffR + 2) << 4;
        int g = (diffG + 2) << 2;
        int b = (diffB + 2) << 0;
        buffer[0] = (byte)(TAG | r | g | b);
        return SIZE;
    }
    static int QOI_OP_LUMA(Span<byte> buffer, int diffG, int diffGR, int diffGB)
    {
        const byte TAG = 0b10000000;
        const byte SIZE = 2;
        int g = (diffG + 32) << 0;
        buffer[0] = (byte)(TAG | g);
        int r = (diffGR + 8) << 4;
        int b = (diffGB + 8) << 0;
        buffer[1] = (byte)(r | b);
        return SIZE;
    }
    static int QOI_OP_RUN(Span<byte> buffer, int runLenght)
    {
        const byte TAG = 0b11000000;
        const byte SIZE = 1;
        buffer[0] = (byte)(TAG | runLenght);
        return SIZE;
    }

    
    public static int CalculateHashTableIndex(Color pixel) =>
        (
              pixel.R * 3
            + pixel.G * 5
            + pixel.B * 7
            + pixel.A * 11)
        % INDEX_SIZE;
}

[StructLayout(LayoutKind.Explicit)]
public struct Color : IEquatable<Color>
{
    [FieldOffset(0)] public int Value;
    [FieldOffset(0)] public byte R = 0;
    [FieldOffset(1)] public byte G = 0;
    [FieldOffset(2)] public byte B = 0;
    [FieldOffset(3)] public byte A = 255;

    public Color(ReadOnlySpan<byte> span) : this()
    {
        // TZO JEST?! span wygląda: 228,226,225,255 a powinien 225,226,228,255
        // Schemat ARGB jest kompletnie odwrócony xd, TZO JEST?!
        Value = 0;
        B = span[0];
        G = span[1];
        R = span[2];
        A = span[3];
    }
    public Color(byte a, byte r, byte g, byte b) : this()
    {
        Value = 0;
        A = a;
        R = r;
        G = g;
        B = b;
    }

    public bool Equals(Color other) => this.Value == other.Value;

}
