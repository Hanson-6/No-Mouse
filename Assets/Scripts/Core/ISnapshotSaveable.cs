/// <summary>
/// 组件级快照接口。
/// 实现该接口的组件会被 SaveManager 自动纳入 JSON 快照。
/// </summary>
public interface ISnapshotSaveable
{
    string CaptureSnapshotState();
    void RestoreSnapshotState(string stateJson);
}
