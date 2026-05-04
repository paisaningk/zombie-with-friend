using Sisus.Init;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Init.Demo
{
	/// <summary>
	/// An object that can provide randomized <see cref="Color">colors</see>.
	/// </summary>
	public sealed class RandomColorProvider : ScriptableObject, IValueProvider<Color>, ISerializationCallbackReceiver
    {
        [SerializeField]
        private Vector2 r = new Vector2(0f, 1f);

        [SerializeField]
        private Vector2 g = new Vector2(0f, 1f);

        [SerializeField]
        private Vector2 b = new Vector2(0f, 1f);

        /// <inheritdoc/>
		public Color Value => Next();

		/// <summary>
		/// Initializes a new instance of the <see cref="RandomColorProvider"/> class.
		/// </summary>
		public RandomColorProvider() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="RandomColorProvider"/> class.
        /// </summary>
        /// <param name="r"> The min and max values of the red component of the color. </param>
        /// <param name="r"> The min and max values of the green component of the color. </param>
        /// <param name="r"> The min and max values of the blue component of the color. </param>
        public RandomColorProvider(Vector2 r, Vector2 g, Vector2 b)
        {
            this.r = r;
            this.g = g;
            this.b = b;
        }

        /// <summary>
        /// Returns a random color.
        /// </summary>
        /// <returns> A fully opaque <see cref="Color"/> with randomized red, green and blue components. </returns>
        public Color Next()
        {
            return new Color(Random.Range(r.x, r.y), Random.Range(g.x, g.y), Random.Range(b.x, b.y), 1f);
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            Normalize(ref r);
            Normalize(ref g);
            Normalize(ref b);
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize() { }

        private void Normalize(ref Vector2 value)
        {
            if(value.x < 0f)
            {
                value = new Vector2(0f, value.y);
            }

            if(value.y > 1f)
            {
                value = new Vector2(value.x, 1f);
            }

            if(value.x > value.y)
            {
                value = new Vector2(value.y, value.x);
            }
        }
    }
}