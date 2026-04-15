using UnityEngine;

public class SingletonBase : MonoBehaviour
{
    public virtual bool IsInstanceCreated { get; protected set; }
}

public abstract class Singleton<T> : SingletonBase where T : class
{
    protected abstract bool dontDestroyOnLoad { get; }

    public override bool IsInstanceCreated => instance != null;

    protected static T instance;

    public static T Instance => instance;

    protected virtual void Awake()
    {
        if (instance == null)
        {
            instance = this as T;
            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

        }
        else
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnDestroy()
    {
        var ownInstance = this as T;
        if (instance != null && instance == ownInstance)
        {
            instance = null;
        }
    }
}
