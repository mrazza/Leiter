namespace Leiter.Algorithms.Segmentation;

using Leiter.Algorithms.Segmentation.Internal;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Implements Rother, Kolmogorov &amp; Blake's GrabCut foreground/background segmentation
/// using 3D RGB Gaussian Mixture Models and an 8-connected Boykov–Kolmogorov min-cut solver.
/// </summary>
public static class GrabCut
{
    /// <summary>
    /// Segments an image given an axis-aligned foreground bounding rectangle.
    /// Pixels inside the rectangle are initialized as <see cref="TrimapLabel.ProbableForeground"/> and pixels outside as <see cref="TrimapLabel.Background"/>.
    /// </summary>
    /// <param name="image">The input RGB image.</param>
    /// <param name="rectangleTopLeft">The top-left coordinate of the bounding rectangle.</param>
    /// <param name="rectangleSize">The width and height of the bounding rectangle.</param>
    /// <param name="options">Optional GrabCut tuning parameters.</param>
    /// <returns>A <see cref="MaskMatrix"/> with <see langword="true"/> for foreground pixels and <see langword="false"/> for background pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="rectangleSize"/> dimensions are not positive or <paramref name="options"/> contains invalid values.</exception>
    /// <exception cref="ArgumentException">Thrown when the rectangle does not overlap the image or leaves zero background pixels outside it.</exception>
    public static MaskMatrix Segment(
        IReadOnlyMatrix<Rgb8> image,
        Coord rectangleTopLeft,
        Size rectangleSize,
        GrabCutOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        GrabCutOptions resolvedOptions = OptionsResolver.Resolve(options);

        TrimapMatrix trimap = TrimapMatrix.FromBoundingBox(image.Size, rectangleTopLeft, rectangleSize);
        return SegmentCore(image, trimap, resolvedOptions);
    }

    /// <summary>
    /// Segments an image using an explicit <see cref="TrimapLabel"/> matrix.
    /// </summary>
    /// <param name="image">The input RGB image.</param>
    /// <param name="trimap">A matrix of <see cref="TrimapLabel"/> values matching <paramref name="image"/> dimensions.</param>
    /// <param name="options">Optional GrabCut tuning parameters.</param>
    /// <returns>A <see cref="MaskMatrix"/> with <see langword="true"/> for foreground pixels and <see langword="false"/> for background pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="trimap"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="trimap"/> dimensions do not match <paramref name="image"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="trimap"/> contains an undefined <see cref="TrimapLabel"/> value or <paramref name="options"/> is invalid.</exception>
    public static MaskMatrix Segment(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<TrimapLabel> trimap,
        GrabCutOptions? options = null)
    {
        Guard.ThrowIfSizeMismatch(image, trimap);

        GrabCutOptions resolvedOptions = OptionsResolver.Resolve(options);
        return SegmentCore(image, trimap, resolvedOptions);
    }

    internal static GrabCutSession CreateSession(IReadOnlyMatrix<Rgb8> image, GrabCutOptions options)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        return new GrabCutSession(image, options);
    }

    private static MaskMatrix SegmentCore(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<TrimapLabel> trimap,
        GrabCutOptions resolvedOptions)
    {
        if (image.Count == 0)
            return new MaskMatrix(image.Size);

        var session = new GrabCutSession(image, resolvedOptions);
        return session.Run(trimap);
    }
}
