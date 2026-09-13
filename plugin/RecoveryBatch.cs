namespace HudWorkshop;

public static class RecoveryBatch
{
    public static List<Exception> AttemptAll(IEnumerable<Action> actions)
    {
        var errors = new List<Exception>();
        foreach (var action in actions)
        {
            try { action(); }
            catch (Exception ex) { errors.Add(ex); }
        }
        return errors;
    }
}
