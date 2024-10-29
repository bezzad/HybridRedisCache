namespace HybridRedisCache;

/// <summary>
/// Indicates when this operation should be performed (only some variations are legal in a given context).
/// </summary>
public enum Condition
{
    /// <summary>
    /// The operation should occur whether or not there is an existing value.
    /// </summary>
    Always,

    /// <summary>
    /// The operation should only occur when there is an existing value.
    /// </summary>
    Exists,

    /// <summary>
    /// The operation should only occur when there is not an existing value.
    /// </summary>
    NotExists,
}

/// <summary>
/// Behaviour markers associated with a given command.
/// </summary>
[Flags]
public enum Flags
{
    /// <summary>
    /// This operation should be performed on the primary if it is available, but read operations may
    /// be performed on a replica if no primary is available. This is the default option.
    /// </summary>
    PreferMaster = 0,

    /// <summary>
    /// The caller is not interested in the result; the caller will immediately receive a default-value
    /// of the expected return type (this value is not indicative of anything at the server).
    /// </summary>
    FireAndForget = 2,

    /// <summary>
    /// This operation should only be performed on the primary.
    /// </summary>
    DemandMaster = 4,

    /// <summary>
    /// This operation should be performed on the replica if it is available, but will be performed on
    /// a primary if no replicas are available. Suitable for read operations only.
    /// </summary>
    PreferReplica = 8, // note: we're using a 2-bit set here, which [Flags] formatting hates; position is doing the best we can for reasonable outcomes here

    /// <summary>
    /// This operation should only be performed on a replica. Suitable for read operations only.
    /// </summary>
    DemandReplica = 12, // note: we're using a 2-bit set here, which [Flags] formatting hates; position is doing the best we can for reasonable outcomes here

    // 16: reserved for additional "demand/prefer" options

    // 32: used for "asking" flag; never user-specified, so not visible on the public API

    /// <summary>
    /// Indicates that this operation should not be forwarded to other servers as a result of an ASK or MOVED response.
    /// </summary>
    NoRedirect = 64,

    // 128: used for "internal call"; never user-specified, so not visible on the public API

    // 256: used for "script unavailable"; never user-specified, so not visible on the public API

    /// <summary>
    /// Indicates that script-related operations should use EVAL, not SCRIPT LOAD + EVALSHA.
    /// </summary>
    NoScriptCache = 512,

    // 1024: Removed - was used for async timeout checks; never user-specified, so not visible on the public API

    // 2048: Use subscription connection type; never user-specified, so not visible on the public API
}
