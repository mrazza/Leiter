namespace Leiter.Algorithms.DataStructures;

/// <summary>
/// Represents a histogram backed by an internal dictionary where each distinct value forms its own bucket.
/// </summary>
/// <typeparam name="T">The type of elements counted in the histogram.</typeparam>
public class DictionaryHistogram<T> : BucketingHistogram<T> where T : notnull
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DictionaryHistogram{T}"/> class.
    /// </summary>
    public DictionaryHistogram() : base(value => value) { }
}