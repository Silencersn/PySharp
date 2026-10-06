namespace PySharp.Resources;

partial class PySR
{
    public const string Runtime_Threading_ThreadAlreadyStarted = "threads can only be started once";
    public const string Runtime_Threading_JoinBeforeStart = "cannot join thread before it is started";
    public const string Runtime_Threading_JoinCurrentThread = "cannot join current thread";
    public const string Runtime_Threading_DaemonOfActiveThread = "cannot set daemon status of active thread";
    public const string Runtime_Threading_GroupMustBeNone = "group argument must be None for now";
    public const string Runtime_Threading_DeprecatedGetName = "getName() is deprecated, get the name attribute instead";
    public const string Runtime_Threading_DeprecatedSetName = "setName() is deprecated, set the name attribute instead";
    public const string Runtime_Threading_DeprecatedIsDaemon = "isDaemon() is deprecated, get the daemon attribute instead";
    public const string Runtime_Threading_DeprecatedSetDaemon = "setDaemon() is deprecated, set the daemon attribute instead";
    public const string Runtime_Threading_TimeoutNonNegative = "timeout value must be a non-negative number";
    public const string Runtime_Threading_TimeoutForNonBlocking = "can't specify a timeout for a non-blocking call";
    public const string Runtime_Threading_TimeoutMustBeNumber = "'{0}' object cannot be interpreted as an integer or float";
    public const string Runtime_Threading_ReleaseUnlockedLock = "release unlocked lock";
    public const string Runtime_Threading_ReleaseUnacquiredLock = "cannot release un-acquired lock";
    public const string Runtime_Threading_InvalidRLockState = "'_acquire_restore' requires a (count, owner) tuple of ints";
    public const string Runtime_Threading_WaitOnUnacquiredLock = "cannot wait on un-acquired lock";
    public const string Runtime_Threading_NotifyOnUnacquiredLock = "cannot notify on un-acquired lock";
    public const string Runtime_Threading_SemaphoreValueNegative = "semaphore initial value must be >= 0";
    public const string Runtime_Threading_SemaphoreReleaseCount = "n must be one or more";
    public const string Runtime_Threading_SemaphoreReleasedTooMany = "Semaphore released too many times";
    public const string Runtime_Threading_SemaphoreTimeoutForNonBlocking = "can't specify timeout for non-blocking acquire";
    public const string Runtime_Threading_BarrierParties = "parties must be >= 1";
    public const string Runtime_Threading_DeprecatedIsSet = "isSet() is deprecated, use is_set() instead";
    public const string Runtime_Threading_DeprecatedNotifyAll = "notifyAll() is deprecated, use notify_all() instead";
    public const string Runtime_Threading_DeprecatedRLockArguments = "Passing arguments to RLock is deprecated and will be removed in 3.15";
    public const string Runtime_Threading_DeprecatedCurrentThread = "currentThread() is deprecated, use current_thread() instead";
    public const string Runtime_Threading_DeprecatedActiveCount = "activeCount() is deprecated, use active_count() instead";
    public const string Runtime_Threading_StackSizeMin = "size must be at least 53248 bytes";
    public const string Runtime_Threading_JoinDummyThread = "cannot join a dummy thread";
    public const string Runtime_Threading_TimeoutTooLarge = "timeout value is too large";
    public const string Runtime_Threading_TimeStampOutOfRange = "timestamp out of range for platform time_t";
    public const string Runtime_Threading_LocalInitArguments = "Initialization arguments are not supported";
}
