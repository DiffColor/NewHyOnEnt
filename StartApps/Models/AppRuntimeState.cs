namespace StartApps.Models;

public record AppRuntimeState(Guid AppId, bool IsRunning, string? Message = null);
