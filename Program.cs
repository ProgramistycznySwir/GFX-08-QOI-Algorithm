using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

Console.WriteLine("ProgramistycznyŚwir's QOI implementation (https://qoiformat.org/)!");

if (args.Length is not 1 or 2)
    PANIC("You have to specify at least one argument (file to compress), and optionally name of new file.");
string filename_in = args[0];
if (File.Exists(filename_in) is false)
    PANIC($"There is no file with name: {filename_in}");

// string filename_in = "D:/Studia/Semestr_7/Grafika/8/QuiteOkAlgorithm/QuiteOkAlgorithm/img/apple_noise.png";


bool decode = filename_in.ToLower().EndsWith(".qoi");
if(decode)
{

}
else
{
    string filename_out =
        args.Length is 2
            ? args[1]
            : $"{filename_in}.qoi";
    Bitmap image_in = new Bitmap(filename_in);
    System.Drawing.Rectangle rect_ = new System.Drawing.Rectangle(0, 0, image_in.Width, image_in.Height);
    BitmapData data = image_in.LockBits(rect_, ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
    int lenght = data.Width * image_in.Height * 4;
    byte[] bytes = new byte[lenght];
    Marshal.Copy(data.Scan0, bytes, 0, lenght);
    bool hasAlpha = CheckIfBitmapHasAlpha(bytes);

    // Main algorithm call.
    Span<byte> qoi_result = QOI_Algorithm.QOI.Encode(bytes, image_in.Width, image_in.Height, hasAlpha);

    // Marshal.Copy(bytes, 0, data.Scan0, lenght);
    image_in.UnlockBits(data);
    // image_in.Save(filename_out);
    File.WriteAllBytes(filename_out, qoi_result.ToArray());

    Console.WriteLine($"Saved result to: {filename_out}");
    Console.WriteLine($"> Compression rate: {(float)(qoi_result.Length)/bytes.Length}");
    Console.WriteLine($"> Output file is: {(float)(new FileInfo(filename_out).Length)/new FileInfo(filename_in).Length} of original");
}

static bool CheckIfBitmapHasAlpha(Span<byte> bmp)
{
    for(int i = 0; i < bmp.Length; i+=4)
        if(bmp[i] is not 255)
            return true;
    return false;
}

