namespace Model.Config
{
    public sealed record ParallelismConfig
    {
        public int PartitionCount { get; init; }
    }
}
