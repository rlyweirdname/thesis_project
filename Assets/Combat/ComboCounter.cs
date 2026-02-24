using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Lives on the player. Tracks current combo count; never auto-resets on enemy death.
    /// </summary>
    public class ComboCounter : MonoBehaviour
    {
        [SerializeField] private int value;
        public int Value => value;

        public void Increment()
        {
            value = Mathf.Max(0, value + 1);
        }

        public void ResetCounter()
        {
            value = 0;
        }
    }
}
