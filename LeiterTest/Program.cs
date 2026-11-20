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

        RunStepAndSave(
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

        (Matrix<Rgb8> colorTransferred, ColorTransferProfile matchedPalette) = RunStep(
            "10. Reinhard Color Transfer (Closest Watercolor Palette, Stage 1)",
            () => ColorTransfer.TransferToClosestProfile(
                imageMatrix,
                WatercolorPalettes.Defaults,
                featureWeights: WatercolorPalettes.DefaultFeatureWeights));
        Console.WriteLine($"  Matched Palette: {matchedPalette.Name}");
        SaveOutput(outputDir, $"{stem}_10_color_transfer.png", colorTransferred);

        IDisjointSet segmentation = RunStep(
            "05. CIELAB + EGBI Segmentation (Stage 2 on ColorTransferred)",
            () =>
            {
                double egbiSigma = WatercolorOptions.ForSize(colorTransferred.Size).EgbiBlurSigma;
                Matrix<Rgb8> blurredTransferred = Blur.GaussianBlur(colorTransferred, egbiSigma);
                Matrix<Lab32> labImage = blurredTransferred.Map(pixel => pixel.ToLab32(RgbColorSpace.SRgb));
                EgbiOptions egbiOptions = EgbiOptions.ForImage(labImage, new EgbiOptions(KFactor: 50.0, Epsilon: 1.0, MinRegionSize: 200));
                return EgbiSegmentation.Segment(labImage, egbiOptions);
            });

        var regions = segmentation.ToRegions();
        Console.WriteLine($"  Segments: {regions.Count}");

        RunStepAndSave(
            "06a. Color Segments (Average Color)",
            outputDir,
            $"{stem}_05_segmented_average.png",
            () => SegmentationVisualizer.ColorRegions(colorTransferred, regions, SegmentationVisualizer.AverageColorAssigner()));

        RunStepAndSave(
            "06b. Color Segments (Random Color, seed=42)",
            outputDir,
            $"{stem}_06_segmented_random.png",
            () => SegmentationVisualizer.ColorRegions(colorTransferred, regions, SegmentationVisualizer.RandomColorAssigner(seed: 42)));

        RunStepAndSave(
            "07. Global Contrast Saliency",
            outputDir,
            $"{stem}_07_saliency_global.png",
            () => GlobalContrastSaliency.ComputeSaliency(colorTransferred).ToGrayscaleRgb8());

        Matrix<DoublePixel> regionalSaliency = RunStep(
            "08. Regional Contrast Saliency (Stage 2)",
            () => RegionalContrastSaliency.ComputeSaliency(colorTransferred, segmentation));
        SaveOutput(outputDir, $"{stem}_08_saliency_regional.png", regionalSaliency.ToGrayscaleRgb8());

        RunStepAndSave(
            "09. Saliency-Masked Composite (HadamardProduct)",
            outputDir,
            $"{stem}_09_saliency_regional_masked.png",
            () => colorTransferred.HadamardProduct<Rgb8, DoublePixel>(regionalSaliency));

        MaskMatrix salientMask = RunStep(
            "11. SaliencyCut Binary Mask (Stage 2)",
            () => SaliencyCut.Extract(colorTransferred, regionalSaliency, SaliencyCutOptions.ForImage(colorTransferred)));
        SaveOutput(outputDir, $"{stem}_11_saliency_cut_mask.png", salientMask.Map(static isSalient => isSalient ? new Rgb8(255, 255, 255) : Rgb8.Zero));

        Matrix<DoublePixel> rawDistanceField = RunStep(
            "12a. Saliency Distance Field (Normalized JFA)",
            () => SaliencyDistanceField.ComputeNormalizedField(salientMask));
        SaveOutput(outputDir, $"{stem}_12a_distance_field_raw.png", rawDistanceField.ToGrayscaleRgb8());

        Matrix<DoublePixel> smoothedDistanceField = RunStep(
            "12b. Saliency Distance Field (Smoothed)",
            () => SaliencyDistanceField.Compute(salientMask));
        SaveOutput(outputDir, $"{stem}_12b_distance_field_smoothed.png", smoothedDistanceField.ToGrayscaleRgb8());

        (Matrix<Rgb8> meanShiftFiltered, RegionGraph meanShiftGraph) = RunStep(
            "13. Mean-Shift FilterAndSegment (Stage 3) + RegionGraph",
            () =>
            {
                (Matrix<Rgb8> filtered, IDisjointSet meanShiftSeg) = MeanShiftSegmentation.FilterAndSegment(
                    colorTransferred,
                    MeanShiftOptions.ForImage(colorTransferred));
                return (filtered, RegionGraph.FromDisjointSet(meanShiftSeg));
            });
        Console.WriteLine($"  Regions: {meanShiftGraph.RegionCount}, Boundaries: {meanShiftGraph.Boundaries.Count}");
        SaveOutput(outputDir, $"{stem}_13a_meanshift_filtered.png", meanShiftFiltered);
        SaveOutput(
            outputDir,
            $"{stem}_13b_meanshift_average.png",
            SegmentationVisualizer.ColorRegions(colorTransferred, meanShiftGraph.Regions, SegmentationVisualizer.AverageColorAssigner()));
        SaveOutput(
            outputDir,
            $"{stem}_13c_meanshift_random.png",
            SegmentationVisualizer.ColorRegions(colorTransferred, meanShiftGraph.Regions, SegmentationVisualizer.RandomColorAssigner(seed: 42)));

        Matrix<Rgb8> lodAbstracted = RunStepAndSave(
            "14. Saliency-Guided LOD Abstraction (Stage 3)",
            outputDir,
            $"{stem}_14_lod_abstraction.png",
            () => LodAbstraction.Apply(meanShiftFiltered, meanShiftGraph, smoothedDistanceField, salientMask, LodAbstractionOptions.ForImage(meanShiftFiltered)));

        WetInWetResult wetInWetResult = RunStep(
            "15. Wet-in-Wet Filter (Stage 4)",
            () => WetInWetFilter.ApplyDetailed(lodAbstracted, meanShiftGraph, salientMask, WetInWetOptions.ForImage(lodAbstracted)));
        int qualifyingWetCount = wetInWetResult.QualifyingBoundaries.Count(static qualified => qualified);
        Console.WriteLine($"  Qualifying Wet Boundaries: {qualifyingWetCount} / {wetInWetResult.QualifyingBoundaries.Count}");
        SaveOutput(outputDir, $"{stem}_15_wet_in_wet.png", wetInWetResult.Image);

        Matrix<Rgb8> handTremor = RunStepAndSave(
            "16. Hand Tremor Filter (Stage 5)",
            outputDir,
            $"{stem}_16_hand_tremor.png",
            () => HandTremorFilter.Apply(wetInWetResult.Image, meanShiftGraph, smoothedDistanceField, wetInWetResult, HandTremorOptions.ForImage(wetInWetResult.Image)));

        Matrix<Rgb8> edgeDarkened = RunStepAndSave(
            "17a. Edge Darkening (Stage 6)",
            outputDir,
            $"{stem}_17a_edge_darkening.png",
            () => EdgeDarkening.Apply(handTremor, EdgeDarkeningOptions.ForImage(handTremor)));

        Matrix<Rgb8> turbulent = RunStepAndSave(
            "17b. Turbulence Flow (Stage 6)",
            outputDir,
            $"{stem}_17b_turbulence_flow.png",
            () => TurbulenceFlow.Apply(edgeDarkened, TurbulenceFlowOptions.ForImage(edgeDarkened)));

        Matrix<Rgb8> toneAdjusted = RunStepAndSave(
            "17c. Piecewise Tone Curve (Stage 6)",
            outputDir,
            $"{stem}_17c_tone_curve.png",
            () => PiecewiseToneCurve.Apply(turbulent, PiecewiseToneCurveOptions.ForImage(turbulent)));

        Matrix<Rgb8> granulated = RunStepAndSave(
            "18. Paper Granulation (Stage 6)",
            outputDir,
            $"{stem}_18_paper_granulation.png",
            () => PaperGranulation.Apply(toneAdjusted, PaperGranulationOptions.ForImage(toneAdjusted)));

        RunStepAndSave(
            "19. FXAA Anti-Aliasing (Stage 6 Final)",
            outputDir,
            $"{stem}_19_fxaa_final.png",
            () => Fxaa.Apply(granulated, FxaaOptions.ForImage(granulated)));

        RunStepAndSave(
            "20. End-to-End WatercolorPipeline.Render (ForImage)",
            outputDir,
            $"{stem}_20_watercolor_pipeline.png",
            () => WatercolorPipeline.Render(imageMatrix, WatercolorOptions.ForImage(imageMatrix)));

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
    /// Loads an image from the specified file path into an RGB matrix using the platform-appropriate loader.
    /// </summary>
    /// <param name="path">The file path of the image to load.</param>
    /// <returns>The loaded image as an <see cref="Rgb8"/> matrix.</returns>
    private static Matrix<Rgb8> LoadImage(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return LoadWindows(path);
        }

        return LoadImageSharp(path);
    }

    /// <summary>
    /// Saves an RGB matrix to the specified file path using the platform-appropriate image writer.
    /// </summary>
    /// <param name="matrix">The image matrix to save.</param>
    /// <param name="path">The destination file path.</param>
    private static void SaveImage(IReadOnlyMatrix<Rgb8> matrix, string path)
    {
        if (OperatingSystem.IsWindows())
        {
            SaveWindows(matrix, path);
            return;
        }

        SaveImageSharp(matrix, path);
    }

    private static Matrix<Rgb8> LoadImageSharp(string path)
    {
        using Image<Rgb24> image = Image.Load<Rgb24>(path);
        return Leiter.Platform.ImageSharp.ImageExtensions.ToSequentialMatrix(image);
    }

    private static void SaveImageSharp(IReadOnlyMatrix<Rgb8> matrix, string path)
    {
        using Image<Rgb24> image = Leiter.Platform.ImageSharp.ImageExtensions.ToImage(matrix);
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
