# Leiter

[![Build & Test](https://github.com/mrazza/Leiter/actions/workflows/test.yml/badge.svg)](https://github.com/mrazza/Leiter/actions/workflows/test.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License: LGPL v3](https://img.shields.io/badge/License-LGPL_v3-blue.svg)](LICENSE.md)

A pure-C# image processing, computer vision, and non-photorealistic rendering (NPR) library targeting **.NET 10**. `Leiter` provides strongly-typed generic pixel and matrix abstractions with **zero external dependencies** in its core library, cross-platform (`SixLabors.ImageSharp`) and Windows GDI+ (`System.Drawing.Common`) image adapters, and a complete end-to-end artistic **photo watercolorization** pipeline composed from modular primitives.

---

## Features

- **Generic Core & Region Graphs:** Strongly-typed pixel (`Rgb8`, `Rgb64`, `Lab32`, `Xyz32`, `DoublePixel`, `LongPixel`), matrix, and boolean mask types paired with disjoint-set, region-adjacency graph, and histogram (`IReadOnlyHistogram<T>.Total`) primitives.
- **Color Science & Palette Transfer:** Bidirectional conversions across **sRGB**, **Linear RGB**, **CIEXYZ**, and **CIELAB** (D65 & Illuminant E with Bradford adaptation), CIE76 & CIEDE2000 color difference, per-channel LUTs, quantization, and Reinhard statistical color transfer.
- **Image Segmentation:** Multi-threaded Felzenszwalb–Huttenlocher (**EGBI**), Comaniciu–Meer **Mean-Shift**, and **GrabCut** foreground extraction.
- **Visual Saliency & Distance Transforms:** Global and Regional Contrast saliency (`EnableSmoothing`), iterative `SaliencyCut` masking, binary morphology, and Jump Flooding (**JFA**) distance fields.
- **Filtering, Gradients, Noise & Anti-Aliasing:** Separable and recursive Gaussian blurs, box and oriented elliptical filters, `Sobel`/`Scharr` gradients, bilinear/box/nearest resampling and displacement warping, fractal Perlin noise, and **FXAA 3.11**.
- **Stylization & Photo Watercolorization:** Saliency-guided LOD abstraction, pigment blending, tone curves, `WetInWet` bleeding, `HandTremor` warping, `PaperGranulation`, cached `ImageAnalysis` & resolution/content-aware option tuning (`ForSize`/`ForImage`/`ForAnalysis`), and an end-to-end `WatercolorPipeline`.
- **Multi-OS Platform Support:** Adapters for cross-platform `SixLabors.ImageSharp` (`LoadRgb8`/`SaveImage`) and Windows GDI+ (`System.Drawing.Common`, `LoadRgb8`/`SaveBitmap` targeting `net10.0-windows`).

---

## Example Usage

### 1. Segmentation, Visual Saliency & Salient Object Extraction

Loads an image, computes EGBI segmentation in CIELAB, evaluates Global and Regional Contrast Saliency, and extracts a refined foreground mask plus normalized saliency distance field via `SaliencyCut` and `1+JFA`:

```csharp
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using Leiter.Algorithms;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Platform.ImageSharp; // Or Leiter.Platform.Windows on Windows

class Program
{
    static void Main()
    {
        // 1. Load an image into a generic Leiter Matrix
        Matrix<Rgb8> imageMatrix = ImageExtensions.LoadRgb8("examples/input.png"); // Or BitmapExtensions.LoadRgb8 on Windows

        // 2. Smooth with Gaussian blur and convert to CIELAB for perceptual segmentation
        Matrix<Rgb8> blurred = Blur.GaussianBlur(imageMatrix, sigma: 0.8);
        Matrix<Lab32> labImage = blurred.ToLab32(RgbColorSpace.SRgb);

        // 3. Segment with EGBI (Felzenszwalb–Huttenlocher)
        var egbiOptions = new EgbiOptions(KFactor: 50.0, Epsilon: 1.0, MinRegionSize: 200);
        IDisjointSet segmentation = EgbiSegmentation.Segment(labImage, egbiOptions);

        Matrix<Rgb8> segmented = SegmentationVisualizer.ColorRegions(
            imageMatrix,
            segmentation.ToRegions(),
            SegmentationVisualizer.AverageColorAssigner());
        segmented.SaveImage("segmented.png"); // Or segmented.SaveBitmap("segmented.png") on Windows

        // 4. Compute Global (HC) and Regional (RC) Contrast Saliency
        Matrix<DoublePixel> globalSaliency = GlobalContrastSaliency.ComputeSaliency(
            imageMatrix,
            new GlobalContrastSaliencyOptions(EnableSmoothing: true));
        Matrix<DoublePixel> regionalSaliency = RegionalContrastSaliency.ComputeSaliency(imageMatrix, segmentation);

        // 5. Extract a refined binary foreground mask and smoothed [0, 1] distance field
        (MaskMatrix salientMask, Matrix<DoublePixel> distanceField) = SaliencyDistanceField.Compute(
            imageMatrix,
            regionalSaliency,
            new SaliencyCutOptions(MaxOuterIterations: 4));
    }
}
```

### 2. Artistic Photo Watercolorization

Runs the complete 6-stage photo watercolorization pipeline in a single call, or with per-stage customization (including `WetInWet`, `HandTremor`, and `PaperGranulation.Apply`) and intermediate artifact inspection:

```csharp
using Leiter.Algorithms;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Platform.ImageSharp;

Matrix<Rgb8> source = ImageExtensions.LoadRgb8("examples/input.png");

// Option A: Render with default parameters and automatic palette matching
Matrix<Rgb8> watercolor = WatercolorPipeline.Render(source);
watercolor.SaveImage("watercolor.png"); // Or watercolor.SaveBitmap("watercolor.png") on Windows

// Option B: Analyze once with ImageAnalysis and tune stage/pipeline options without re-analyzing
// (individual stages also expose *Options.ForImage(...), *Options.ForAnalysis(...), and *Options.ForSize(...))
ImageAnalysis analysis = ImageAnalysis.Analyze(source);
WatercolorOptions autoOptions = WatercolorOptions.ForAnalysis(analysis);
Matrix<Rgb8> resolutionTuned = WatercolorPipeline.Render(source, options: autoOptions);

// Option C: Tune stage options explicitly (passing paperHeightMap before options) and inspect all intermediates
var options = new WatercolorOptions(
    TargetPalette: WatercolorPalettes.WarmOchreEarth,
    MeanShift: new MeanShiftOptions(SpatialRadius: 8, RangeRadius: 5.5, MinRegionSize: 50),
    WetInWet: new WetInWetOptions(Mode: WetInWetMode.Default),
    HandTremor: new HandTremorOptions(Amplitude: 2.5),
    TurbulenceFlow: new TurbulenceFlowOptions(Intensity: 0.35, Noise: TurbulenceFlowOptions.DefaultNoise),
    PaperGranulation: new PaperGranulationOptions(DensityStrength: 0.3, WarpAmplitude: 0.6, GrainNoise: PaperGranulationOptions.DefaultGrainNoise));

Matrix<DoublePixel> paperHeightMap = PaperGranulation.GeneratePaperHeightMap(source.Size, options.PaperGranulation?.GrainNoise);
WatercolorResult detailed = WatercolorPipeline.RenderDetailed(source, paperHeightMap, options);
// Standalone finishing stage uses the same parameter order:
// Matrix<Rgb8> granulated = PaperGranulation.Apply(detailed.ToneAdjusted, paperHeightMap, options.PaperGranulation);
// Available artifacts: detailed.ColorTransferred, detailed.SelectedPalette,
// detailed.SaliencyMap, detailed.SalientMask, detailed.SaliencyDistanceField,
// detailed.MeanShiftFiltered, detailed.SegmentationGraph, detailed.LodAbstracted,
// detailed.WetInWet (.Image, .WetAreaMask, .QualifyingBoundaries), detailed.HandTremor,
// detailed.DensityAdjusted, detailed.ToneAdjusted, detailed.Granulated, and detailed.Image.
```

---

## Visual Showcases

### Segmentation, Visual Saliency & Salient Object Extraction

| Original Input | EGBI Segmentation | Global Contrast Saliency (HC) |
|:---:|:---:|:---:|
| <img src="examples/input.png" width="320"/> | <img src="examples/segmented.png" width="320"/> | <img src="examples/global_saliency.png" width="320"/> |
| *Input image (`1408×768`)* | *EGBI regions (`k = 50, min = 200`)* | *Histogram-based global contrast* |

| Regional Contrast Saliency (RC) | `SaliencyCut` Foreground Mask | Saliency Distance Field (`1+JFA`) |
|:---:|:---:|:---:|
| <img src="examples/regional_saliency.png" width="320"/> | <img src="examples/saliency_cut.png" width="320"/> | <img src="examples/distance_field.png" width="320"/> |
| *Segmentation-weighted regional contrast* | *Iterative `GrabCut` + morphology refinement* | *Collinear ray–border normalized & smoothed* |

### Photo Watercolorization Pipeline

| Stage 3: LOD Abstraction | Stage 4: Wet-in-Wet Bleeding | Stage 5: Hand Tremor Effect | Stage 6: Final Watercolor Painting |
|:---:|:---:|:---:|:---:|
| <img src="examples/lod_abstraction.png" width="240"/> | <img src="examples/wet_in_wet.png" width="240"/> | <img src="examples/hand_tremor.png" width="240"/> | <img src="examples/watercolor.png" width="240"/> |
| *Saliency-guided Mean-Shift abstraction* | *Oriented elliptical feathering* | *Perlin edge warp, gaps & glazes* | *Edge darkening, granulation & FXAA* |

---

## Solution Architecture & CLI Harness

- **`Leiter`**: Zero-dependency core library containing matrices, pixel types, color spaces, vision algorithms, and stylization pipelines.
- **`Leiter.Platform.ImageSharp`**: Cross-platform image I/O via `SixLabors.ImageSharp` (`LoadRgb8`, `SaveImage`, `ToSequentialMatrix`, `ToImage`).
- **`Leiter.Platform.Windows`**: Native Windows GDI+ (`System.Drawing.Common`) image I/O via `Bitmap.LockBits` (`LoadRgb8`, `SaveBitmap`, `ToSequentialMatrix`, `ToBitmap`), targeting `net10.0-windows` with `EnableWindowsTargeting=true` so the full solution builds cross-platform.
- **`Leiter.Tests`**: Unit test suite validating core math, platform interop, and algorithm behavior (targets `net10.0-windows` on Windows and `net10.0` elsewhere, skipping Windows-only GDI+ tests on non-Windows hosts).
- **`LeiterTest`**: CLI benchmark harness executing the library's operations against an input image (targets `net10.0-windows` on Windows and `net10.0` elsewhere).

### Build, Test & Run CLI Benchmark

```bash
# Build the full solution in Release mode
dotnet build Leiter.slnx --configuration Release

# Run the xUnit test suite
dotnet test Leiter.slnx --configuration Release

# Run the CLI harness against an image (writes benchmark timings & PNG outputs)
dotnet run --project LeiterTest --configuration Release -- examples/input.png ./output
```

---

## References

- Wang, M., Wang, B., Fei, Y., Qian, K., Wang, W., Chen, J., & Yong, J.-H. *"Towards Photo Watercolorization with Artistic Verisimilitude."* IEEE TVCG, 2014.
- Cheng, M.-M., Mitra, N. J., Huang, X., Torr, P. H. S., & Hu, S.-M. *"Global Contrast Based Salient Region Detection."* IEEE TPAMI, 2015.
- Felzenszwalb, P. F., & Huttenlocher, D. P. *"Efficient Graph-Based Image Segmentation."* IJCV, 2004.
- Comaniciu, D., & Meer, P. *"Mean Shift: A Robust Approach Toward Feature Space Analysis."* IEEE TPAMI, 2002.
- Rother, C., Kolmogorov, V., & Blake, A. *"'GrabCut' — Interactive Foreground Extraction using Iterated Graph Cuts."* ACM TOG (SIGGRAPH), 2004.
- Bousseau, A., Kaplan, M., Thollot, J., & Sillion, F. X. *"Interactive Watercolor Rendering with Temporal Coherence and Abstraction."* NPAR, 2006.
- Reinhard, E., Adhikhmin, M., Gooch, B., & Shirley, P. *"Color Transfer between Images."* IEEE CG&A, 2001.
- Rong, G., & Tan, T.-S. *"Jump Flooding in GPU with Applications to Voronoi Diagram and Distance Transform."* ACM I3D, 2006.
- Young, I. T., & van Vliet, L. J. *"Recursive Implementation of the Gaussian Filter."* Signal Processing, 1995; Triggs, B., & Sdika, M. *"Boundary Conditions for Young–van Vliet Recursive Filtering."* IEEE TSP, 2006.
- Lottes, T. *"FXAA 3.11."* NVIDIA, 2011.

---

## License

This project is licensed under the GNU Lesser General Public License v3.0 (LGPL-3.0) — see [LICENSE.md](LICENSE.md) for details.
