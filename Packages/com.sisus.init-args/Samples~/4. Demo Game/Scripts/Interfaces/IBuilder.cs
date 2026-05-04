using UnityEngine;

namespace Init.Demo
{
    /// <summary>
    /// Represents an object that manages the creation and disposal
    /// of objects of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">
    /// Type of the objects that this object can create and dispose of.
    /// </typeparam>
    public interface IBuilder<T>
    {
        /// <summary>
        /// Returns a new instance of type <see cref="T"/>.
        /// </summary>
        /// <returns>
        /// An active instance of type <see cref="T"/> ready to be used.
        /// </returns>
        T Create(Vector3 position);

        /// <summary>
        /// Disposes instance of type 
        /// </summary>
        /// <param name="gameObject"></param>
        void Dispose(T instance);
    }
}