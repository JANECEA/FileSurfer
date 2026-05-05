using FileSurfer.Core;

namespace Mocks;

public sealed class MockDefaultSettingsProvider : ServiceMock, IDefaultSettingsProvider
{
    public void PopulateDefaults(SettingsRecord settingsRecord) =>
        RecordCall(nameof(PopulateDefaults), settingsRecord);
}
