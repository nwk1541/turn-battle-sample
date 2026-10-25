namespace Noname
{
    /// <summary>공개 기본 생성자를 사용하는 일반 C# 싱글톤.</summary>
    public abstract class Singleton<T> where T : Singleton<T>, new()
    {
        public static T Instance { get; } = new T();

        protected Singleton()
        {
        }
    }
}
