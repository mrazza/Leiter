namespace Leiter.Algorithms.DataStructures;

public class DictionaryHistogram<T> : BucketingHistogram<T> where T : notnull
{
    public DictionaryHistogram() : base(value => value) {}
}