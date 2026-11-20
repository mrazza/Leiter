namespace Leiter.Tests.Algorithms.Segmentation.Internal;

using Leiter.Algorithms.Segmentation.Internal;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="GaussianMixtureModel"/>.
/// </summary>
public class GaussianMixtureModelTests
{
    [Fact]
    public void GaussianMixtureModel_OrchardBoumanAndRegularization_ShouldBehaveCleanly()
    {
        var gmm = new GaussianMixtureModel(componentCount: 3);

        gmm.InitializeAndFit(ReadOnlySpan<Rgb8>.Empty, ReadOnlySpan<int>.Empty);
        Assert.True(gmm.EvaluateNegativeLogLikelihood(new Rgb8(10, 20, 30)) > 100.0);
        Assert.Equal(0, gmm.AssignBestComponent(new Rgb8(10, 20, 30)));

        Rgb8[] rgb =
        [
            new(10, 12, 15),
            new(18, 14, 19),
            new(12, 22, 11),
            new(200, 210, 220),
            new(215, 195, 205),
            new(205, 220, 210),
        ];
        gmm.InitializeAndFit(rgb, [0, 1, 2, 3, 4, 5]);

        int darkCluster = gmm.AssignBestComponent(new Rgb8(14, 16, 14));
        int brightCluster = gmm.AssignBestComponent(new Rgb8(208, 208, 212));
        Assert.NotEqual(darkCluster, brightCluster);

        double inlierNll = gmm.EvaluateNegativeLogLikelihood(new Rgb8(14, 16, 14));
        double outlierNll = gmm.EvaluateNegativeLogLikelihood(new Rgb8(20, 250, 10));
        Assert.True(double.IsFinite(inlierNll));
        Assert.True(outlierNll > inlierNll);

        Rgb8[] uniform = [new(50, 60, 70), new(50, 60, 70)];
        gmm.InitializeAndFit(uniform, [0, 1]);
        Assert.True(double.IsFinite(gmm.EvaluateNegativeLogLikelihood(new Rgb8(50, 60, 70))));

        gmm.FitFromAssignedComponents(uniform, [true, true], targetForeground: false, [0, 0], totalMemberCount: 0);
        Assert.True(gmm.EvaluateNegativeLogLikelihood(new Rgb8(50, 60, 70)) > 100.0);

        // Component counts above the stackalloc threshold (256) exercise the heap fallback safely.
        var largeGmm = new GaussianMixtureModel(componentCount: 300);
        largeGmm.InitializeAndFit(rgb, [0, 1, 2, 3, 4, 5]);
        double largeInlierNll = largeGmm.EvaluateNegativeLogLikelihood(new Rgb8(14, 16, 14));
        double largeOutlierNll = largeGmm.EvaluateNegativeLogLikelihood(new Rgb8(20, 250, 10));
        Assert.True(double.IsFinite(largeInlierNll));
        Assert.True(largeOutlierNll > largeInlierNll);

        Assert.Throws<ArgumentOutOfRangeException>(() => new GaussianMixtureModel(0));
    }
}
