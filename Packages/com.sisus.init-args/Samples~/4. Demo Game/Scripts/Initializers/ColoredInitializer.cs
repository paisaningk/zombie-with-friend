using Sisus.Init;
using UnityEngine;

namespace Init.Demo
{
    /// <summary>
    /// <see cref="Initializer{,}"/> for the <see cref="Colored"/> component.
    /// </summary>
    [AddComponentMenu("Initialization/Demo/Initializers/Colored Initializer")]
    internal sealed class ColoredInitializer : Initializer<Colored, Color> { }
}