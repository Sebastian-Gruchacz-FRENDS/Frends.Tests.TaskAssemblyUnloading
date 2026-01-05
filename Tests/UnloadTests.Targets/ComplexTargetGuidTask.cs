using System.ComponentModel;


namespace UnloadTests.Targets
{
    // definitions copied from GuidTask: https://github.com/skogsberg89/Frends.Community.Guid
    // this is only to simulate more composite interface (including async methods), not yet fully build Tasks with references

    public class TimeBasedGuidParameters
    {
        [DefaultValue(true)]
        public bool UseMacAddress { get; set; }

        public DateTime? CustomTimestamp { get; set; }

        public ushort? ClockSequence { get; set; }
    }

    public enum Format
    {
        [Description("Braced format: {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}")]
        B,

        [Description("Default format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx")]
        D,

        [Description("Compact format: xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
        N,

        [Description("Parentheses format: (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx)")]
        P
    }

    public class NameBasedGuidParameters
    {
        [DefaultValue("6ba7b810-9dad-11d1-80b4-00c04fd430c8")]
        public string NamespaceGuid { get; set; }

        [DefaultValue("")]
        public string InputString { get; set; }
    }

    public class Options
    {
        [DefaultValue(Format.D)]
        public Format Format { get; set; }
    }

    public class Result
    {
        public System.Guid Guid { get; set; }

        public string GuidString { get; set; }

        public string Version { get; set; }

        public string Format { get; set; }

        public DateTime? Timestamp { get; set; }

        public string NodeIdentifier { get; set; }
    }

    public enum GuidVersion
    {
        V1,
        V3,
        V4,
        V5
    }

    public static class ComplexTargetGuidTask
    {
        public static Result GenerateGuidV1([PropertyTab] TimeBasedGuidParameters parameters,
            [PropertyTab] Options options,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return new Result()
            {
                Version = nameof(GuidVersion.V1)
            };
        }
        public static Result GenerateGuidV3([PropertyTab] NameBasedGuidParameters parameters,
            [PropertyTab] Options options,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return new Result()
            {
                Version = nameof(GuidVersion.V3)
            };
        }

        public static Result GenerateGuidV4([PropertyTab] Options options, CancellationToken cancellationToken)
        {
            return new Result()
            {
                Version = nameof(GuidVersion.V4)
            };
        }

        public static Result GenerateGuidV5([PropertyTab] NameBasedGuidParameters parameters,
            [PropertyTab] Options options,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return new Result()
            {
                Version = nameof(GuidVersion.V5)
            };
        }

    }
}
