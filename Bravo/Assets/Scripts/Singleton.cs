namespace Noname
{
    /// <summary>파생 타입의 공개 기본 생성자는 제네릭 초기화에 사용하며 호출부는 Instance로 접근한다.</summary>
    public abstract class Singleton<T> where T : Singleton<T>, new()
    {
        public static T Instance { get; } = new T();

        protected Singleton()
        {
        }
    }
}
