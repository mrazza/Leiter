namespace Leiter.Tests.Algorithms.Segmentation.Internal;

using Leiter.Algorithms.Segmentation.Internal;
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

        gmm.InitializeAndFit(ReadOnlySpan<float>.Empty, ReadOnlySpan<int>.Empty);
        Assert.True(gmm.EvaluateNegativeLogLikelihood(10f, 20f, 30f) > 100.0);
        Assert.Equal(0, gmm.AssignBestComponent(10f, 20f, 30f));

        float[] rgb =
        [
            10f, 12f, 15f,
            18f, 14f, 19f,
            12f, 22f, 11f,
            200f, 210f, 220f,
            215f, 195f, 205f,
            205f, 220f, 210f,
        ];
        gmm.InitializeAndFit(rgb, [0, 1, 2, 3, 4, 5]);

        int darkCluster = gmm.AssignBestComponent(14f, 16f, 14f);
        int brightCluster = gmm.AssignBestComponent(208f, 208f, 212f);
        Assert.NotEqual(darkCluster, brightCluster);

        double inlierNll = gmm.EvaluateNegativeLogLikelihood(14f, 16f, 14f);
        double outlierNll = gmm.EvaluateNegativeLogLikelihood(20f, 250f, 10f);
        Assert.True(double.IsFinite(inlierNll));
        Assert.True(outlierNll > inlierNll);

        float[] uniform = [50f, 60f, 70f, 50f, 60f, 70f];
        gmm.InitializeAndFit(uniform, [0, 1]);
        Assert.True(double.IsFinite(gmm.EvaluateNegativeLogLikelihood(50f, 60f, 70f)));

        gmm.FitFromAssignedComponents(uniform, [1, 1], expectedLabel: 0, [0, 0], totalMemberCount: 0);
        Assert.True(gmm.EvaluateNegativeLogLikelihood(50f, 60f, 70f) > 100.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => new GaussianMixtureModel(0));
    }
}
