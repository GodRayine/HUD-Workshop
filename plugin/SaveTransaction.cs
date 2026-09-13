namespace HudWorkshop;

// The caller must finish preflight before entering this boundary.
public static class SaveTransaction
{
    public static void Run(Action applyAndVerify, Action persist, Action rollback)
    {
        try
        {
            applyAndVerify();
            persist();
        }
        catch (Exception saveError)
        {
            try { rollback(); }
            catch (Exception rollbackError)
            {
                throw new AggregateException("Сохранение и восстановление завершились ошибкой. Проверьте HUD перед продолжением.", saveError, rollbackError);
            }
            throw;
        }
    }
}
