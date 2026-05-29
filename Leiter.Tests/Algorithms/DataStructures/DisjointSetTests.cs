using Leiter.Tests.TestUtils;
using Xunit;
using Leiter.Core;
using Leiter.Algorithms.DataStructures;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Leiter.Tests.Algorithms.DataStructures;

/// <summary>
/// Provides unit tests for <see cref="DisjointSetTests" />.
/// </summary>
public class DisjointSetTests
{
    /// <summary>
    /// Verifies that the constructor and basic properties should initialize behaves correctly.
    /// </summary>
    [Fact]
    public void Constructor_AndBasicProperties_ShouldInitialize()
    {
        var mat = new DummyUntypedMatrix(4, 4);
        var dsu = new DisjointSet<int>(mat, index => index * 10);

        Assert.Equal(16, dsu.Count);
        Assert.Equal(4, dsu.Width);
        Assert.Equal(4, dsu.Height);
        Assert.Equal(new Size(4, 4), dsu.Size);

        // Initially each element is its own partition
        for (int i = 0; i < 16; i++)
        {
            Assert.Equal(i, dsu[i].Value);
            Assert.Equal(i, dsu[new Coord(i % 4, i / 4)].Value);
            Assert.Equal(i, dsu[i % 4, i / 4].Value);
        }
    }

    /// <summary>
    /// Verifies that the get partition should return snapshot behaves correctly.
    /// </summary>
    [Fact]
    public void GetPartition_ShouldReturnSnapshot()
    {
        var mat = new DummyUntypedMatrix(4, 4);
        var dsu = new DisjointSet<int>(mat, index => index * 10);

        var p = dsu.GetPartition(new Coord(1, 1)); // index 5
        Assert.Equal(5, p.RootIndex);
        Assert.Equal(1, p.Size);
        Assert.Equal(50, p.Value);

        var coords = p.GetCoords().ToList();
        Assert.Single(coords);
        Assert.Equal(new Coord(1, 1), coords[0]);

        var indices = p.GetIndices().ToList();
        Assert.Single(indices);
        Assert.Equal(5, indices[0]);
    }

    /// <summary>
    /// Verifies that the union should merge partitions correctly behaves correctly.
    /// </summary>
    [Fact]
    public void Union_ShouldMergePartitionsCorrectly()
    {
        var mat = new DummyUntypedMatrix(4, 4);
        var dsu = new DisjointSet<int>(mat, index => 0);

        // Union elements and verify size / structure
        // Union 0 and 1
        var p0_1 = dsu.Union(0, 1, 100);
        Assert.Equal(2, p0_1.Size);
        Assert.Equal(100, p0_1.Value);

        // Find should now return the same root (either 0 or 1, DSU chooses based on size rank)
        long r0 = dsu[0].Value;
        long r1 = dsu[1].Value;
        Assert.Equal(r0, r1);

        // Union coords
        var p2_3 = dsu.Union(new Coord(2, 0), new Coord(3, 0), 200); // index 2 and 3
        Assert.Equal(2, p2_3.Size);
        Assert.Equal(200, p2_3.Value);

        // Union different size sets (0-1 set of size 2, 2-3 set of size 2, let's union them)
        var pCombined = dsu.Union(dsu.GetPartition(0), dsu.GetPartition(2), 300);
        Assert.Equal(4, pCombined.Size);
        Assert.Equal(300, pCombined.Value);

        // Coords should contain {0,0}, {1,0}, {2,0}, {3,0}
        var coords = pCombined.GetCoords().ToList();
        Assert.Equal(4, coords.Count);

        // Indices should contain 0, 1, 2, 3
        var indices = pCombined.GetIndices().ToList();
        Assert.Equal(4, indices.Count);

        // Already in same set should throw
        Assert.Throws<ArgumentException>(() => dsu.Union(0, 3, 400));
        Assert.Throws<ArgumentException>(() => dsu.Union(new Coord(0, 0), new Coord(3, 0), 400));
        Assert.Throws<ArgumentException>(() => dsu.Union(dsu.GetPartition(0), dsu.GetPartition(3), 400));
    }

    /// <summary>
    /// Verifies that the get region should return populated region behaves correctly.
    /// </summary>
    [Fact]
    public void GetRegion_ShouldReturnPopulatedRegion()
    {
        var mat = new DummyUntypedMatrix(4, 4);
        var dsu = new DisjointSet<int>(mat, index => 0);

        dsu.Union(0, 1, 10);
        dsu.Union(1, 2, 20);

        var rByPartition = dsu.GetRegion(dsu.GetPartition(0));
        Assert.Equal(3, rByPartition.Pixels.Count);
        Assert.Contains(new Coord(0, 0), rByPartition.Pixels);
        Assert.Contains(new Coord(1, 0), rByPartition.Pixels);
        Assert.Contains(new Coord(2, 0), rByPartition.Pixels);

        var rByCoord = dsu.GetRegion(new Coord(1, 0));
        Assert.Equal(3, rByCoord.Pixels.Count);

        var rByIndex = dsu.GetRegion(2);
        Assert.Equal(3, rByIndex.Pixels.Count);
    }

    /// <summary>
    /// Verifies that the to matrix should create materialized matrix behaves correctly.
    /// </summary>
    [Fact]
    public void ToMatrix_ShouldCreateMaterializedMatrix()
    {
        var mat = new DummyUntypedMatrix(4, 4);
        var dsu = new DisjointSet<int>(mat, index => 0);
        dsu.Union(0, 1, 10);

        var m = dsu.ToMatrix();
        Assert.Equal(mat.Width, m.Width);
        Assert.Equal(mat.Height, m.Height);
        Assert.Equal(dsu[0].Value, m[0].Value);
        Assert.Equal(dsu[1].Value, m[1].Value);
    }

    /// <summary>
    /// Verifies that the to regions should return set of regions behaves correctly.
    /// </summary>
    [Fact]
    public void ToRegions_ShouldReturnSetOfRegions()
    {
        var mat = new DummyUntypedMatrix(4, 4);
        var dsu = new DisjointSet<int>(mat, index => 0);
        dsu.Union(0, 1, 10);
        dsu.Union(2, 3, 20);

        var regions = dsu.ToRegions();
        // Since we had 16 elements, merged (0,1) and (2,3), we should have 14 distinct regions
        Assert.Equal(14, regions.Count);

        var region0_1 = regions.First(r => r.Id == dsu[0].Value);
        Assert.Equal(2, region0_1.Pixels.Count);
        Assert.Contains(new Coord(0, 0), region0_1.Pixels);
        Assert.Contains(new Coord(1, 0), region0_1.Pixels);
    }

    /// <summary>
    /// Verifies that the indexers and get element should delegate to find behaves correctly.
    /// </summary>
    [Fact]
    public void Indexers_AndGetElement_ShouldDelegateToFind()
    {
        var mat = new DummyUntypedMatrix(4, 4);
        var dsu = new DisjointSet<int>(mat, index => 0);
        dsu.Union(0, 1, 10);

        Assert.Equal(dsu[0], dsu.GetElement(0));
        Assert.Equal(dsu[1], dsu.GetElement(1, 0));
    }

    /// <summary>
    /// Verifies that the helpers coord to index index to coord should delegate behaves correctly.
    /// </summary>
    [Fact]
    public void Helpers_CoordToIndex_IndexToCoord_ShouldDelegate()
    {
        var mat = new DummyUntypedMatrix(4, 4);
        var dsu = new DisjointSet<int>(mat, index => 0);

        Assert.Equal(new Coord(1, 2), dsu.CoordFromIndex(9));
        Assert.Equal(9, dsu.IndexFromCoord(new Coord(1, 2)));
    }

    /// <summary>
    /// Verifies that the enumerator should return roots behaves correctly.
    /// </summary>
    [Fact]
    public void Enumerator_ShouldReturnRoots()
    {
        var mat = new DummyUntypedMatrix(4, 4);
        var dsu = new DisjointSet<int>(mat, index => 0);
        dsu.Union(0, 1, 10);

        var roots = dsu.ToList();
        Assert.Equal(16, roots.Count);
        Assert.Equal(roots[0], roots[1]); // same set root

        var seq = (System.Collections.IEnumerable)dsu;
        var seqEnum = seq.GetEnumerator();
        Assert.True(seqEnum.MoveNext());
        Assert.NotNull(seqEnum.Current);
    }
}
