namespace WinUIVerify.Services;

public sealed class VisualRegressionResult
{
    public double Difference { get; set; }
    public string Status => Difference < 1.0 ? "PASS" : "FAIL";
}

public static class VisualRegression
{
    public static VisualRegressionResult Compare(string goldenPath, string actualPath)
    {
        var result = new VisualRegressionResult();

        if (!File.Exists(goldenPath) || !File.Exists(actualPath))
        {
            result.Difference = 100;
            return result;
        }

        try
        {
            using var golden = (System.Drawing.Bitmap)System.Drawing.Image.FromFile(goldenPath);
            using var actual = (System.Drawing.Bitmap)System.Drawing.Image.FromFile(actualPath);

            if (golden.Width != actual.Width || golden.Height != actual.Height)
            {
                result.Difference = 100;
                return result;
            }

            int diffPixels = 0;
            int totalPixels = golden.Width * golden.Height;

            for (int y = 0; y < golden.Height; y++)
            {
                for (int x = 0; x < golden.Width; x++)
                {
                    var gp = golden.GetPixel(x, y);
                    var ap = actual.GetPixel(x, y);

                    if (gp.R != ap.R || gp.G != ap.G || gp.B != ap.B || gp.A != ap.A)
                        diffPixels++;
                }
            }

            result.Difference = Math.Round((double)diffPixels / totalPixels * 100, 2);
        }
        catch
        {
            result.Difference = 100;
        }

        return result;
    }
}
