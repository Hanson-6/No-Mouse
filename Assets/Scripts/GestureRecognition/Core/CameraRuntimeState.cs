namespace GestureRecognition.Core
{
    public enum CameraRuntimeState
    {
        Unknown = 0,
        Starting = 1,
        Ready = 2,
        NoDevice = 3,
        PermissionDenied = 4,
        NoFrame = 5,
        StreamStopped = 6,
        BackendUnavailable = 7,
    }
}
