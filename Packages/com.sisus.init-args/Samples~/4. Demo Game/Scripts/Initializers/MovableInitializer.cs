using Sisus.Init;
using UnityEngine;

namespace Init.Demo
{
    /// <summary>
    /// <see cref="Initializer{,,,}"/> for the <see cref="Movable"/> component.
    /// </summary>
    [AddComponentMenu("Initialization/Demo/Initializers/Movable Initializer")]
    internal sealed class MovableInitializer : Initializer<Movable, IMoveSettings, ILevel, ITimeProvider> { }
}