namespace SakuraMod.SakuraModCode;

internal static class TaskHelper
{
    public static void RunSafely(Task task)
    {
        _ = RunAndLog(task);
    }

    private static async Task RunAndLog(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"Unhandled background task error: {ex}");
        }
    }
}
