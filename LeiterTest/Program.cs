namespace LeiterTest;

using System.Diagnostics;
using System.Runtime.Versioning;
using Leiter.Algorithms;
using Leiter.Algorithms.AntiAliasing;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Quantization;
using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Stylization;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Command-line harness for running all Leiter image-processing operations on an input photo.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Serves as the main entry point for the Leiter test harness application.
    /// </summary>
    /// <param name="args">The command-line arguments specifying the input image path and optional output directory.</param>
    /// <returns>Zero if execution succeeded; otherwise, non-zero.</returns>
    private static int Main(string[] args)
    {
        if (args.Length == 1 && (args[0] == "-h" || args[0] == "--help"))
        {
            PrintUsage(Console.Out);
            return 0;
        }

        if (args.Length is < 1 or > 2)
        {
            Console.Error.WriteLine("Error: expected 1 or 2 arguments.");
            PrintUsage(Console.Error);
            return 1;
        }

        string inputPath = args[0];
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Error: input file not found: {inputPath}");
            return 1;
        }

        string outputDir = args.Length == 2
            ? args[1]
            : Path.Combine(Directory.GetCurrentDirectory(), "output");
        Directory.CreateDirectory(outputDir);

        string stem = Path.GetFileNameWithoutExtension(inputPath);

        Console.WriteLine($"Input:  {Path.GetFullPath(inputPath)}");
        Console.WriteLine($"Output: {Path.GetFullPath(outputDir)}");

        Matrix<Rgb8> imageMatrix = RunStep("00. Load image", () => LoadImage(inputPath));
        Console.WriteLine($"  Dimensions: {imageMatrix.Width}x{imageMatrix.Height}");

        Matrix<Rgb8> gaussianBlurred = RunStepAndSave(
            "01. Gaussian Blur (sigma=0.8, radius=2)",
            outputDir,
            $"{stem}_01_gaussian_blur.png",
            () => Blur.GaussianBlur(imageMatrix, sigma: 0.8, radius: 2));

        RunStepAndSave(
            "02. Box Blur (radius=2)",
            outputDir,
            $"{stem}_02_box_blur.png",
            () => Blur.BoxBlur(imageMatrix, radius: 2));

        RunStepAndSave(
            "03. Uniform Quantization (4 levels/channel)",
            outputDir,
            $"{stem}_03_quantized_4levels.png",
            () => imageMatrix.Map(pixel => UniformQuantizer.QuantizeToMidpoint(pixel, levelsPerChannel: 4)));

        RunStepAndSave(
            "04. Uniform Quantization (3 bits/channel)",
            outputDir,
            $"{stem}_04_quantized_3bits.png",
            () => imageMatrix.Map(pixel => UniformQuantizer.QuantizeBitsToMidpoint(pixel, bitsPerChannel: 3)));

        IDisjointSet segmentation = RunStep(
            "05. CIELAB + EGBI Segmentation (k=50, eps=1, minSize=200)",
            () =>
            {
                Matrix<Lab32> labImage = gaussianBlurred.Map(pixel => pixel.ToLab32(RgbColorSpace.sRGB));
                return EgbiSegmentation.Segment(labImage, new EgbiOptions(KFactor: 50.0, Epsilon: 1.0, MinRegionSize: 200));
            });

        var regions = segmentation.ToRegions();
        Console.WriteLine($"  Segments: {regions.Count}");

        RunStepAndSave(
            "06a. Color Segments (Average Color)",
            outputDir,
            $"{stem}_05_segmented_average.png",
            () => ColorBySegmentation(imageMatrix, regions, EgbiSegmentation.AverageColorAssigner()));

        RunStepAndSave(
            "06b. Color Segments (Random Color, seed=42)",
            outputDir,
            $"{stem}_06_segmented_random.png",
            () => ColorBySegmentation(imageMatrix, regions, EgbiSegmentation.RandomColorAssigner(randomSeed: 42)));

        RunStepAndSave(
            "07. Global Contrast Saliency",
            outputDir,
            $"{stem}_07_saliency_global.png",
            () => SaliencyToGrayscale(GlobalContrastSaliency.ComputeSaliency(imageMatrix)));

        Matrix<DoublePixel> regionalSaliency = RunStep(
            "08. Regional Contrast Saliency",
            () => RegionalContrastSaliency.ComputeSaliency(imageMatrix, segmentation));
        SaveOutput(outputDir, $"{stem}_08_saliency_regional.png", SaliencyToGrayscale(regionalSaliency));

        RunStepAndSave(
            "09. Saliency-Masked Composite (HadamardProduct)",
            outputDir,
            $"{stem}_09_saliency_regional_masked.png",
            () => imageMatrix.HadamardProduct<Rgb8, DoublePixel>(regionalSaliency));

        (Matrix<Rgb8> colorTransferred, WatercolorPalette matchedPalette) = RunStep(
            "10. Reinhard Color Transfer (Closest Watercolor Palette)",
            () => ColorTransfer.TransferToClosestPalette(imageMatrix));
        Console.WriteLine($"  Matched Palette: {matchedPalette.Name}");
        SaveOutput(outputDir, $"{stem}_10_color_transfer.png", colorTransferred);

        MaskMatrix salientMask = RunStep(
            "11. SaliencyCut Binary Mask",
            () => SaliencyCut.Extract(imageMatrix, regionalSaliency));
        SaveOutput(outputDir, $"{stem}_11_saliency_cut_mask.png", salientMask.Map(static isSalient => isSalient ? new Rgb8(255, 255, 255) : Rgb8.Zero));

        Matrix<DoublePixel> rawDistanceField = RunStep(
            "12a. Saliency Distance Field (Normalized JFA)",
            () => SaliencyDistanceField.ComputeNormalizedField(salientMask));
        SaveOutput(outputDir, $"{stem}_12a_distance_field_raw.png", SaliencyToGrayscale(rawDistanceField));

        Matrix<DoublePixel> smoothedDistanceField = RunStep(
            "12b. Saliency Distance Field (Smoothed)",
            () => SaliencyDistanceField.Compute(salientMask));
        SaveOutput(outputDir, $"{stem}_12b_distance_field_smoothed.png", SaliencyToGrayscale(smoothedDistanceField));

        RunStepAndSave(
            "13a. Mean-Shift Mode Filter (hs=8, hr=5.5)",
            outputDir,
            $"{stem}_13a_meanshift_filtered.png",
            () => MeanShiftSegmentation.Filter(colorTransferred));

        RegionGraph meanShiftGraph = RunStep(
            "13b. Mean-Shift Segmentation (hs=8, hr=5.5, minSize=50) + RegionGraph",
            () =>
            {
                IDisjointSet meanShiftSeg = MeanShiftSegmentation.Segment(colorTransferred);
                return RegionGraph.FromDisjointSet(meanShiftSeg);
            });
        Console.WriteLine($"  Regions: {meanShiftGraph.RegionCount}, Boundaries: {meanShiftGraph.Boundaries.Count}");
        SaveOutput(
            outputDir,
            $"{stem}_13b_meanshift_average.png",
            ColorBySegmentation(colorTransferred, meanShiftGraph.Regions, EgbiSegmentation.AverageColorAssigner()));
        SaveOutput(
            outputDir,
            $"{stem}_13c_meanshift_random.png",
            ColorBySegmentation(colorTransferred, meanShiftGraph.Regions, EgbiSegmentation.RandomColorAssigner(randomSeed: 42)));

        Matrix<Rgb8> lodAbstracted = RunStepAndSave(
            "14. Saliency-Guided LOD Abstraction (Stage 3)",
            outputDir,
            $"{stem}_14_lod_abstraction.png",
            () => LodAbstraction.Apply(colorTransferred, meanShiftGraph, smoothedDistanceField, salientMask));

        WetInWetResult wetInWetResult = RunStep(
            "15. Wet-in-Wet Filter (Stage 4)",
            () => WetInWetFilter.ApplyDetailed(lodAbstracted, meanShiftGraph, smoothedDistanceField, salientMask));
        int qualifyingWetCount = wetInWetResult.QualifyingBoundaries.Count(static qualified => qualified);
        Console.WriteLine($"  Qualifying Wet Boundaries: {qualifyingWetCount} / {wetInWetResult.QualifyingBoundaries.Count}");
        SaveOutput(outputDir, $"{stem}_15_wet_in_wet.png", wetInWetResult.Image);

        Matrix<Rgb8> handTremor = RunStepAndSave(
            "16. Hand Tremor Filter (Stage 5)",
            outputDir,
            $"{stem}_16_hand_tremor.png",
            () => HandTremorFilter.Apply(wetInWetResult.Image, meanShiftGraph, smoothedDistanceField, wetInWetResult, salientMask));

        Matrix<Rgb8> edgeDarkened = RunStepAndSave(
            "17a. Edge Darkening (Stage 6)",
            outputDir,
            $"{stem}_17a_edge_darkening.png",
            () => EdgeDarkening.Apply(handTremor));

        Matrix<Rgb8> turbulent = RunStepAndSave(
            "17b. Turbulence Flow (Stage 6)",
            outputDir,
            $"{stem}_17b_turbulence_flow.png",
            () => TurbulenceFlow.Apply(edgeDarkened));

        Matrix<Rgb8> toneAdjusted = RunStepAndSave(
            "17c. Piecewise Tone Curve (Stage 6)",
            outputDir,
            $"{stem}_17c_tone_curve.png",
            () => PiecewiseToneCurve.Apply(turbulent));

        Matrix<Rgb8> granulated = RunStepAndSave(
            "18. Paper Granulation (Stage 6)",
            outputDir,
            $"{stem}_18_paper_granulation.png",
            () => PaperGranulation.Apply(toneAdjusted));

        RunStepAndSave(
            "19. FXAA Anti-Aliasing (Stage 6 Final)",
            outputDir,
            $"{stem}_19_fxaa_final.png",
            () => Fxaa.Apply(granulated));

        RunStepAndSave(
            "20. End-to-End WatercolorPipeline.Render",
            outputDir,
            $"{stem}_20_watercolor_pipeline.png",
            () => WatercolorPipeline.Render(imageMatrix));

        Console.WriteLine("Done.");
        return 0;
    }

    private static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("Usage: dotnet run --project LeiterTest -- <input-image> [output-directory]");
        writer.WriteLine();
        writer.WriteLine("Arguments:");
        writer.WriteLine("  <input-image>       Path to the source image (e.g. examples/input.png).");
        writer.WriteLine("  [output-directory]  Directory where processed images are written (default: ./output).");
    }

    /// <summary>
    /// Executes a benchmarked processing step, logging its execution time to the console.
    /// </summary>
    /// <typeparam name="T">The result type of the processing step.</typeparam>
    /// <param name="label">The descriptive display label for the step.</param>
    /// <param name="action">The function delegate executing the processing step.</param>
    /// <returns>The result produced by <paramref name="action"/>.</returns>
    private static T RunStep<T>(string label, Func<T> action)
    {
        var stopwatch = Stopwatch.StartNew();
        T result = action();
        stopwatch.Stop();
        Console.WriteLine($"[{stopwatch.ElapsedMilliseconds,6} ms] {label}");
        return result;
    }

    /// <summary>
    /// Executes a benchmarked processing step and saves the resulting image to disk.
    /// </summary>
    /// <param name="label">The descriptive display label for the step.</param>
    /// <param name="outputDir">The directory where the output image will be saved.</param>
    /// <param name="fileName">The file name of the saved image.</param>
    /// <param name="action">The function delegate producing the image matrix.</param>
    /// <returns>The resulting image matrix.</returns>
    private static Matrix<Rgb8> RunStepAndSave(string label, string outputDir, string fileName, Func<Matrix<Rgb8>> action)
    {
        Matrix<Rgb8> result = RunStep(label, action);
        SaveOutput(outputDir, fileName, result);
        return result;
    }

    private static void SaveOutput(string outputDir, string fileName, IReadOnlyMatrix<Rgb8> matrix)
    {
        string outputPath = Path.Combine(outputDir, fileName);
        SaveImage(matrix, outputPath);
        Console.WriteLine($"  Saved -> {outputPath}");
    }

    /// <summary>
    /// Creates a copy of the source image with segmented regions recolored using the specified color assigner.
    /// </summary>
    /// <param name="source">The original image matrix to recolor.</param>
    /// <param name="regions">The set of segmented coordinate regions.</param>
    /// <param name="colorAssigner">The delegate used to determine the color for each region.</param>
    /// <returns>A new image matrix with recolored segment regions.</returns>
    private static Matrix<Rgb8> ColorBySegmentation(
        Matrix<Rgb8> source,
        IEnumerable<Region<Coord>> regions,
        EgbiSegmentation.ColorAssigner<Rgb8> colorAssigner)
    {
        Matrix<Rgb8> clone = source.Clone();
        EgbiSegmentation.ColorImageBySegmentation(clone, regions, colorAssigner);
        return clone;
    }

    /// <summary>
    /// Converts a continuous [0, 1] saliency map into an 8-bit grayscale image matrix.
    /// </summary>
    /// <param name="saliency">The matrix of saliency values in the [0, 1] range.</param>
    /// <returns>An RGB matrix where each channel represents the clamped grayscale intensity.</returns>
    private static Matrix<Rgb8> SaliencyToGrayscale(Matrix<DoublePixel> saliency)
    {
        return saliency.Map(pixel =>
        {
            byte value = (byte)Math.Clamp(Math.Round(pixel.Value * 255.0), 0.0, 255.0);
            return new Rgb8(value, value, value);
        });
    }

    /// <summary>
    /// Loads an image from the specified file path into an RGB matrix using the platform-appropriate loader.
    /// </summary>
    /// <param name="path">The file path of the image to load.</param>
    /// <returns>The loaded image as an <see cref="Rgb8"/> matrix.</returns>
    /// <exception cref="PlatformNotSupportedException">The current operating system is neither Windows nor Linux.</exception>
    private static Matrix<Rgb8> LoadImage(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return LoadWindows(path);
        }

        if (OperatingSystem.IsLinux())
        {
            return LoadLinux(path);
        }

        throw new PlatformNotSupportedException("LeiterTest supports Linux and Windows image loaders only.");
    }

    /// <summary>
    /// Saves an RGB matrix to the specified file path using the platform-appropriate image writer.
    /// </summary>
    /// <param name="matrix">The image matrix to save.</param>
    /// <param name="path">The destination file path.</param>
    /// <exception cref="PlatformNotSupportedException">The current operating system is neither Windows nor Linux.</exception>
    private static void SaveImage(IReadOnlyMatrix<Rgb8> matrix, string path)
    {
        if (OperatingSystem.IsWindows())
        {
            SaveWindows(matrix, path);
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            SaveLinux(matrix, path);
            return;
        }

        throw new PlatformNotSupportedException("LeiterTest supports Linux and Windows image writers only.");
    }

    [SupportedOSPlatform("linux")]
    private static Matrix<Rgb8> LoadLinux(string path)
    {
        using Image<Rgb24> image = Image.Load<Rgb24>(path);
        return Leiter.Platform.Linux.ImageExtensions.ToSequentialMatrix(image);
    }

    [SupportedOSPlatform("linux")]
    private static void SaveLinux(IReadOnlyMatrix<Rgb8> matrix, string path)
    {
        using Image<Rgb24> image = Leiter.Platform.Linux.ImageExtensions.ToImage(matrix);
        image.Save(path);
    }

    [SupportedOSPlatform("windows")]
    private static Matrix<Rgb8> LoadWindows(string path)
    {
        using var bitmap = new System.Drawing.Bitmap(path);
        return Leiter.Platform.Windows.BitmapExtensions.ToSequentialMatrix(bitmap);
    }

    /// <summary>
    /// Saves an RGB matrix to disk on Windows, determining the image format from the file extension.
    /// </summary>
    /// <param name="matrix">The image matrix to save.</param>
    /// <param name="path">The destination file path.</param>
    [SupportedOSPlatform("windows")]
    private static void SaveWindows(IReadOnlyMatrix<Rgb8> matrix, string path)
    {
        using System.Drawing.Bitmap bitmap = Leiter.Platform.Windows.BitmapExtensions.ToBitmap(matrix);
        System.Drawing.Imaging.ImageFormat format = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => System.Drawing.Imaging.ImageFormat.Jpeg,
            ".bmp" => System.Drawing.Imaging.ImageFormat.Bmp,
            ".gif" => System.Drawing.Imaging.ImageFormat.Gif,
            ".tif" or ".tiff" => System.Drawing.Imaging.ImageFormat.Tiff,
            _ => System.Drawing.Imaging.ImageFormat.Png
        };
        bitmap.Save(path, format);
    }
}
