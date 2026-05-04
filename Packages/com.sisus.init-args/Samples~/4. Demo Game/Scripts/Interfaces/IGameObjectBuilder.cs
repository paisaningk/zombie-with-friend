using UnityEngine;

namespace Init.Demo
{
    /// <summary>
    /// Represents an object that manages the creation and disposal
    /// of <see cref="GameObject">GameObjects</see>.
    /// </summary>
    public interface IGameObjectBuilder : IBuilder<GameObject> { }
}