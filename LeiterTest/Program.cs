namespace LeiterTest;

using System.Diagnostics;
using Leiter.Algorithms;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Quantization;
using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using Leiter.Platform.ImageSharp;
#if WINDOWS
using Leiter.Platform.Windows;
#endif

/// <summary>
/// Command-line harness for running Leiter image-processing demos and the end-to-end watercolor pipeline on an input photo.
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

        // Standalone operation demos
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
            () => imageMatrix.Map(static pixel => UniformQuantizer.QuantizeToMidpoint(pixel, levelsPerChannel: 4)));

        RunStepAndSave(
            "04. Uniform Quantization (3 bits/channel)",
            outputDir,
            $"{stem}_04_quantized_3bits.png",
            () => imageMatrix.Map(static pixel => UniformQuantizer.QuantizeBitsToMidpoint(pixel, bitsPerChannel: 3)));

        IDisjointSet egbiSeg = RunStep(
            "05. CIELAB + EGBI Segmentation",
            () =>
            {
                double egbiSigma = WatercolorOptions.Default.EgbiBlurSigma;
                Matrix<Rgb8> blurred = Blur.GaussianBlur(imageMatrix, egbiSigma);
                Matrix<Lab32> labImage = blurred.ToLab32(RgbColorSpace.SRgb);
                EgbiOptions egbiOptions = EgbiOptions.ForSize(labImage.Size, WatercolorOptions.DefaultSaliencyEgbi);
                return EgbiSegmentation.Segment(labImage, egbiOptions);
            });
        var egbiRegions = egbiSeg.ToRegions();
        Console.WriteLine($"  Segments: {egbiRegions.Count}");

        RunStepAndSave(
            "06. Color EGBI Segments (Average Color)",
            outputDir,
            $"{stem}_05_egbi_average.png",
            () => SegmentationVisualizer.ColorRegions(imageMatrix, egbiRegions, SegmentationVisualizer.AverageColorAssigner()));

        RunStepAndSave(
            "07. Color EGBI Segments (Random Color, seed=42)",
            outputDir,
            $"{stem}_06_egbi_random.png",
            () => SegmentationVisualizer.ColorRegions(imageMatrix, egbiSeg, seed: 42));

        RunStepAndSave(
            "08. Global Contrast Saliency",
            outputDir,
            $"{stem}_07_saliency_global.png",
            () => GlobalContrastSaliency.ComputeSaliency(imageMatrix).ToGrayscaleRgb8());

        // End-to-end watercolor pipeline (all intermediate and final outputs come from one consistent run)
        WatercolorResult pipelineResult = RunStep(
            "09. End-to-End WatercolorPipeline.RenderDetailed (ForImage)",
            () => WatercolorPipeline.RenderDetailed(
                imageMatrix,
                options: WatercolorOptions.ForImage(imageMatrix),
                onStageCompleted: static progress =>
                    Console.WriteLine($"  [{progress.Elapsed.TotalMilliseconds,6:F0} ms] Stage: {progress.Stage}")));

        Console.WriteLine($"  Matched Palette: {pipelineResult.SelectedPalette.Name}");
        Console.WriteLine($"  Mean-Shift Regions: {pipelineResult.SegmentationGraph.RegionCount}, Boundaries: {pipelineResult.SegmentationGraph.Boundaries.Count}");
        int qualifyingWetCount = pipelineResult.WetInWet.QualifyingBoundaries.Count(static qualified => qualified);
        Console.WriteLine($"  Qualifying Wet Boundaries: {qualifyingWetCount} / {pipelineResult.WetInWet.QualifyingBoundaries.Count}");

        SaveOutput(outputDir, $"{stem}_08_color_transfer.png", pipelineResult.ColorTransferred);
        SaveOutput(outputDir, $"{stem}_09_saliency_regional.png", pipelineResult.SaliencyMap.ToGrayscaleRgb8());
        SaveOutput(outputDir, $"{stem}_10_saliency_regional_masked.png", pipelineResult.ColorTransferred.HadamardProduct<Rgb8, DoublePixel>(pipelineResult.SaliencyMap));
        SaveOutput(outputDir, $"{stem}_11_saliency_cut_mask.png", pipelineResult.SalientMask.ToGrayscaleRgb8());
        SaveOutput(outputDir, $"{stem}_12_distance_field.png", pipelineResult.SaliencyDistanceField.ToGrayscaleRgb8());
        SaveOutput(outputDir, $"{stem}_13_meanshift_filtered.png", pipelineResult.MeanShiftFiltered);
        SaveOutput(
            outputDir,
            $"{stem}_14_meanshift_average.png",
            SegmentationVisualizer.ColorRegions(pipelineResult.ColorTransferred, pipelineResult.SegmentationGraph, SegmentationVisualizer.AverageColorAssigner()));
        SaveOutput(
            outputDir,
            $"{stem}_15_meanshift_random.png",
            SegmentationVisualizer.ColorRegions(pipelineResult.ColorTransferred, pipelineResult.SegmentationGraph, seed: 42));
        SaveOutput(outputDir, $"{stem}_16_lod_abstraction.png", pipelineResult.LodAbstracted);
        SaveOutput(outputDir, $"{stem}_17_wet_in_wet.png", pipelineResult.WetInWet.Image);
        SaveOutput(outputDir, $"{stem}_18_hand_tremor.png", pipelineResult.HandTremor);
        SaveOutput(outputDir, $"{stem}_19_density_adjusted.png", pipelineResult.DensityAdjusted);
        SaveOutput(outputDir, $"{stem}_20_tone_adjusted.png", pipelineResult.ToneAdjusted);
        SaveOutput(outputDir, $"{stem}_21_granulated.png", pipelineResult.Granulated);
        SaveOutput(outputDir, $"{stem}_22_watercolor_pipeline.png", pipelineResult.Image);

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
    private static void RunStepAndSave(string label, string outputDir, string fileName, Func<Matrix<Rgb8>> action)
    {
        Matrix<Rgb8> result = RunStep(label, action);
        SaveOutput(outputDir, fileName, result);
    }

    private static void SaveOutput(string outputDir, string fileName, IReadOnlyMatrix<Rgb8> matrix)
    {
        string outputPath = Path.Combine(outputDir, fileName);
        SaveImage(matrix, outputPath);
        Console.WriteLine($"  Saved -> {outputPath}");
    }

    private static Matrix<Rgb8> LoadImage(string path) =>
#if WINDOWS
        OperatingSystem.IsWindows()
            ? BitmapExtensions.LoadRgb8(path)
            : ImageExtensions.LoadRgb8(path);
#else
        ImageExtensions.LoadRgb8(path);
#endif

    private static void SaveImage(IReadOnlyMatrix<Rgb8> matrix, string path)
    {
#if WINDOWS
        if (OperatingSystem.IsWindows())
        {
            BitmapExtensions.SaveBitmap(matrix, path);
            return;
        }
#endif

        ImageExtensions.SaveImage(matrix, path);
    }
}
