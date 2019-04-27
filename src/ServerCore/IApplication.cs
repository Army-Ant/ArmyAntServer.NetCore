namespace ArmyAnt.Server {
    public enum LogLevel {
        Verbose,
        Debug,
        Info,
        Import,
        Warning,
        Error,
        Fatal,
    }
    public interface IApplication {
        void Log(LogLevel lv, string Tag, params object[] content);
    }
}
