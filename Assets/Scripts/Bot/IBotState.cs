public interface IBotState
{
    void      OnEnter(BotContext ctx);
    BotCommand Update(BotContext ctx);
    void      OnExit(BotContext ctx);
}
